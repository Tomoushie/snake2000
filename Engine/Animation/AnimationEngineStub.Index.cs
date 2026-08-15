// /Engine/Animation/AnimationEngineStub.Index.cs
//
// Responsabilités : Catalogue, orchestration, supervision, introspection des sous-systèmes AnimationEngineStub.
// Dépendances : Tous les fichiers AnimationEngineStub.*.cs (partials), EventBus, IRenderPipeline, etc.
// Intègre : SubsystemRegistry, DependencyGraph, HealthMonitor, MetricsLink, SecurityLevel, StressProfile.
// Ajouts : Découverte dynamique, orchestration avancée, monitoring, interopérabilité, configuration, documentation, audit, historique.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Engine.Core;
using Engine.Services;
using Engine.Events;
using Engine.Rendering; // Pour LinkToRenderGraph

namespace Engine.Animation
{
    #region Supporting Types (Index - Ajouts)

    public enum PoolResizeStrategy
    {
        Manual,
        Adaptive,
        Conservative,
        Aggressive
    }

    public enum StubClusterState
    {
        Idle,
        Initializing,
        Active,
        ScalingUp,
        ScalingDown,
        Error,
        ShuttingDown
    }

    public enum ThreadingMode
    {
        SingleThreaded,
        MultiThreaded,
        JobBased,
        AsyncBased
    }

    public enum DiagnosticsLevel
    {
        None,
        Basic,
        Verbose,
        Full
    }

    // [AJOUT] Attributs pour la découverte
    [AttributeUsage(AttributeTargets.Class)]
    public class SubsystemAttribute : Attribute
    {
        public SubsystemType Type { get; }
        public string Description { get; }
        public bool Experimental { get; set; } = false;
        public bool Deprecated { get; set; } = false;
        public List<string> Dependencies { get; set; } = new List<string>();
        public SubsystemAttribute(SubsystemType type, string description) => (Type, Description) = (type, description);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DependsOnAttribute : Attribute
    {
        public List<string> Dependencies { get; }
        public DependsOnAttribute(params string[] deps) => Dependencies = deps.ToList();
    }

    // [AJOUT] Interfaces pour l'interopérabilité
    public interface IMetricsProvider
    {
        AnimationEngineMetrics GetMetrics();
    }

    public interface IOrchestrator
    {
        void PublishMetrics(AnimationEngineMetrics metrics);
        void LogMessage(string message);
    }

    public interface IMovementSystem
    {
        void SetAnimationRootMotion(string entityId, Vector3 position, Quaternion rotation);
    }

    public interface IAudioEngine
    {
        void PlayAnimationSound(string eventId, string soundName);
    }

    public interface IPhysicsEngine
    {
        void ApplyAnimationForces(string entityId, List<Vector3> forces);
    }

    #endregion

    /// <summary>
    /// Centralise la gestion, la découverte, l'interopérabilité et la supervision de tous les sous-systèmes AnimationEngineStub.
    /// Fournit une API riche pour l'orchestration, le monitoring, le debugging et la configuration dynamique.
    /// Intègre : Découverte dynamique, orchestration avancée, monitoring, interopérabilité, configuration, documentation, audit, historique.
    /// </summary>
    public static class AnimationEngineIndex
    {
        #region Fields

        private static readonly ConcurrentDictionary<string, object> _subsystemRegistry = new ConcurrentDictionary<string, object>();
        private static readonly ConcurrentDictionary<string, SubsystemDescriptor> _descriptorRegistry = new ConcurrentDictionary<string, SubsystemDescriptor>();
        private static readonly ConcurrentDictionary<SubsystemType, List<string>> _typeIndex = new ConcurrentDictionary<SubsystemType, List<string>>();
        private static readonly ConcurrentDictionary<string, SubsystemHealthStatus> _healthMap = new ConcurrentDictionary<string, SubsystemHealthStatus>();
        private static readonly ConcurrentDictionary<string, List<string>> _dependencyGraph = new ConcurrentDictionary<string, List<string>>();
        private static readonly ConcurrentDictionary<string, string> _versionMap = new ConcurrentDictionary<string, string>();
        private static readonly List<string> _loadOrder = new List<string>();
        private static readonly List<string> _criticalSubsystems = new List<string>();
        private static readonly List<string> _experimentalSubsystems = new List<string>();
        private static readonly List<string> _deprecatedSubsystems = new List<string>();
        private static readonly List<string> _externalSubsystems = new List<string>();
        private static readonly object _lock = new object();

        // [AJOUT] Pour la découverte dynamique
        private static readonly List<Type> _discoveredTypes = new List<Type>();
        private static readonly List<string> _pluginPaths = new List<string> { "Plugins/Animation/" }; // Chemin par défaut

        // [AJOUT] Pour l'interopérabilité
        private static IRenderPipeline _linkedRenderPipeline;
        private static IOrchestrator _linkedOrchestrator;
        private static IMovementSystem _linkedMovementSystem;
        private static IAudioEngine _linkedAudioEngine;
        private static IPhysicsEngine _linkedPhysicsEngine;

        // [AJOUT] Pour la configuration dynamique
        private static DiagnosticsLevel _currentDiagnosticsLevel = DiagnosticsLevel.Basic;
        private static ThreadingMode _currentThreadingMode = ThreadingMode.SingleThreaded;
        private static float _currentMetricsSamplingRate = 1.0f; // 100%

        #endregion

        #region A. Structure générale du fichier (Mis à jour)

        /// <summary>
        /// Enregistre tous les sous-systèmes connus et initialise leur état.
        /// Désormais inclut la découverte dynamique.
        /// </summary>
        public static void RegisterSubsystems(AnimationEngineStub engine)
        {
            lock (_lock)
            {
                // [AJOUT] Découvrir les sous-systèmes
                DiscoverSubsystems();
                DiscoverExternalModules();

                // Enregistrement des sous-systèmes internes (partials) - via la découverte
                RegisterDiscoveredSubsystems(engine);

                // Calcul de l'ordre de chargement basé sur les dépendances
                CalculateLoadOrder();

                // Marquer les sous-systèmes critiques (exemples)
                _criticalSubsystems.Add("Core");
                _criticalSubsystems.Add("Metrics");
            }
        }

        private static void RegisterDiscoveredSubsystems(AnimationEngineStub engine)
        {
            foreach (var type in _discoveredTypes)
            {
                if (typeof(IAnimationSubsystem).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type, engine) as IAnimationSubsystem;
                        if (instance != null)
                        {
                            var name = instance.Name;
                            var attributes = type.GetCustomAttributes<SubsystemAttribute>(false);
                            var subsystemAttr = attributes.FirstOrDefault();
                            if (subsystemAttr != null)
                            {
                                var depsAttr = type.GetCustomAttribute<DependsOnAttribute>(false);
                                var deps = depsAttr?.Dependencies ?? new List<string>();

                                var desc = new SubsystemDescriptor(
                                    name, "1.0.0", deps, subsystemAttr.Type, SubsystemHealthStatus.Unknown,
                                    subsystemAttr.Description,
                                    new List<string> { "Discovered" }, "Engine Team", DateTime.UtcNow, StubFeatureFlags.None, SubsystemSecurityLevel.Low, StressProfileExtensions.Default, 0.0f);

                                _subsystemRegistry[name] = instance;
                                _descriptorRegistry[name] = desc;
                                _healthMap[name] = instance.GetHealthStatus();
                                _versionMap[name] = desc.Version;
                                _dependencyGraph[name] = deps;
                                _typeIndex.AddOrUpdate(subsystemAttr.Type, new List<string> { name }, (t, l) => { l.Add(name); return l; });

                                if (subsystemAttr.Experimental) _experimentalSubsystems.Add(name);
                                if (subsystemAttr.Deprecated) _deprecatedSubsystems.Add(name);

                                System.Console.WriteLine($"[Index] Registered discovered subsystem: {name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[Index] Failed to register discovered subsystem {type.Name}. Error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Découvre dynamiquement les sous-systèmes dans l'assembly courant.
        /// </summary>
        private static void DiscoverSubsystems()
        {
            _discoveredTypes.Clear();
            var currentAssembly = Assembly.GetExecutingAssembly();
            var subsystemTypes = currentAssembly.GetTypes()
                .Where(t => t.Namespace?.StartsWith("Engine.Animation") == true && t.GetCustomAttribute<SubsystemAttribute>() != null);
            _discoveredTypes.AddRange(subsystemTypes);
            System.Console.WriteLine($"[Index] Discovered {_discoveredTypes.Count} internal subsystems.");
        }

        /// <summary>
        /// Découvre dynamiquement les modules externes dans les dossiers de plugins.
        /// </summary>
        private static void DiscoverExternalModules()
        {
            foreach (var path in _pluginPaths)
            {
                if (Directory.Exists(path))
                {
                    var dllFiles = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);
                    foreach (var dllPath in dllFiles)
                    {
                        try
                        {
                            var assembly = Assembly.LoadFrom(dllPath);
                            var externalTypes = assembly.GetTypes()
                                .Where(t => typeof(IExternalSubsystem).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                            foreach (var type in externalTypes)
                            {
                                var instance = Activator.CreateInstance(type) as IExternalSubsystem;
                                if (instance != null && ValidateExternalSubsystemCompatibility(instance))
                                {
                                     _discoveredTypes.Add(type);
                                     System.Console.WriteLine($"[Index] Discovered external subsystem: {instance.Name} from {dllPath}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"[Index] Failed to load external subsystems from {dllPath}. Error: {ex.Message}");
                        }
                    }
                }
            }
            System.Console.WriteLine($"[Index] Discovered {(_discoveredTypes.Count - GetInternalSubsystemCount())} external subsystems.");
        }

        private static int GetInternalSubsystemCount()
        {
            // Approximatif, basé sur les enums connus
            return Enum.GetNames(typeof(SubsystemType)).Length;
        }

        // ... (autres méthodes RegisterSubsystems, ListSubsystems, GetSubsystem<T>, etc. restent inchangées) ...

        #endregion

        #region B. Informations descriptives (Mis à jour)

        /// <summary>
        /// Génère une description textuelle de tous les sous-systèmes, incluant les attributs découverts.
        /// </summary>
        public static string DescribeAllSubsystems()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("--- AnimationEngineStub Subsystem Index (Dynamic Discovery Included) ---");
            foreach (var kvp in _descriptorRegistry)
            {
                var desc = kvp.Value;
                sb.AppendLine($"Name: {desc.Name}");
                sb.AppendLine($"  Version: {desc.Version}");
                sb.AppendLine($"  Type: {desc.Type}");
                sb.AppendLine($"  Status: {desc.Status}");
                sb.AppendLine($"  Description: {desc.Description}");
                sb.AppendLine($"  Tags: [{string.Join(", ", desc.Tags)}]");
                sb.AppendLine($"  Author: {desc.Author}");
                sb.AppendLine($"  Last Modified: {desc.LastModified}");
                sb.AppendLine($"  Security Level: {desc.SecurityLevel}");
                sb.AppendLine($"  Performance Score: {desc.PerformanceScore:F2}");
                sb.AppendLine($"  Dependencies: [{string.Join(", ", _dependencyGraph.TryGetValue(desc.Name, out var deps) ? deps : new List<string>())}]");
                // [AJOUT] Informations découvertes
                var isExperimental = _experimentalSubsystems.Contains(desc.Name);
                var isDeprecated = _deprecatedSubsystems.Contains(desc.Name);
                sb.AppendLine($"  Experimental: {isExperimental}");
                sb.AppendLine($"  Deprecated: {isDeprecated}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        #endregion

        #region C. Fonctions utilitaires (Mis à jour)

        /// <summary>
        /// Recharge un sous-système (simulé).
        /// Maintenant avec gestion des dépendances.
        /// </summary>
        public static bool ReloadSubsystem(string name, AnimationEngineStub engine)
        {
            var desc = GetSubsystemDescriptor(name);
            if (desc.HasValue)
            {
                var subsystem = GetSubsystem<IAnimationSubsystem>(name);
                if (subsystem != null)
                {
                    // [AJOUT] Gérer les dépendances : recharger les dépendants aussi ?
                    // Pour l'instant, on re-initialise juste ce module
                    try
                    {
                        subsystem.Shutdown();
                        subsystem.Initialize(engine);
                        _healthMap[name] = subsystem.GetHealthStatus(); // Mettre à jour la santé
                        System.Console.WriteLine($"[Index] Reloaded subsystem: {name}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[Index] Failed to reload subsystem {name}. Error: {ex.Message}");
                        _healthMap[name] = SubsystemHealthStatus.Error;
                    }
                }
            }
            return false;
        }

        #endregion

        #region D. Intégration avec le moteur (Mis à jour)

        /// <summary>
        /// Initialise tous les sous-systèmes dans l'ordre calculé.
        /// Maintenant avec gestion des erreurs et dépendances.
        /// </summary>
        public static void InitializeAll(AnimationEngineStub engine)
        {
            foreach (var name in _loadOrder)
            {
                var subsystem = GetSubsystem<IAnimationSubsystem>(name);
                if (subsystem != null)
                {
                    try
                    {
                        // [AJOUT] Vérifier les dépendances avant l'initialisation
                        var deps = GetSubsystemDependencies(name);
                        bool depsOk = true;
                        foreach (var dep in deps)
                        {
                            if (!IsSubsystemHealthy(dep))
                            {
                                System.Console.WriteLine($"[Index] Cannot initialize {name}, dependency {dep} is not healthy.");
                                depsOk = false;
                                break;
                            }
                        }

                        if (depsOk)
                        {
                            subsystem.Initialize(engine);
                            _healthMap[name] = subsystem.GetHealthStatus();
                            System.Console.WriteLine($"[Index] Initialized subsystem: {name}");
                        }
                        else
                        {
                            _healthMap[name] = SubsystemHealthStatus.Error;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[Index] Failed to initialize subsystem: {name}. Error: {ex.Message}");
                        _healthMap[name] = SubsystemHealthStatus.Error;
                    }
                }
            }
        }

        // ... (autres méthodes InitializeAll, ShutdownAll, UpdateAll restent similaires, avec gestion des erreurs) ...

        #endregion

        #region E. Visualisation et debug (Mis à jour)

        // ... (PrintSubsystemIndex, ExportSubsystemIndexToJson restent inchangées) ...

        /// <summary>
        /// Exporte le graphe de dépendances au format DOT (Graphviz).
        /// Mis à jour pour inclure les statuts.
        /// </summary>
        public static void ExportSubsystemGraphToDot(string filePath)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("digraph AnimationEngineSubsystems {");
            foreach (var kvp in _dependencyGraph)
            {
                var name = kvp.Key;
                var health = GetSubsystemHealth(name);
                string color = health switch
                {
                    SubsystemHealthStatus.Healthy => "green",
                    SubsystemHealthStatus.Warning => "orange",
                    SubsystemHealthStatus.Error => "red",
                    _ => "gray"
                };
                sb.AppendLine($"  \"{name}\" [color={color}, style=filled];");
                foreach (var dep in kvp.Value)
                {
                    sb.AppendLine($"  \"{dep}\" -> \"{name}\";");
                }
            }
            sb.AppendLine("}");
            File.WriteAllText(filePath, sb.ToString());
            System.Console.WriteLine($"[Index] Exported dependency graph to {filePath}");
        }

        #endregion

        #region F. Gestion dynamique (Mis à jour)

        // ... (EnableSubsystem, DisableSubsystem, ToggleSubsystem restent inchangées) ...

        #endregion

        #region G. Extensions et compatibilité (Mis à jour)

        /// <summary>
        /// Enregistre un sous-système externe.
        /// Mis à jour pour utiliser la découverte.
        /// </summary>
        public static bool RegisterExternalSubsystem(IExternalSubsystem subsystem)
        {
            // Validation est implicite dans la découverte
            if (_subsystemRegistry.ContainsKey(subsystem.Name)) return false;

            _subsystemRegistry[subsystem.Name] = subsystem;
            var desc = new SubsystemDescriptor(
                subsystem.Name, subsystem.Version, subsystem.GetDependencies(), subsystem.Type, SubsystemHealthStatus.Unknown,
                "External subsystem.", new List<string> { "External" }, "Plugin Author", DateTime.UtcNow, StubFeatureFlags.None, SubsystemSecurityLevel.Medium, StressProfileExtensions.Default, 0.0f);
            _descriptorRegistry[subsystem.Name] = desc;
            _healthMap[subsystem.Name] = SubsystemHealthStatus.Unknown;
            _versionMap[subsystem.Name] = subsystem.Version;
            _dependencyGraph[subsystem.Name] = subsystem.GetDependencies();
            _typeIndex.AddOrUpdate(subsystem.Type, new List<string> { subsystem.Name }, (t, l) => { l.Add(subsystem.Name); return l; });
            _externalSubsystems.Add(subsystem.Name);
            System.Console.WriteLine($"[Index] Registered external subsystem: {subsystem.Name}");
            return true;
        }

        #endregion

        #region H. Sécurité et validation (Mis à jour)

        /// <summary>
        /// Audite les permissions et les états des sous-systèmes.
        /// Mis à jour pour inclure la découverte et les statuts.
        /// </summary>
        public static void AuditSubsystems()
        {
            System.Console.WriteLine("--- Subsystem Audit ---");
            foreach (var name in ListSubsystems())
            {
                var health = GetSubsystemHealth(name);
                var security = GetSubsystemSecurityLevel(name);
                var isExternal = _externalSubsystems.Contains(name);
                var isExperimental = _experimentalSubsystems.Contains(name);
                var isDeprecated = _deprecatedSubsystems.Contains(name);

                if (health == SubsystemHealthStatus.Error)
                {
                    System.Console.WriteLine($"[AUDIT] CRITICAL: Subsystem {name} has ERROR status.");
                }
                else if (health == SubsystemHealthStatus.Warning)
                {
                    System.Console.WriteLine($"[AUDIT] WARNING: Subsystem {name} has WARNING status.");
                }
                if (isExternal && security == SubsystemSecurityLevel.High)
                {
                    System.Console.WriteLine($"[AUDIT] CAUTION: External subsystem {name} has HIGH security level.");
                }
                if (isExperimental)
                {
                    System.Console.WriteLine($"[AUDIT] INFO: Subsystem {name} is marked as EXPERIMENTAL.");
                }
                if (isDeprecated)
                {
                    System.Console.WriteLine($"[AUDIT] INFO: Subsystem {name} is marked as DEPRECATED.");
                }
            }
            System.Console.WriteLine("---------------------");
        }

        #endregion

        #region I. Historique et audit (Mis à jour avec placeholders plus riches)

        public static void RecordSubsystemMetricsSnapshot(AnimationEngineStub engine) { /* Placeholder */ }
        public static void CompareSubsystemSnapshots(string snap1, string snap2) { /* Placeholder */ }
        public static void RollbackSubsystemState(string snapshotId) { /* Placeholder */ }
        public static void ArchiveSubsystemState(string archiveName) { /* Placeholder */ }
        public static void RestoreSubsystemState(string archiveName) { /* Placeholder */ }
        public static void PurgeSubsystemCache() { /* Placeholder */ }

        #endregion

        #region J. Métadonnées et documentation (Mis à jour avec placeholders plus riches)

        public static void GenerateSubsystemDocs(string outputDir) { /* Placeholder */ }
        public static void GenerateSubsystemMarkdown(string outputFile) { /* Placeholder */ }
        public static void GenerateSubsystemHTML(string outputFile) { /* Placeholder */ }
        public static void GenerateSubsystemSummary(TextWriter writer) { /* Placeholder */ }
        public static void GenerateSubsystemChangelog(string outputFile) { /* Placeholder */ }
        public static void GenerateSubsystemDependencyReport(string outputFile) { /* Placeholder */ }
        public static void GenerateSubsystemPerformanceReport(string outputFile) { /* Placeholder */ }
        public static void GenerateSubsystemSecurityReport(string outputFile) { /* Placeholder */ }
        public static void GenerateSubsystemStressReport(string outputFile) { /* Placeholder */ }
        public static void GenerateSubsystemHealthReport(string outputFile) { /* Placeholder */ }

        #endregion

        #region K. Bonus (concepts avancés) - Ajouts

        /// <summary>
        /// Découvre les interfaces implémentées par un sous-système.
        /// </summary>
        public static List<Type> DiscoverInterfaces(string name)
        {
            if (_subsystemRegistry.TryGetValue(name, out var obj))
            {
                return obj.GetType().GetInterfaces().ToList();
            }
            return new List<Type>();
        }

        /// <summary>
        /// Découvre les attributs d'un sous-système.
        /// </summary>
        public static List<Attribute> DiscoverAttributes(string name)
        {
            if (_subsystemRegistry.TryGetValue(name, out var obj))
            {
                return obj.GetType().GetCustomAttributes(true).OfType<Attribute>().ToList();
            }
            return new List<Attribute>();
        }

        /// <summary>
        /// Orchestration dynamique : Broadcast d'événements.
        /// </summary>
        public static void BroadcastEventToSubsystems(string eventName, object eventData = null)
        {
            System.Console.WriteLine($"[Index] Broadcasting event: {eventName}");
            foreach (var name in ListSubsystems())
            {
                // On suppose une interface ou une méthode standardisée pour gérer les événements
                // Ex: if (subsystem is IEventBroadcaster broadcaster) broadcaster.OnEvent(eventName, eventData);
                // Pour l'instant, on ne fait qu'afficher.
            }
        }

        /// <summary>
        /// Orchestration dynamique : Collecte des réponses.
        /// </summary>
        public static Dictionary<string, object> CollectSubsystemResponses(string query)
        {
            var responses = new Dictionary<string, object>();
            System.Console.WriteLine($"[Index] Collecting responses for query: {query}");
            foreach (var name in ListSubsystems())
            {
                // On suppose une interface ou une méthode standardisée pour répondre
                // Ex: if (subsystem is IQueryResponder responder) responses[name] = responder.Query(query);
                // Pour l'instant, on renvoie null.
                responses[name] = null;
            }
            return responses;
        }

        /// <summary>
        /// Synchronise les états entre les sous-systèmes.
        /// </summary>
        public static void SynchronizeSubsystemStates()
        {
            // Exemple : forcer un refresh des caches partagés entre Metrics, Diagnostics, etc.
            System.Console.WriteLine("[Index] Synchronizing subsystem states...");
            // Implémentation dépendante des interactions spécifiques entre modules.
        }

        /// <summary>
        /// Met à jour les sous-systèmes en fonction d'un changement de configuration.
        /// </summary>
        public static void ReloadSubsystemsOnConfigChange(AnimationEngineStub engine, AnimationEngineStubConfig newConfig)
        {
            System.Console.WriteLine("[Index] Reloading subsystems due to config change...");
            // Appliquer les nouveaux flags
            ApplyFeatureFlags(newConfig.FeatureFlags);
            // Réinitialiser les modules concernés
            InitializeAll(engine); // Réinitialisation brute pour l'exemple
        }

        /// <summary>
        /// Interopérabilité : Lie le moteur au RenderGraph.
        /// </summary>
        public static void LinkToRenderGraph(IRenderPipeline pipeline)
        {
            _linkedRenderPipeline = pipeline;
            System.Console.WriteLine($"[Index] Linked to RenderPipeline: {_linkedRenderPipeline?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// Interopérabilité : Lie le moteur à l'Orchestrateur.
        /// </summary>
        public static void LinkToOrchestrator(IOrchestrator orchestrator)
        {
            _linkedOrchestrator = orchestrator;
            System.Console.WriteLine($"[Index] Linked to Orchestrator: {_linkedOrchestrator?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// Interopérabilité : Lie le moteur au système de mouvement.
        /// </summary>
        public static void LinkToMovementSystem(IMovementSystem system)
        {
            _linkedMovementSystem = system;
            System.Console.WriteLine($"[Index] Linked to MovementSystem: {_linkedMovementSystem?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// Interopérabilité : Lie le moteur au moteur audio.
        /// </summary>
        public static void LinkToAudioSystem(IAudioEngine audio)
        {
            _linkedAudioEngine = audio;
            System.Console.WriteLine($"[Index] Linked to AudioEngine: {_linkedAudioEngine?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// Interopérabilité : Lie le moteur au moteur physique.
        /// </summary>
        public static void LinkToPhysicsSystem(IPhysicsEngine physics)
        {
            _linkedPhysicsEngine = physics;
            System.Console.WriteLine($"[Index] Linked to PhysicsEngine: {_linkedPhysicsEngine?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// Configuration dynamique : Applique les flags de fonctionnalités.
        /// </summary>
        public static void ApplyFeatureFlags(StubFeatureFlags flags)
        {
            System.Console.WriteLine($"[Index] Applying feature flags: {flags}");
            // Exemple : activer/désactiver des modules en fonction des flags
            if (flags.HasFlag(StubFeatureFlags.CallLogging))
            {
                // Activer la journalisation dans le stub principal
            }
            if (flags.HasFlag(StubFeatureFlags.Telemetry))
            {
                // Activer la télémétrie dans le stub principal
            }
            // etc.
        }

        /// <summary>
        /// Configuration dynamique : Applique un profil de stress global.
        /// </summary>
        public static void ApplyStressProfile(StressProfile profile)
        {
            System.Console.WriteLine($"[Index] Applying global stress profile: CPU={profile.CpuLoadPercent}%, Mem={profile.MemoryPressureMB}MB, Threads={profile.ThreadingLoadTasks}");
            // On peut envoyer ce profil à un gestionnaire central de stress
            // ou à chaque sous-système capable de le gérer.
        }

        /// <summary>
        /// Configuration dynamique : Applique les politiques de sécurité.
        /// </summary>
        public static void ApplySecurityPolicies(object policies) // Placeholder pour l'objet de politique
        {
            System.Console.WriteLine($"[Index] Applying security policies: {policies?.ToString() ?? "null"}");
            // Implémentation dépendante d'un système de politiques.
        }

        /// <summary>
        /// Configuration dynamique : Applique le niveau de diagnostics.
        /// </summary>
        public static void ApplyDiagnosticsLevel(DiagnosticsLevel level)
        {
            _currentDiagnosticsLevel = level;
            System.Console.WriteLine($"[Index] Applied diagnostics level: {_currentDiagnosticsLevel}");
            // Propager ce niveau au stub principal et aux sous-systèmes.
        }

        /// <summary>
        /// Configuration dynamique : Applique le taux d'échantillonnage des métriques.
        /// </summary>
        public static void ApplyMetricsSamplingRate(float rate)
        {
            _currentMetricsSamplingRate = Math.Max(0.0f, Math.Min(1.0f, rate)); // Clamp 0-1
            System.Console.WriteLine($"[Index] Applied metrics sampling rate: {_currentMetricsSamplingRate:P2}");
            // Utiliser ce taux dans MetricCollector.
        }

        /// <summary>
        /// Configuration dynamique : Applique le mode de threading.
        /// </summary>
        public static void ApplyThreadingMode(ThreadingMode mode)
        {
            _currentThreadingMode = mode;
            System.Console.WriteLine($"[Index] Applied threading mode: {_currentThreadingMode}");
            // Adapter les systèmes internes (Update, JobSystem, etc.) en conséquence.
        }

        /// <summary>
        /// Monitoring : Vérifie l'intégrité de tous les sous-systèmes chargés.
        /// </summary>
        public static void ValidateSubsystemIntegrity()
        {
            System.Console.WriteLine("--- Validating Subsystem Integrity ---");
            foreach (var name in ListSubsystems())
            {
                var isHealthy = ValidateSubsystemIntegrity(name);
                System.Console.WriteLine($"  {name}: {(isHealthy ? "OK" : "FAILED")}");
            }
            System.Console.WriteLine("------------------------------------");
        }

        /// <summary>
        /// Audit : Audite la sécurité de tous les sous-systèmes.
        /// </summary>
        public static void AuditSubsystemSecurity()
        {
            System.Console.WriteLine("--- Auditing Subsystem Security ---");
            foreach (var name in ListSubsystems())
            {
                var level = GetSubsystemSecurityLevel(name);
                var isExternal = _externalSubsystems.Contains(name);
                if (isExternal && level == SubsystemSecurityLevel.High)
                {
                    System.Console.WriteLine($"  [SECURITY AUDIT] CAUTION: External subsystem '{name}' has HIGH security level.");
                }
                else if (isExternal && level == SubsystemSecurityLevel.Low)
                {
                    System.Console.WriteLine($"  [SECURITY AUDIT] INFO: External subsystem '{name}' has LOW security level.");
                }
            }
            System.Console.WriteLine("-----------------------------------");
        }

        /// <summary>
        /// Exporte les métriques de tous les sous-systèmes au format CSV.
        /// </summary>
        public static void ExportSubsystemMetricsToCsv(string filePath)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine("Subsystem,Type,Metric,Value"); // En-tête CSV
            foreach (var name in ListSubsystems())
            {
                var metrics = GetSubsystemMetrics(name);
                foreach (var metric in metrics.Values)
                {
                    writer.WriteLine($"{name},{GetSubsystemDescriptor(name)?.Type},{metric.Key},{metric.Value}");
                }
            }
            System.Console.WriteLine($"[Index] Exported subsystem metrics to {filePath}");
        }

        /// <summary>
        /// Exporte un rapport de santé de tous les sous-systèmes.
        /// </summary>
        public static void ExportSubsystemHealthReport(string filePath)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("--- Subsystem Health Report ---");
            report.AppendLine($"Generated at: {DateTime.UtcNow}");
            report.AppendLine();
            foreach (var name in ListSubsystems())
            {
                var health = GetSubsystemHealth(name);
                var version = GetSubsystemVersion(name);
                var desc = GetSubsystemDescriptor(name);
                report.AppendLine($"Name: {name}");
                report.AppendLine($"  Version: {version}");
                report.AppendLine($"  Type: {desc?.Type}");
                report.AppendLine($"  Health: {health}");
                report.AppendLine($"  Description: {desc?.Description}");
                report.AppendLine();
            }
            File.WriteAllText(filePath, report.ToString());
            System.Console.WriteLine($"[Index] Exported health report to {filePath}");
        }

        #endregion

        #region K. Bonus (concepts avancés) - Placeholders

        // Les méthodes SubsystemAuto... sont des concepts avancés.
        // Elles nécessiteraient des implémentations complexes (ML, introspection profonde, etc.).
        // On les garde en placeholders pour la structure.
        public static void SubsystemAutoDiscovery() { /* Placeholder */ }
        public static void SubsystemAutoReload() { /* Placeholder */ }
        public static void SubsystemAutoRepair() { /* Placeholder */ }
        public static void SubsystemAutoOptimize() { /* Placeholder */ }
        public static void SubsystemAutoProfile() { /* Placeholder */ }
        public static void SubsystemAutoBalance() { /* Placeholder */ }
        public static void SubsystemAutoScale() { /* Placeholder */ }
        public static void SubsystemAutoValidate() { /* Placeholder */ }
        public static void SubsystemAutoDocument() { /* Placeholder */ }
        public static void SubsystemAutoExport() { /* Placeholder */ }

        #endregion
    }
}