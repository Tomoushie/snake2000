// /Engine/Rendering/IRenderPipeline.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Engine.Rendering
{
    /// <summary>
    /// Interface centrale définissant un pipeline de rendu générique.
    /// Un pipeline encapsule la logique de transformation des données de scène brutes
    /// en primitives graphiques visibles, en coordonnant les ressources, les passes et les backends.
    /// </summary>
    public partial interface IRenderPipeline : IDisposable
    {
        /// <summary>
        /// Nom unique du pipeline.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// État actuel du pipeline (Initialisé, En cours d'exécution, En pause, etc.).
        /// </summary>
        RenderPipelineState State { get; }

        /// <summary>
        /// Identifiant unique du pipeline compilé (hash de la configuration et des passes).
        /// </summary>
        RenderPipelineIdentity GetIdentity();

        /// <summary>
        /// Indique si le pipeline est thread-safe pour l'utilisation parallèle.
        /// </summary>
        bool IsThreadSafe { get; }

        /// <summary>
        /// Index de la frame courante. Utile pour le debugging et le déterminisme.
        /// </summary>
        uint FrameIndex { get; }

        /// <summary>
        /// Le device graphique associé à ce pipeline.
        /// </summary>
        IRenderDevice Device { get; }

        /// <summary>
        /// Les capacités et fonctionnalités supportées par ce pipeline sur le device actuel.
        /// </summary>
        RenderPipelineCapabilities Capabilities { get; }

        /// <summary>
        /// Rôle du pipeline (Scène principale, Réflexion, Ombre, UI, etc.).
        /// </summary>
        RenderPipelineRole Role { get; }

        /// <summary>
        /// Métadonnées attachées arbitrairement au pipeline.
        /// </summary>
        IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Collection des passes de rendu du pipeline (lecture seule).
        /// </summary>
        IReadOnlyCollection<IRenderPass> Passes { get; }

        /// <summary>
        /// Topologie du graphe de rendu (DAG des passes).
        /// </summary>
        PipelineTopology GetTopology();

        /// <summary>
        /// Contrat de garanties du pipeline (thread-safe, déterministe, etc.).
        /// </summary>
        RenderPipelineContract Contract { get; }

        /// <summary>
        /// Événement déclenché quand le pipeline est invalidé.
        /// </summary>
        event Action<IRenderPipeline> OnInvalidated;

        /// <summary>
        /// Événement déclenché quand une passe est ajoutée.
        /// </summary>
        event Action<IRenderPipeline, IRenderPass> OnPassAdded;

        /// <summary>
        /// Événement déclenché quand une passe est retirée.
        /// </summary>
        event Action<IRenderPipeline, IRenderPass> OnPassRemoved;

        /// <summary>
        /// Initialise le pipeline avec une configuration spécifique.
        /// Doit être appelé une fois avant Execute.
        /// </summary>
        /// <param name="config">La configuration du pipeline.</param>
        /// <returns>True si l'initialisation a réussi.</returns>
        bool Initialize(IRenderPipelineConfig config);

        /// <summary>
        /// Exécute le pipeline de rendu pour une frame donnée.
        /// Coordonne les passes de rendu, les ressources et le backend graphique.
        /// </summary>
        /// <param name="sceneData">Données de la scène à rendre.</param>
        /// <param name="outputTarget">Cible de rendu finale (écran, texture, etc.).</param>
        void Execute(IRenderScene sceneData, IRenderTarget outputTarget);

        /// <summary>
        /// Effectue une validation interne du pipeline et de ses ressources.
        /// Utile pour le debugging ou la vérification de l'état.
        /// </summary>
        /// <returns>Une liste d'erreurs ou d'avertissements trouvés.</returns>
        IReadOnlyList<RenderValidationError> Validate();

        /// <summary>
        /// Met à jour les paramètres du pipeline sans le réinitialiser entièrement.
        /// </summary>
        /// <param name="newConfig">La nouvelle configuration partielle.</param>
        /// <param name="mode">Le mode d'application (hot reload ou recompilation).</param>
        void ApplyConfiguration(IReadOnlyRenderPipelineConfig newConfig, ConfigApplyMode mode);

        /// <summary>
        /// Définit la liste de commandes de rendu à traiter par ce pipeline.
        /// Permet une séparation entre la génération des commandes et leur exécution.
        /// </summary>
        /// <param name="commandList">La liste de commandes à exécuter.</param>
        void SetRenderCommandList(RenderCommandList commandList);

        /// <summary>
        /// Ajoute une passe de rendu au pipeline. L'ordre d'ajout peut influencer l'ordre d'exécution.
        /// </summary>
        /// <param name="renderPass">La passe à ajouter.</param>
        void AddRenderPass(IRenderPass renderPass);

        /// <summary>
        /// Retire une passe de rendu du pipeline.
        /// </summary>
        /// <param name="renderPassName">Nom de la passe à retirer.</param>
        /// <returns>True si la suppression a réussi.</returns>
        bool RemoveRenderPass(string renderPassName);

        /// <summary>
        /// Récupère une référence à une passe de rendu spécifique par son nom.
        /// </summary>
        /// <typeparam name="TPass">Le type concret de la passe.</typeparam>
        /// <param name="name">Nom de la passe.</param>
        /// <returns>L'instance de la passe, ou null si non trouvée.</returns>
        TPass GetRenderPass<TPass>(string name) where TPass : class, IRenderPass;

        /// <summary>
        /// Tente de récupérer une référence à une passe de rendu spécifique par son nom.
        /// </summary>
        /// <typeparam name="TPass">Le type concret de la passe.</typeparam>
        /// <param name="name">Nom de la passe.</param>
        /// <param name="pass">Variable de sortie pour la passe trouvée.</param>
        /// <returns>True si la passe a été trouvée et assignée.</returns>
        bool TryGetRenderPass<TPass>(string name, out TPass pass) where TPass : class, IRenderPass;

        /// <summary>
        /// Récupère une référence à une passe de rendu spécifique par son tag.
        /// </summary>
        /// <typeparam name="TPass">Le type concret de la passe.</typeparam>
        /// <param name="tag">Tag de la passe.</param>
        /// <returns>L'instance de la passe, ou null si non trouvée.</returns>
        TPass GetRenderPassByTag<TPass>(RenderPassTag tag) where TPass : class, IRenderPass;

        /// <summary>
        /// Trie les passes du pipeline selon un comparateur.
        /// </summary>
        /// <param name="comparer">Le comparateur pour l'ordonnancement.</param>
        void ReorderPasses(IComparer<IRenderPass> comparer);

        /// <summary>
        /// Verrouille la structure du pipeline (ajouts/suppressions de passes interdits).
        /// </summary>
        void Freeze();

        /// <summary>
        /// Déverrouille la structure du pipeline.
        /// </summary>
        void Unfreeze();

        /// <summary>
        /// Informe le pipeline qu'un changement critique (résolution, format de sortie, etc.) a eu lieu.
        /// Le pipeline peut alors invalider ses caches ou redimensionner ses ressources internes.
        /// </summary>
        /// <param name="reason">La raison de l'invalidation.</param>
        void Invalidate(InvalidationReason reason);

        /// <summary>
        /// Réinitialise le pipeline sans le détruire.
        /// </summary>
        void Reset();

        /// <summary>
        /// Force la soumission des commandes en attente.
        /// </summary>
        void Flush();

        /// <summary>
        /// Bloque jusqu'à la complétion totale du GPU (utile au shutdown et aux screenshots).
        /// </summary>
        void WaitIdle();

        /// <summary>
        /// Vérifie la compatibilité du pipeline avec un device donné.
        /// </summary>
        /// <param name="device">Le device à tester.</param>
        /// <returns>True si le pipeline est compatible.</returns>
        bool IsCompatibleWith(IRenderDevice device);

        /// <summary>
        /// Crée un clone du pipeline avec sa configuration actuelle.
        /// </summary>
        /// <param name="target">Le pipeline cible pour le clonage.</param>
        void CloneInto(IRenderPipeline target);

        /// <summary>
        /// Crée un nouveau pipeline dérivé de celui-ci avec des modifications.
        /// </summary>
        /// <param name="name">Nom du nouveau pipeline dérivé.</param>
        /// <param name="mutate">Action pour modifier le builder du pipeline.</param>
        /// <returns>Le nouveau pipeline dérivé.</returns>
        IRenderPipeline DerivePipeline(string name, Action<IRenderPipelineBuilder> mutate);

        /// <summary>
        /// Vérifie si ce pipeline est dérivé d'un autre pipeline parent.
        /// </summary>
        /// <param name="parent">Le pipeline parent potentiel.</param>
        /// <returns>True si ce pipeline est un descendant.</returns>
        bool IsDerivedFrom(IRenderPipeline parent);

        /// <summary>
        /// Capture un instantané du pipeline pour l'inspection et les tests.
        /// </summary>
        /// <returns>Un snapshot du pipeline.</returns>
        RenderPipelineSnapshot CaptureSnapshot();

        /// <summary>
        /// Construit le graphe de rendu interne du pipeline.
        /// </summary>
        /// <param name="builder">Le builder pour déclarer les passes et ressources.</param>
        /// <returns>Le graphe construit.</returns>
        RenderGraph BuildGraph(RenderGraphBuilder builder);

        /// <summary>
        /// Compile le graphe de rendu pour l'optimisation.
        /// </summary>
        void CompileGraph();

        /// <summary>
        /// Exécute une commande de rendu asynchrone.
        /// </summary>
        /// <param name="command">La commande asynchrone à exécuter.</param>
        /// <returns>Un Task représentant l'exécution.</returns>
        Task ExecuteCommandAsync(IRenderCommand command);

        /// <summary>
        /// Commence la journalisation des événements de rendu.
        /// </summary>
        /// <param name="sessionName">Nom de la session de journalisation.</param>
        void BeginEventLogging(string sessionName);

        /// <summary>
        /// Termine la journalisation des événements de rendu.
        /// </summary>
        void EndEventLogging();

        /// <summary>
        /// Active/désactive la journalisation des appels GPU.
        /// </summary>
        /// <param name="enabled">True pour activer.</param>
        void SetCallLoggingEnabled(bool enabled);

        /// <summary>
        /// Récupère les statistiques de performance du pipeline.
        /// </summary>
        /// <returns>Les statistiques de performance.</returns>
        RenderPipelineStats GetPerformanceStats();

        /// <summary>
        /// Démarre une session de profilage GPU.
        /// </summary>
        void BeginGpuProfiling();

        /// <summary>
        /// Termine une session de profilage GPU.
        /// </summary>
        void EndGpuProfiling();

        /// <summary>
        /// Exécute un test de déterminisme.
        /// </summary>
        /// <param name="testScene">La scène de test.</param>
        /// <returns>True si le résultat est déterministe.</returns>
        bool RunDeterminismTest(IRenderScene testScene);

        /// <summary>
        /// Enregistre une extension de pipeline.
        /// </summary>
        /// <param name="extension">L'extension à enregistrer.</param>
        void RegisterExtension(IRenderPipelineExtension extension);

        /// <summary>
        /// Récupère une extension de pipeline par son type.
        /// </summary>
        /// <typeparam name="T">Le type de l'extension.</typeparam>
        /// <returns>L'extension, ou null si non trouvée.</returns>
        T GetExtension<T>() where T : class, IRenderPipelineExtension;
    }

    #region Enums & Structs

    /// <summary>
    /// États possibles du pipeline de rendu.
    /// </summary>
    public enum RenderPipelineState
    {
        Uninitialized,
        Initialized,
        Running,
        Paused,
        Invalidated,
        Disposed
    }

    /// <summary>
    /// Rôles possibles du pipeline de rendu.
    /// </summary>
    public enum RenderPipelineRole
    {
        MainScene,
        ReflectionProbe,
        ShadowMap,
        UI,
        PostProcess,
        Debug,
        Cinematic
    }

    /// <summary>
    /// Raisons possibles d'invalidation du pipeline.
    /// </summary>
    public enum InvalidationReason
    {
        DeviceLost,
        ResolutionChanged,
        FormatChanged,
        BackendChanged,
        ConfigurationChanged,
        ResourceOutOfMemory
    }

    /// <summary>
    /// Modes d'application de la configuration.
    /// </summary>
    public enum ConfigApplyMode
    {
        HotReload,      // Changements appliqués immédiatement
        ColdReload,     // Requiert une recompilation du pipeline
        ValidateOnly    // Ne fait que valider la configuration
    }

    /// <summary>
    /// Tags pour identifier les passes de rendu.
    /// </summary>
    public enum RenderPassTag
    {
        Geometry,
        Lighting,
        Shadows,
        PostProcess,
        UI,
        Debug
    }

    /// <summary>
    /// Identité unique du pipeline (hash de sa configuration).
    /// </summary>
    public readonly struct RenderPipelineIdentity
    {
        public readonly ulong Hash;
        public RenderPipelineIdentity(ulong hash) => Hash = hash;
        public override string ToString() => Hash.ToString("X");
    }

    /// <summary>
    /// Topologie du graphe de rendu (DAG).
    /// </summary>
    public interface PipelineTopology
    {
        int NodeCount { get; }
        int EdgeCount { get; }
        // Autres propriétés pour la visualisation et l'analyse...
    }

    /// <summary>
    /// Contrat de garanties du pipeline.
    /// </summary>
    public interface RenderPipelineContract
    {
        bool IsThreadSafe { get; }
        bool IsDeterministic { get; }
        bool GuaranteesConsistency { get; }
        // Autres garanties...
    }

    /// <summary>
    /// Statistiques de performance du pipeline.
    /// </summary>
    public interface RenderPipelineStats
    {
        float FrameTimeMs { get; }
        int DrawCalls { get; }
        int TrianglesRendered { get; }
        // Autres métriques...
    }

    #endregion

    #region Interfaces Associées (à implémenter dans d'autres fichiers)

    // Exemples de types qui devront être définis ailleurs
    public interface IRenderPipelineConfig { }
    public interface IReadOnlyRenderPipelineConfig { }
    public interface IRenderDevice
    {
        string DeviceName { get; }
        string DriverVersion { get; }
        bool IsLost { get; }
        bool TryRecover();
        void WaitForIdle();
    }

    public interface IRenderScene { }
    public interface IRenderTarget { }
    public interface IRenderPass { }
    public interface RenderGraph { }
    public interface RenderGraphBuilder { }
    public interface IRenderCommand { }
    public interface RenderCommandList { }
    public interface IRenderPipelineExtension { }
    public interface RenderPipelineCapabilities { }

    public class RenderValidationError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public SeverityLevel Severity { get; set; }
    }

    public enum SeverityLevel
    {
        Warning,
        Error
    }

    #endregion
}