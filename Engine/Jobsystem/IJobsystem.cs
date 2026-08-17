using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Core
{
    /// <summary>
    /// Contrat du systeme de jobs, reduit a ce que le depot appelle reellement.
    /// </summary>
    /// <remarks>
    /// La declaration d'origine — 3 121 lignes, 408 methodes — est archivee dans
    /// Docs/Intention/IJobSystem.cs.txt. La mesure des appelants donnait trois
    /// methodes appelees sur 408, toutes depuis ThreadAffinityManager.cs, et
    /// SetJobAffinityHints — pourtant appelee deux fois — n'y figurait pas.
    /// Ne pas reintroduire l'archive ici : le jour ou un membre est reellement
    /// appele, il rejoint ce contrat un par un.
    /// </remarks>
    public interface IJobSystem
    {
        /// <summary>
        /// Nombre de threads workers.
        /// Appelee par ThreadAffinityManager.GetWorkerThreadCount (ligne 1107).
        /// </summary>
        int GetWorkerThreadCount();

        /// <summary>
        /// Suspend un thread worker temporairement.
        /// Appelee par ThreadAffinityManager aux lignes 714, 738 et 762.
        /// </summary>
        void SuspendWorkerThread(int threadIndex);

        /// <summary>
        /// Applique des indices d'affinite a un job.
        /// Appelee par ThreadAffinityManager aux lignes 476 et 508 ; le retour est
        /// ignore aux deux sites, void suffit donc au contrat mesure.
        /// </summary>
        void SetJobAffinityHints(JobHandle jobHandle, AffinityHints hints);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 📦 INTERFACES DE JOBS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Interface de base pour tout job exécutable par le système.
    /// </summary>
    public interface IJob
    {
        /// <summary>
        /// Exécute le job.
        /// </summary>
        void Execute();

        /// <summary>
        /// Obtient le nom du job pour le debug et le profiling.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Obtient la catégorie du job.
        /// </summary>
        JobCategory Category { get; }
    }

    /// <summary>
    /// Interface pour les jobs de type ParallelFor.
    /// </summary>
    public interface IParallelForJob
    {
        /// <summary>
        /// Exécute une itération du ParallelFor.
        /// </summary>
        /// <param name="index">Index de l'itération</param>
        void Execute(int index);

        /// <summary>
        /// Obtient le nom du job.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Obtient la catégorie du job.
        /// </summary>
        JobCategory Category { get; }
    }

    /// <summary>
    /// Interface pour les jobs de réduction (map-reduce).
    /// </summary>
    public interface IReduceJob
    {
        /// <summary>
        /// Exécute une étape de réduction.
        /// </summary>
        /// <param name="index">Index de l'élément</param>
        /// <param name="accumulator">Accumulateur courant</param>
        void Execute(int index, ref object accumulator);

        /// <summary>
        /// Fusionne deux résultats partiels.
        /// </summary>
        /// <param name="left">Résultat gauche</param>
        /// <param name="right">Résultat droit</param>
        /// <returns>Résultat fusionné</returns>
        object Combine(object left, object right);

        /// <summary>
        /// Obtient le nom du job.
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Interface pour les fabriques de jobs personnalisés.
    /// </summary>
    public interface IJobFactory
    {
        /// <summary>
        /// Crée une instance de job.
        /// </summary>
        /// <returns>Nouvelle instance de job</returns>
        IJob CreateJob();

        /// <summary>
        /// Obtient le type de job produit.
        /// </summary>
        string JobTypeName { get; }
    }

    /// <summary>
    /// Interface pour les extensions du système de jobs.
    /// </summary>
    public interface IJobSystemExtension
    {
        /// <summary>
        /// Initialise l'extension.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Met à jour l'extension.
        /// </summary>
        /// <param name="deltaTime">Temps delta</param>
        void Update(float deltaTime);

        /// <summary>
        /// Arrête l'extension.
        /// </summary>
        void Shutdown();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 📦 STRUCTURES & ENUMS FONDAMENTAUX
    // ═══════════════════════════════════════════════════════════════════

    #region Enums

    /// <summary>
    /// Statut d'un job dans son cycle de vie.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>Le job n'a pas encore été planifié.</summary>
        NotScheduled,
        /// <summary>Le job est en file d'attente.</summary>
        Queued,
        /// <summary>Le job est en cours d'exécution.</summary>
        Running,
        /// <summary>Le job est terminé avec succès.</summary>
        Completed,
        /// <summary>Le job a échoué.</summary>
        Failed,
        /// <summary>Le job a été annulé.</summary>
        Cancelled,
        /// <summary>Le job est en cours de récupération après échec.</summary>
        Recovering,
        /// <summary>Le job attend ses dépendances.</summary>
        WaitingForDependencies,
        /// <summary>Le job est suspendu.</summary>
        Suspended
    }

    /// <summary>
    /// Priorité d'exécution d'un job.
    /// </summary>
    public enum JobPriority
    {
        /// <summary>Priorité critique : exécution immédiate.</summary>
        Critical = 0,
        /// <summary>Priorité haute.</summary>
        High = 1,
        /// <summary>Priorité normale (défaut).</summary>
        Normal = 2,
        /// <summary>Priorité basse.</summary>
        Low = 3,
        /// <summary>Priorité d'arrière-plan.</summary>
        Background = 4
    }

    /// <summary>
    /// Catégories de jobs pour l'organisation et l'affinité.
    /// </summary>
    public enum JobCategory
    {
        /// <summary>Jobs d'animation (blendspace, IK, skinning).</summary>
        Animation,
        /// <summary>Jobs de physique (collisions, raycasts).</summary>
        Physics,
        /// <summary>Jobs d'IA (pathfinding, behavior trees).</summary>
        AI,
        /// <summary>Jobs de rendu (culling, draw call prep).</summary>
        Rendering,
        /// <summary>Jobs audio (mixage, DSP).</summary>
        Audio,
        /// <summary>Jobs de mouvement (locomotion, steering).</summary>
        Movement,
        /// <summary>Jobs de chargement de ressources.</summary>
        ResourceLoading,
        /// <summary>Jobs de streaming.</summary>
        Streaming,
        /// <summary>Jobs réseau.</summary>
        Networking,
        /// <summary>Jobs de sauvegarde.</summary>
        SaveSystem,
        /// <summary>Jobs GPU compute.</summary>
        GPUCompute,
        /// <summary>Jobs de particules.</summary>
        Particles,
        /// <summary>Jobs d'UI.</summary>
        UI,
        /// <summary>Jobs génériques.</summary>
        General,
        /// <summary>Jobs de profiling.</summary>
        Profiling,
        /// <summary>Jobs de debug.</summary>
        Debug
    }

    /// <summary>
    /// État global du système de jobs.
    /// </summary>
    public enum JobSystemState
    {
        /// <summary>Non initialisé.</summary>
        Uninitialized,
        /// <summary>En cours d'initialisation.</summary>
        Initializing,
        /// <summary>Prêt à accepter des jobs.</summary>
        Ready,
        /// <summary>En cours d'exécution.</summary>
        Running,
        /// <summary>En pause.</summary>
        Paused,
        /// <summary>En cours d'arrêt.</summary>
        ShuttingDown,
        /// <summary>Arrêté.</summary>
        Shutdown,
        /// <summary>En erreur.</summary>
        Error
    }

    /// <summary>
    /// Niveau de gravité des erreurs de jobs.
    /// </summary>
    public enum JobErrorLevel
    {
        /// <summary>Information.</summary>
        Info,
        /// <summary>Avertissement.</summary>
        Warning,
        /// <summary>Erreur.</summary>
        Error,
        /// <summary>Erreur critique.</summary>
        Critical
    }

    /// <summary>
    /// Stratégie de récupération après échec d'un job.
    /// </summary>
    public enum RecoveryStrategy
    {
        /// <summary>Réessayer le job.</summary>
        Retry,
        /// <summary>Réessayer avec un backoff exponentiel.</summary>
        RetryWithBackoff,
        /// <summary>Exécuter sur le thread principal.</summary>
        FallbackToMainThread,
        /// <summary>Ignorer le job et continuer.</summary>
        Skip,
        /// <summary>Annuler le job et ses dépendants.</summary>
        CancelAndPropagate,
        /// <summary>Dégrader la qualité (LOD, résolution réduite).</summary>
        DegradeQuality
    }

    /// <summary>
    /// Types de visualisation de debug.
    /// </summary>
    public enum JobDebugVisualizationType
    {
        /// <summary>Timeline des jobs.</summary>
        Timeline,
        /// <summary>Graphe des dépendances.</summary>
        DependencyGraph,
        /// <summary>Heatmap de charge des threads.</summary>
        ThreadHeatmap,
        /// <summary>File d'attente par priorité.</summary>
        PriorityQueues,
        /// <summary>Work-stealing visualization.</summary>
        WorkStealing,
        /// <summary>Budget CPU par catégorie.</summary>
        BudgetUsage,
        /// <summary>Mémoire par catégorie.</summary>
        MemoryUsage,
        /// <summary>Erreurs récentes.</summary>
        RecentErrors,
        /// <summary>Performance trend.</summary>
        PerformanceTrend,
        /// <summary>Deadlock detection.</summary>
        DeadlockDetection
    }

    /// <summary>
    /// Types de sous-systèmes du job system.
    /// </summary>
    public enum JobSubsystemType
    {
        /// <summary>Scheduler principal.</summary>
        Scheduler,
        /// <summary>Pool de threads.</summary>
        ThreadPool,
        /// <summary>Gestionnaire de dépendances.</summary>
        DependencyManager,
        /// <summary>Gestionnaire de cache.</summary>
        CacheManager,
        /// <summary>Gestionnaire de budget.</summary>
        BudgetManager,
        /// <summary>Profiler.</summary>
        Profiler,
        /// <summary>Système de récupération.</summary>
        RecoverySystem,
        /// <summary>Système de debug.</summary>
        DebugSystem,
        /// <summary>Work stealer.</summary>
        WorkStealer
    }

    /// <summary>
    /// Niveau de priorité des threads.
    /// </summary>
    public enum ThreadPriorityLevel
    {
        /// <summary>Priorité la plus basse.</summary>
        Lowest,
        /// <summary>Priorité basse.</summary>
        BelowNormal,
        /// <summary>Priorité normale.</summary>
        Normal,
        /// <summary>Priorité haute.</summary>
        AboveNormal,
        /// <summary>Priorité la plus haute.</summary>
        Highest,
        /// <summary>Priorité temps réel.</summary>
        RealTime
    }

    /// <summary>
    /// Direction de tendance des performances.
    /// </summary>
    public enum JobTrendDirection
    {
        /// <summary>Performance en amélioration.</summary>
        Improving,
        /// <summary>Performance stable.</summary>
        Stable,
        /// <summary>Performance en dégradation.</summary>
        Declining
    }

    #endregion

    #region Structures fondamentales

    /// <summary>
    /// Handle unique identifiant un job planifié.
    /// </summary>
    public struct JobHandle : IEquatable<JobHandle>
    {
        /// <summary>Identifiant unique du job.</summary>
        public int Id;

        /// <summary>Numéro de séquence pour éviter les collisions.</summary>
        public int SequenceNumber;

        /// <summary>Handle invalide.</summary>
        public static readonly JobHandle Invalid = new JobHandle { Id = -1, SequenceNumber = 0 };

        /// <summary>Vérifie si le handle est valide.</summary>
        public bool IsValid => Id >= 0;

        public bool Equals(JobHandle other) => Id == other.Id && SequenceNumber == other.SequenceNumber;
        public override bool Equals(object obj) => obj is JobHandle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, SequenceNumber);
        public static bool operator ==(JobHandle left, JobHandle right) => left.Equals(right);
        public static bool operator !=(JobHandle left, JobHandle right) => !left.Equals(right);
    }

    /// <summary>
    /// Résultat de l'exécution d'un job.
    /// </summary>
    public struct JobResult
    {
        /// <summary>True si le job a réussi.</summary>
        public bool Success;

        /// <summary>Message d'erreur si échec.</summary>
        public string ErrorMessage;

        /// <summary>Exception si échec.</summary>
        public Exception Exception;

        /// <summary>Données de sortie du job (optionnel).</summary>
        public object OutputData;

        /// <summary>Temps d'exécution du job.</summary>
        public TimeSpan ExecutionTime;

        /// <summary>Statut final du job.</summary>
        public JobStatus FinalStatus;
    }

    /// <summary>
    /// Fence de synchronisation pour un job.
    /// </summary>
    public struct JobFence
    {
        /// <summary>Handle du job surveillé.</summary>
        public JobHandle Handle;

        /// <summary>True si la fence est signalée (job terminé).</summary>
        public bool IsSignaled;

        /// <summary>Attend que la fence soit signalée.</summary>
        public void Wait() { }

        /// <summary>Attend que la fence soit signalée avec un timeout.</summary>
        public bool Wait(TimeSpan timeout) { return false; }
    }

    /// <summary>
    /// Configuration du système de jobs.
    /// </summary>
    public class JobSystemConfig
    {
        /// <summary>Nombre de threads workers (0 = auto).</summary>
        public int WorkerThreadCount { get; set; } = 0;

        /// <summary>Nombre maximum de threads workers.</summary>
        public int MaxWorkerThreadCount { get; set; } = Environment.ProcessorCount;

        /// <summary>Taille maximale de la file d'attente par thread.</summary>
        public int MaxQueueSizePerThread { get; set; } = 1024;

        /// <summary>Active le work-stealing.</summary>
        public bool EnableWorkStealing { get; set; } = true;

        /// <summary>Seuil de batch pour le ParallelFor.</summary>
        public int DefaultBatchSize { get; set; } = 64;

        /// <summary>Budget CPU par frame en millisecondes.</summary>
        public float FrameBudgetMs { get; set; } = 4.0f;

        /// <summary>Active le mode de trace détaillée.</summary>
        public bool EnableTracing { get; set; } = false;

        /// <summary>Nombre maximum de retries par job.</summary>
        public int MaxRetriesPerJob { get; set; } = 3;

        /// <summary>Durée du backoff entre retries en millisecondes.</summary>
        public float RetryBackoffMs { get; set; } = 1.0f;

        /// <summary>Active le mode d'économie d'énergie.</summary>
        public bool EnablePowerSaving { get; set; } = false;

        /// <summary>Taille du cache de résultats.</summary>
        public int CacheSize { get; set; } = 4096;

        /// <summary>Durée d'expiration du cache.</summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Affinité des threads (null = OS décide).</summary>
        public ThreadAffinityConfig ThreadAffinity { get; set; } = null;

        /// <summary>Budgets par catégorie de jobs.</summary>
        public Dictionary<JobCategory, float> CategoryBudgets { get; set; } = new();

        /// <summary>Priorités par catégorie de jobs.</summary>
        public Dictionary<JobCategory, JobPriority> CategoryPriorities { get; set; } = new();
    }

    /// <summary>
    /// Configuration d'affinité des threads.
    /// </summary>
    public class ThreadAffinityConfig
    {
        /// <summary>Masque d'affinité CPU (bitmask des cœurs).</summary>
        public long CpuAffinityMask { get; set; } = -1; // -1 = tous les cœurs

        /// <summary>Threads réservés au système d'exploitation.</summary>
        public int ReservedCoreCount { get; set; } = 1;

        /// <summary>Affectation explicite thread → cœur.</summary>
        public Dictionary<int, int> ThreadToCoreMap { get; set; } = new();

        /// <summary>Active l'hyperthreading.</summary>
        public bool EnableHyperthreading { get; set; } = true;

        /// <summary>Mode d'affinité.</summary>
        public ThreadAffinityMode Mode { get; set; } = ThreadAffinityMode.Auto;
    }

    /// <summary>
    /// Mode d'affinité des threads.
    /// </summary>
    public enum ThreadAffinityMode
    {
        /// <summary>L'OS gère l'affinité.</summary>
        Auto,
        /// <summary>Affinité manuelle par bitmask.</summary>
        Manual,
        /// <summary>Affinité par catégorie de jobs.</summary>
        CategoryBased,
        /// <summary>Affinité dynamique adaptative.</summary>
        Adaptive
    }

    #endregion

    #region Structures de reporting

    /// <summary>
    /// Rapport de charge des threads.
    /// </summary>
    public struct ThreadLoadReport
    {
        /// <summary>Charge par thread (index → pourcentage 0-100).</summary>
        public Dictionary<int, float> LoadPercentages;

        /// <summary>Nombre de jobs actifs par thread.</summary>
        public Dictionary<int, int> ActiveJobsPerThread;

        /// <summary>Nombre de jobs en attente par thread.</summary>
        public Dictionary<int, int> QueuedJobsPerThread;

        /// <summary>Charge moyenne globale.</summary>
        public float AverageLoad;

        /// <summary>Thread le plus chargé.</summary>
        public int MostLoadedThread;

        /// <summary>Thread le moins chargé.</summary>
        public int LeastLoadedThread;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Statistiques du pool de threads.
    /// </summary>
    public struct ThreadPoolStats
    {
        /// <summary>Nombre total de threads.</summary>
        public int TotalThreads;

        /// <summary>Nombre de threads actifs.</summary>
        public int ActiveThreads;

        /// <summary>Nombre de threads inactifs.</summary>
        public int IdleThreads;

        /// <summary>Nombre de threads suspendus.</summary>
        public int SuspendedThreads;

        /// <summary>Nombre total de jobs complétés.</summary>
        public long TotalJobsCompleted;

        /// <summary>Nombre total de jobs échoués.</summary>
        public long TotalJobsFailed;

        /// <summary>Temps moyen d'exécution d'un job.</summary>
        public float AverageJobExecutionTimeMs;

        /// <summary>Temps de fonctionnement du pool.</summary>
        public TimeSpan Uptime;
    }

    /// <summary>
    /// Contexte d'un thread worker.
    /// </summary>
    public struct WorkerThreadContext
    {
        /// <summary>Index du thread.</summary>
        public int ThreadIndex;

        /// <summary>ID natif du thread.</summary>
        public int NativeThreadId;

        /// <summary>Nom du thread.</summary>
        public string Name;

        /// <summary>Priorité du thread.</summary>
        public ThreadPriorityLevel Priority;

        /// <summary>État actuel du thread.</summary>
        public WorkerThreadState State;

        /// <summary>Nombre de jobs exécutés par ce thread.</summary>
        public long JobsExecuted;

        /// <summary>Nombre de jobs volés par ce thread.</summary>
        public long JobsStolen;

        /// <summary>Cœur CPU sur lequel le thread est affecté (-1 = non affecté).</summary>
        public int AffinityCore;

        /// <summary>Charge actuelle du thread (0-100).</summary>
        public float CurrentLoad;
    }

    /// <summary>
    /// État d'un thread worker.
    /// </summary>
    public enum WorkerThreadState
    {
        /// <summary>En cours d'initialisation.</summary>
        Initializing,
        /// <summary>Inactif, en attente de jobs.</summary>
        Idle,
        /// <summary>En train d'exécuter un job.</summary>
        Busy,
        /// <summary>En train de voler un job.</summary>
        Stealing,
        /// <summary>Suspendu.</summary>
        Suspended,
        /// <summary>En cours d'arrêt.</summary>
        ShuttingDown,
        /// <summary>Arrêté.</summary>
        Stopped,
        /// <summary>En erreur.</summary>
        Error
    }

    /// <summary>
    /// Statistiques de work-stealing.
    /// </summary>
    public struct WorkStealingStats
    {
        /// <summary>Nombre total de tentatives de vol.</summary>
        public long TotalStealAttempts;

        /// <summary>Nombre de vols réussis.</summary>
        public long SuccessfulSteals;

        /// <summary>Taux de réussite des vols (0.0 à 1.0).</summary>
        public float StealSuccessRate;

        /// <summary>Temps moyen d'un vol en microsecondes.</summary>
        public float AverageStealTimeUs;

        /// <summary>Nombre de vols par thread.</summary>
        public Dictionary<int, long> StealsPerThread;
    }

    /// <summary>
    /// Rapport de débit des jobs.
    /// </summary>
    public struct JobThroughputReport
    {
        /// <summary>Jobs par seconde (moyenne glissante).</summary>
        public float JobsPerSecond;

        /// <summary>Jobs par seconde (pic).</summary>
        public float PeakJobsPerSecond;

        /// <summary>Nombre total de jobs traités.</summary>
        public long TotalJobsProcessed;

        /// <summary>Débit par catégorie.</summary>
        public Dictionary<JobCategory, float> ThroughputByCategory;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Rapport d'échec des jobs.
    /// </summary>
    public struct JobFailureReport
    {
        /// <summary>Taux d'échec (0.0 à 1.0).</summary>
        public float FailureRate;

        /// <summary>Nombre total d'échecs.</summary>
        public int TotalFailures;

        /// <summary>Échecs par catégorie.</summary>
        public Dictionary<JobCategory, int> FailuresByCategory;

        /// <summary>Échecs par type d'erreur.</summary>
        public Dictionary<string, int> FailuresByErrorType;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Rapport de retry des jobs.
    /// </summary>
    public struct JobRetryReport
    {
        /// <summary>Nombre moyen de retries.</summary>
        public float AverageRetries;

        /// <summary>Nombre maximum de retries.</summary>
        public int MaxRetries;

        /// <summary>Nombre total de retries.</summary>
        public long TotalRetries;

        /// <summary>Taux de succès après retry (0.0 à 1.0).</summary>
        public float RetrySuccessRate;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Graphe de dépendances des jobs.
    /// </summary>
    public struct JobDependencyGraph
    {
        /// <summary>Adjacence : job → liste des jobs qui en dépendent.</summary>
        public Dictionary<JobHandle, List<JobHandle>> Adjacency;

        /// <summary>Ordre d'exécution topologique.</summary>
        public List<JobHandle> ExecutionOrder;

        /// <summary>True si le graphe contient un cycle.</summary>
        public bool HasCycle;

        /// <summary>Nombre de nœuds dans le graphe.</summary>
        public int NodeCount;

        /// <summary>Nombre d'arêtes dans le graphe.</summary>
        public int EdgeCount;
    }

    /// <summary>
    /// Rapport de validation des dépendances.
    /// </summary>
    public struct DependencyValidationReport
    {
        /// <summary>True si les dépendances sont valides.</summary>
        public bool IsValid;

        /// <summary>Liste des problèmes détectés.</summary>
        public List<string> Issues;

        /// <summary>Dépendances circulaires détectées.</summary>
        public List<List<JobHandle>> CircularDependencies;

        /// <summary>Jobs orphelins (dépendances manquantes).</summary>
        public List<JobHandle> OrphanedJobs;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Rapport de détection de deadlocks.
    /// </summary>
    public struct DeadlockDetectionReport
    {
        /// <summary>True si un deadlock est détecté.</summary>
        public bool HasDeadlock;

        /// <summary>Cycles de deadlock détectés.</summary>
        public List<List<JobHandle>> DeadlockCycles;

        /// <summary>Jobs impliqués dans les deadlocks.</summary>
        public List<JobHandle> InvolvedJobs;

        /// <summary>Recommandations de résolution.</summary>
        public List<string> Recommendations;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Rapport de conflits de threads.
    /// </summary>
    public struct ThreadConflictReport
    {
        /// <summary>True si des conflits sont détectés.</summary>
        public bool HasConflicts;

        /// <summary>Liste des conflits.</summary>
        public List<ThreadConflict> Conflicts;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Description d'un conflit de threads.
    /// </summary>
    public struct ThreadConflict
    {
        /// <summary>Premier thread impliqué.</summary>
        public int ThreadA;

        /// <summary>Deuxième thread impliqué.</summary>
        public int ThreadB;

        /// <summary>Ressource en conflit.</summary>
        public string ResourceName;

        /// <summary>Type de conflit.</summary>
        public ThreadConflictType Type;

        /// <summary>Description du conflit.</summary>
        public string Description;
    }

    /// <summary>
    /// Type de conflit de threads.
    /// </summary>
    public enum ThreadConflictType
    {
        /// <summary>Accès concurrent en écriture.</summary>
        WriteWrite,
        /// <summary>Accès concurrent lecture/écriture.</summary>
        ReadWrite,
        /// <summary>Verrou mortel.</summary>
        Deadlock,
        /// <summary>Famine de thread.</summary>
        Starvation
    }

    /// <summary>
    /// Résultat de validation d'un job.
    /// </summary>
    public struct JobValidationResult
    {
        /// <summary>True si le job est valide.</summary>
        public bool IsValid;

        /// <summary>Liste des erreurs de validation.</summary>
        public List<string> Errors;

        /// <summary>Liste des avertissements.</summary>
        public List<string> Warnings;
    }

    /// <summary>
    /// Rapport d'erreurs du système de jobs.
    /// </summary>
    public struct JobErrorReport
    {
        /// <summary>Liste des erreurs.</summary>
        public List<JobErrorEntry> Errors;

        /// <summary>Nombre total d'erreurs.</summary>
        public int ErrorCount;

        /// <summary>Erreurs par niveau.</summary>
        public Dictionary<JobErrorLevel, int> ErrorsByLevel;

        /// <summary>Erreurs par catégorie.</summary>
        public Dictionary<JobCategory, int> ErrorsByCategory;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Entrée d'erreur de job.
    /// </summary>
    public struct JobErrorEntry
    {
        /// <summary>Timestamp de l'erreur.</summary>
        public DateTime Timestamp;

        /// <summary>Niveau de gravité.</summary>
        public JobErrorLevel Level;

        /// <summary>Message d'erreur.</summary>
        public string Message;

        /// <summary>Exception associée.</summary>
        public Exception Exception;

        /// <summary>Stack trace.</summary>
        public string StackTrace;

        /// <summary>Handle du job concerné.</summary>
        public JobHandle JobHandle;

        /// <summary>Catégorie du job.</summary>
        public JobCategory Category;
    }

    /// <summary>
    /// Détail d'erreur d'un job spécifique.
    /// </summary>
    public struct JobErrorDetail
    {
        /// <summary>Handle du job.</summary>
        public JobHandle Handle;

        /// <summary>Liste des erreurs de ce job.</summary>
        public List<JobErrorEntry> Errors;

        /// <summary>Nombre de retries effectués.</summary>
        public int RetryCount;

        /// <summary>Stratégie de récupération appliquée.</summary>
        public RecoveryStrategy AppliedStrategy;

        /// <summary>True si le job a été récupéré.</summary>
        public bool IsRecovered;
    }

    /// <summary>
    /// Rapport de sévérité des erreurs par catégorie.
    /// </summary>
    public struct JobErrorSeverityReport
    {
        /// <summary>Erreurs par catégorie et niveau.</summary>
        public Dictionary<JobCategory, Dictionary<JobErrorLevel, int>> SeverityByCategory;

        /// <summary>Catégorie avec le plus d'erreurs critiques.</summary>
        public JobCategory WorstCategory;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Rapport de sécurité thread-safe.
    /// </summary>
    public struct ThreadSafetyReport
    {
        /// <summary>True si le job est thread-safe.</summary>
        public bool IsThreadSafe;

        /// <summary>Opérations non thread-safe détectées.</summary>
        public List<string> UnsafeOperations;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Rapport des verrous du système de jobs.
    /// </summary>
    public struct JobLockStatusReport
    {
        /// <summary>Verrous actifs.</summary>
        public List<JobLockInfo> ActiveLocks;

        /// <summary>Verrous en contention.</summary>
        public List<JobLockInfo> ContendedLocks;

        /// <summary>Temps moyen d'attente par verrou.</summary>
        public Dictionary<string, float> AverageWaitTimeByLock;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Information sur un verrou.
    /// </summary>
    public struct JobLockInfo
    {
        /// <summary>Nom du verrou.</summary>
        public string Name;

        /// <summary>Thread propriétaire.</summary>
        public int OwnerThread;

        /// <summary>Nombre de threads en attente.</summary>
        public int WaitingThreads;

        /// <summary>Durée de possession en millisecondes.</summary>
        public float HoldTimeMs;
    }

    /// <summary>
    /// État de récupération du système de jobs.
    /// </summary>
    public struct JobRecoveryStatus
    {
        /// <summary>Nombre de récupérations en cours.</summary>
        public int ActiveRecoveries;

        /// <summary>Nombre de récupérations réussies.</summary>
        public long SuccessfulRecoveries;

        /// <summary>Nombre de récupérations échouées.</summary>
        public long FailedRecoveries;

        /// <summary>Taux de réussite des récupérations.</summary>
        public float RecoverySuccessRate;
    }

    /// <summary>
    /// Données de performance d'une catégorie de jobs.
    /// </summary>
    public struct JobCategoryPerformanceData
    {
        /// <summary>Catégorie.</summary>
        public JobCategory Category;

        /// <summary>Temps CPU moyen en ms.</summary>
        public float AverageCPUTimeMs;

        /// <summary>Temps CPU maximum en ms.</summary>
        public float MaxCPUTimeMs;

        /// <summary>Nombre de jobs exécutés.</summary>
        public long JobsExecuted;

        /// <summary>Nombre de jobs échoués.</summary>
        public long JobsFailed;

        /// <summary>Utilisation mémoire en octets.</summary>
        public long MemoryUsageBytes;

        /// <summary>Timestamp.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Distribution des priorités des jobs.
    /// </summary>
    public struct JobPriorityDistribution
    {
        /// <summary>Nombre de jobs par priorité.</summary>
        public Dictionary<JobPriority, int> CountByPriority;

        /// <summary>Priorité dominante.</summary>
        public JobPriority DominantPriority;

        /// <summary>Timestamp.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Rapport de timeline des jobs.
    /// </summary>
    public struct JobTimelineReport
    {
        /// <summary>Événements de la timeline.</summary>
        public List<JobTimelineEvent> Events;

        /// <summary>Début de la timeline.</summary>
        public DateTime StartTime;

        /// <summary>Fin de la timeline.</summary>
        public DateTime EndTime;

        /// <summary>Durée totale en ms.</summary>
        public float TotalDurationMs;

        /// <summary>Nombre de frames couvertes.</summary>
        public int FrameCount;
    }

    /// <summary>
    /// Événement de timeline d'un job.
    /// </summary>
    public struct JobTimelineEvent
    {
        /// <summary>Nom du job.</summary>
        public string JobName;

        /// <summary>Handle du job.</summary>
        public JobHandle Handle;

        /// <summary>Catégorie du job.</summary>
        public JobCategory Category;

        /// <summary>Thread d'exécution.</summary>
        public int ThreadIndex;

        /// <summary>Début de l'exécution.</summary>
        public DateTime StartTime;

        /// <summary>Fin de l'exécution.</summary>
        public DateTime EndTime;

        /// <summary>Durée en ms.</summary>
        public float DurationMs;

        /// <summary>Statut du job.</summary>
        public JobStatus Status;
    }

    /// <summary>
    /// Heatmap de charge CPU.
    /// </summary>
    public struct CPUHeatmapReport
    {
        /// <summary>Charge par thread (index → 0.0-1.0).</summary>
        public Dictionary<int, float> LoadByThread;

        /// <summary>Charge par catégorie.</summary>
        public Dictionary<JobCategory, float> LoadByCategory;

        /// <summary>Charge maximale.</summary>
        public float MaxLoad;

        /// <summary>Charge minimale.</summary>
        public float MinLoad;

        /// <summary>Charge moyenne.</summary>
        public float AverageLoad;

        /// <summary>Timestamp.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Historique des événements du système de jobs.
    /// </summary>
    public struct JobEventHistory
    {
        /// <summary>Liste des événements.</summary>
        public List<JobEvent> Events;

        /// <summary>Début de l'historique.</summary>
        public DateTime StartTime;

        /// <summary>Fin de l'historique.</summary>
        public DateTime EndTime;

        /// <summary>Nombre total d'événements.</summary>
        public int EventCount;
    }

    /// <summary>
    /// Événement du système de jobs.
    /// </summary>
    public struct JobEvent
    {
        /// <summary>Nom de l'événement.</summary>
        public string Name;

        /// <summary>Type de l'événement.</summary>
        public JobEventType Type;

        /// <summary>Timestamp.</summary>
        public DateTime Timestamp;

        /// <summary>Données associées.</summary>
        public object Data;

        /// <summary>Thread concerné.</summary>
        public int ThreadIndex;
    }

    /// <summary>
    /// Type d'événement du système de jobs.
    /// </summary>
    public enum JobEventType
    {
        JobScheduled,
        JobStarted,
        JobCompleted,
        JobFailed,
        JobCancelled,
        JobRetried,
        JobRecovered,
        WorkStealAttempt,
        WorkStealSuccess,
        ThreadCreated,
        ThreadDestroyed,
        ThreadSuspended,
        ThreadResumed,
        DeadlockDetected,
        BudgetExceeded,
        CacheCleared,
        ConfigurationChanged
    }

    /// <summary>
    /// Données de télémétrie du système de jobs.
    /// </summary>
    public struct JobSystemTelemetryData
    {
        /// <summary>Métriques collectées.</summary>
        public Dictionary<string, object> Metrics;

        /// <summary>Timestamp.</summary>
        public DateTime Timestamp;

        /// <summary>Durée de collecte.</summary>
        public TimeSpan CollectionDuration;
    }

    /// <summary>
    /// Rapport de profilage des jobs.
    /// </summary>
    public struct JobProfilingReport
    {
        /// <summary>Nom de la session.</summary>
        public string SessionName;

        /// <summary>Début de la session.</summary>
        public DateTime StartTime;

        /// <summary>Fin de la session.</summary>
        public DateTime EndTime;

        /// <summary>Données par catégorie.</summary>
        public Dictionary<JobCategory, JobCategoryPerformanceData> CategoryData;

        /// <summary>Timeline des jobs.</summary>
        public JobTimelineReport Timeline;

        /// <summary>Heatmap de charge.</summary>
        public CPUHeatmapReport Heatmap;
    }

    /// <summary>
    /// Rapport d'utilisation du cache.
    /// </summary>
    public struct JobCacheUsageReport
    {
        /// <summary>Taux de cache hit (0.0 à 1.0).</summary>
        public float HitRate;

        /// <summary>Taux de cache miss.</summary>
        public float MissRate;

        /// <summary>Nombre d'entrées dans le cache.</summary>
        public int EntryCount;

        /// <summary>Taille maximale du cache.</summary>
        public int MaxSize;

        /// <summary>Taille utilisée en octets.</summary>
        public long UsedSizeBytes;

        /// <summary>Timestamp.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Heatmap du système de jobs.
    /// </summary>
    public struct JobSystemHeatmap
    {
        /// <summary>Charge par sous-système.</summary>
        public Dictionary<JobSubsystemType, float> LoadBySubsystem;

        /// <summary>Charge par catégorie.</summary>
        public Dictionary<JobCategory, float> LoadByCategory;

        /// <summary>Timestamp.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Tendance des performances.
    /// </summary>
    public struct JobPerformanceTrend
    {
        /// <summary>Échantillons de performance.</summary>
        public List<JobPerformanceSample> Samples;

        /// <summary>Direction de la tendance.</summary>
        public JobTrendDirection Direction;

        /// <summary>Timestamp.</summary>
        public DateTime ReportTime;
    }

    /// <summary>
    /// Échantillon de performance.
    /// </summary>
    public struct JobPerformanceSample
    {
        /// <summary>Temps CPU en ms.</summary>
        public float CPUTimeMs;

        /// <summary>Utilisation mémoire en octets.</summary>
        public long MemoryUsageBytes;

        /// <summary>Nombre de jobs actifs.</summary>
        public int ActiveJobs;

        /// <summary>Timestamp.</summary>
        public DateTime Timestamp;
    }

    /// <summary>
    /// Snapshot complet du système de jobs.
    /// </summary>
    public struct JobSystemSnapshot
    {
        /// <summary>État du système.</summary>
        public JobSystemState State;

        /// <summary>Configuration.</summary>
        public JobSystemConfig Config;

        /// <summary>Statistiques du pool.</summary>
        public ThreadPoolStats PoolStats;

        /// <summary>Charge des threads.</summary>
        public ThreadLoadReport ThreadLoad;

        /// <summary>Jobs en attente.</summary>
        public int PendingJobs;

        /// <summary>Jobs actifs.</summary>
        public int ActiveJobs;

        /// <summary>Jobs complétés.</summary>
        public long CompletedJobs;

        /// <summary>Utilisation mémoire.</summary>
        public long MemoryUsage;

        /// <summary>Utilisation du budget.</summary>
        public float BudgetUsage;

        /// <summary>Timestamp du snapshot.</summary>
        public DateTime Timestamp;
    }

    /// <summary>
    /// Allocateur de budget du système de jobs.
    /// </summary>
    public class JobBudgetAllocator
    {
        /// <summary>Budget total par frame en ms.</summary>
        public float TotalBudgetMs { get; set; }

        /// <summary>Budget utilisé cette frame en ms.</summary>
        public float UsedTimeMs { get; set; }

        /// <summary>Budget restant en ms.</summary>
        public float RemainingTimeMs { get; set; }

        /// <summary>True si le budget est dépassé.</summary>
        public bool IsOverBudget { get; set; }

        /// <summary>Budgets par catégorie.</summary>
        public Dictionary<JobCategory, float> CategoryBudgets { get; set; } = new();

        /// <summary>Utilisation par catégorie.</summary>
        public Dictionary<JobCategory, float> CategoryUsage { get; set; } = new();
    }

    /// <summary>
    /// Profiler du système de jobs.
    /// </summary>
    public class JobSystemProfiler
    {
        /// <summary>Données de profilage.</summary>
        public Dictionary<string, JobProfilerData> ProfileData { get; set; } = new();

        /// <summary>True si le profiling est actif.</summary>
        public bool IsProfiling { get; set; }

        /// <summary>Nom de la session actuelle.</summary>
        public string CurrentSession { get; set; }

        /// <summary>Commence un échantillon.</summary>
        public void BeginSample(string name) { }

        /// <summary>Termine un échantillon.</summary>
        public void EndSample(string name) { }

        /// <summary>Obtient les données d'un échantillon.</summary>
        public JobProfilerData GetSampleData(string name) { return default; }
    }

    /// <summary>
    /// Données de profilage d'un échantillon.
    /// </summary>
    public struct JobProfilerData
    {
        /// <summary>Nom de l'échantillon.</summary>
        public string Name;

        /// <summary>Temps CPU en ms.</summary>
        public float CPUTimeMs;

        /// <summary>Nombre d'appels.</summary>
        public int CallCount;

        /// <summary>Temps moyen par appel en ms.</summary>
        public float AverageTimeMs;

        /// <summary>Temps maximum en ms.</summary>
        public float MaxTimeMs;

        /// <summary>Temps minimum en ms.</summary>
        public float MinTimeMs;
    }

    /// <summary>
    /// Overlay de debug du système de jobs.
    /// </summary>
    public class JobDebugOverlay
    {
        /// <summary>Visualisations actives.</summary>
        public Dictionary<JobDebugVisualizationType, bool> Visualizations { get; set; } = new();

        /// <summary>Couleur de l'overlay.</summary>
        public Vector4 OverlayColor { get; set; }

        /// <summary>Opacité de l'overlay.</summary>
        public float OverlayOpacity { get; set; }

        /// <summary>Affiche les métriques de performance.</summary>
        public bool ShowPerformanceMetrics { get; set; }
    }

    /// <summary>
    /// Logger d'erreurs du système de jobs.
    /// </summary>
    public class JobErrorLogger
    {
        /// <summary>Journal des erreurs.</summary>
        public List<JobErrorEntry> ErrorLog { get; set; } = new();

        /// <summary>Nombre maximum d'entrées.</summary>
        public int MaxLogEntries { get; set; } = 1000;

        /// <summary>Niveau de log minimum.</summary>
        public JobErrorLevel LogLevel { get; set; } = JobErrorLevel.Warning;
    }

    /// <summary>
    /// Informations sur un sous-système du job system.
    /// </summary>
    public struct JobSubsystemInfo
    {
        /// <summary>Type du sous-système.</summary>
        public JobSubsystemType Type;

        /// <summary>Nom du sous-système.</summary>
        public string Name;

        /// <summary>True si le sous-système est initialisé.</summary>
        public bool IsInitialized;

        /// <summary>True si le sous-système est actif.</summary>
        public bool IsActive;
    }

    /// <summary>
    /// État d'un sous-système du job system.
    /// </summary>
    public struct JobSubsystemState
    {
        /// <summary>Type du sous-système.</summary>
        public JobSubsystemType Type;

        /// <summary>True si initialisé.</summary>
        public bool IsInitialized;

        /// <summary>True si en cours d'exécution.</summary>
        public bool IsRunning;

        /// <summary>True si des erreurs sont présentes.</summary>
        public bool HasErrors;

        /// <summary>Dernière mise à jour.</summary>
        public DateTime LastUpdate;

        /// <summary>Temps CPU utilisé en ms.</summary>
        public float CPUTimeMs;

        /// <summary>Utilisation mémoire en octets.</summary>
        public long MemoryUsageBytes;
    }

    /// <summary>
    /// Rapport d'intégrité des sous-systèmes.
    /// </summary>
    public struct JobSubsystemIntegrityReport
    {
        /// <summary>True si tous les sous-systèmes sont intacts.</summary>
        public bool IsIntact;

        /// <summary>Liste des problèmes détectés.</summary>
        public List<string> Issues;

        /// <summary>Timestamp du rapport.</summary>
        public DateTime GeneratedAt;
    }

    /// <summary>
    /// Informations de version.
    /// </summary>
    public struct VersionInfo
    {
        /// <summary>Numéro de version.</summary>
        public string Version;

        /// <summary>Numéro de build.</summary>
        public string BuildNumber;

        /// <summary>Date du build.</summary>
        public DateTime BuildDate;
    }

    /// <summary>
    /// Métadonnées de build.
    /// </summary>
    public struct BuildMetadata
    {
        /// <summary>Plateforme cible.</summary>
        public string Platform;

        /// <summary>Configuration (Debug, Release, etc.).</summary>
        public string Configuration;

        /// <summary>Version du compilateur.</summary>
        public string CompilerVersion;

        /// <summary>Fonctionnalités activées.</summary>
        public List<string> Features;
    }

    /// <summary>
    /// Carte d'affinité par catégorie.
    /// </summary>
    public struct CategoryAffinityMap
    {
        /// <summary>Affectation catégorie → index de thread.</summary>
        public Dictionary<JobCategory, int> Assignments;

        /// <summary>Catégories sans affectation.</summary>
        public List<JobCategory> UnassignedCategories;

        /// <summary>
        /// Constructeur de copie : ThreadAffinityManager ecrit
        /// `new CategoryAffinityMap(_affinityMap)` pour rendre un instantane.
        /// </summary>
        public CategoryAffinityMap(CategoryAffinityMap autre)
        {
            Assignments = autre.Assignments != null
                ? new Dictionary<JobCategory, int>(autre.Assignments)
                : new Dictionary<JobCategory, int>();
            UnassignedCategories = autre.UnassignedCategories != null
                ? new List<JobCategory>(autre.UnassignedCategories)
                : new List<JobCategory>();
        }

        public void Clear()
        {
            Assignments?.Clear();
            UnassignedCategories?.Clear();
        }

        /// <summary>
        /// Threads affectes a une categorie. Le site d'appel lit `threads.Count`,
        /// d'ou une liste et non l'entier de `Assignments`.
        /// </summary>
        public bool TryGetValue(JobCategory category, out List<int> threads)
        {
            if (Assignments != null && Assignments.TryGetValue(category, out var t))
            {
                threads = new List<int> { t };
                return true;
            }
            threads = new List<int>();
            return false;
        }
    }

    #endregion

    #region Structures additionnelles pour les 80 idées de perfectionnement studio

    // Structures pour l'optimisation runtime
    public struct GPUPreference
    {
        public float ComputeLoad;
        public float MemoryBandwidth;
        public bool PreferGPU;
    }

    public struct SchedulerPredictionReport
    {
        public Dictionary<JobCategory, TimeSpan> PredictedExecutionTimes;
        public Dictionary<int, float> PredictedThreadLoads;
        public DateTime PredictionTimestamp;
    }

    public interface IJobWarmupManager
    {
        void WarmupJob(IJob job);
        void WarmupJobs(IReadOnlyList<IJob> jobs);
        void WarmupCategory(JobCategory category);
    }

    public interface IJobCoolingManager
    {
        void CoolJob(JobHandle handle);
        void CoolJobs(IReadOnlyList<JobHandle> handles);
        void CoolCategory(JobCategory category);
    }

    public struct JobSystemAdaptiveBudget
    {
        public float CPUBudget;
        public float GPUBudget;
        public float MemoryBudget;
        public float NetworkBudget;
    }

    public struct FrameAnalysisReport
    {
        public bool IsOverloaded;
        public float LoadPercentage;
        public List<string> Bottlenecks;
        public List<JobHandle> LongRunningJobs;
    }

    // Structures pour la sécurité multithread
    public struct ThreadLockInspectionReport
    {
        public List<LockInfo> UnusedLocks;
        public List<LockInfo> HighlyContendedLocks;
        public float AverageContentionTime;
    }

    public struct LockInfo
    {
        public string Name;
        public int ContentionCount;
        public float AverageWaitTime;
    }

    public struct ThreadDeadlockDetectionReport
    {
        public List<DeadlockInfo> DetectedDeadlocks;
        public bool AutoRollbackApplied;
        public int RollbackCount;
    }

    public struct DeadlockInfo
    {
        public List<ThreadInfo> InvolvedThreads;
        public List<LockInfo> InvolvedLocks;
        public DateTime DetectionTime;
    }

    public struct ThreadInfo
    {
        public int ThreadId;
        public string Name;
        public ThreadState State;
    }

    public struct ThreadSafeQueueValidationReport
    {
        public bool AllQueuesValid;
        public List<QueueValidationIssue> Issues;
        public DateTime ValidationTime;
    }

    public struct QueueValidationIssue
    {
        public string QueueName;
        public string IssueDescription;
        public SeverityLevel Severity;
    }

    public enum SeverityLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public struct ThreadRaceAnalysisReport
    {
        public List<RaceCondition> RaceConditions;
        public int TotalReaders;
        public int TotalWriters;
        public DateTime AnalysisTime;
    }

    public struct RaceCondition
    {
        public string ResourceName;
        public List<ThreadInfo> AccessingThreads;
        public DateTime FirstAccessTime;
        public DateTime LastAccessTime;
    }

    public struct ThreadIntegrityReport
    {
        public List<ThreadIntegrityIssue> Issues;
        public int TotalThreads;
        public int HealthyThreads;
        public DateTime ReportTime;
    }

    public struct ThreadIntegrityIssue
    {
        public int ThreadId;
        public string IssueType;
        public string Description;
        public DateTime DetectionTime;
    }

    public struct ThreadWatchdogReport
    {
        public List<InactiveThreadInfo> InactiveThreads;
        public float AverageResponseTime;
        public DateTime ReportTime;
    }

    public struct InactiveThreadInfo
    {
        public int ThreadId;
        public TimeSpan InactivityDuration;
        public string LastActivity;
    }

    // Structures pour le profilage & visualisation
    public interface IJobSystemTimelineVisualizer
    {
        void Render();
        void SetTimeRange(TimeSpan startTime, TimeSpan endTime);
        void HighlightJobs(List<JobHandle> handles);
    }

    public interface IJobSystemHeatmapOverlay
    {
        void Render();
        void SetDisplayMode(HeatmapDisplayMode mode);
        void SetOpacity(float opacity);
    }

    public enum HeatmapDisplayMode
    {
        CPU,
        Memory,
        IO,
        Network
    }

    public interface IJobSystemLatencyGraph
    {
        void Render();
        void SetTimeWindow(TimeSpan window);
        void SetJobFilter(JobCategory category);
    }

    public interface IJobSystemFlameGraphExporter
    {
        void ExportToFile(string filePath);
        void ExportToImage(string imagePath);
        void SetDetailLevel(FlameGraphDetailLevel level);
    }

    public enum FlameGraphDetailLevel
    {
        Basic,
        Detailed,
        Full
    }

    public interface IJobSystemPerformanceHUD
    {
        void Render();
        void SetMetrics(List<string> metrics);
        void SetPosition(Vector2 position);
    }

    public interface IJobSystemProfilerOverlay
    {
        void Render();
        void ToggleVisibility();
        void SetTheme(ProfilerTheme theme);
    }

    public enum ProfilerTheme
    {
        Light,
        Dark,
        Custom
    }

    public interface IJobSystemFrameBudgetVisualizer
    {
        void Render();
        void SetBudget(float budget);
        void ShowBreakdown(bool show);
    }

    public interface IJobSystemMemoryHeatmap
    {
        void Render();
        void SetGranularity(MemoryGranularity granularity);
        void HighlightMemoryLeaks(bool highlight);
    }

    public enum MemoryGranularity
    {
        Page,
        Block,
        Object
    }

    public interface IJobSystemThreadLoadGraph
    {
        void Render();
        void SetTimeWindow(TimeSpan window);
        void ShowThreadNames(bool show);
    }

    public interface IJobSystemEventTimeline
    {
        void Render();
        void SetEventTypes(List<JobEventType> types);
        void FilterByCategory(JobCategory category);
    }

    // Structures pour l'interopérabilité moteur
    public interface IJobSystemRenderHook
    {
        void SynchronizeGPUJobs();
        void SubmitToGPU(IJob job);
        void WaitForGPUCompletion();
    }

    public interface IJobSystemPhysicsHook
    {
        void ScheduleCollisionJob(IJob job);
        void BatchCollisionJobs(List<IJob> jobs);
        void SyncPhysicsStep();
    }

    public interface IJobSystemAIHook
    {
        void SchedulePathfindingJob(IJob job);
        void ScheduleBehaviorTreeEvaluation(IJob job);
        void SyncAIUpdates();
    }

    public interface IJobSystemAudioHook
    {
        void ScheduleSpatializationJob(IJob job);
        void ScheduleMixingJob(IJob job);
        void SyncAudioFrame();
    }

    public interface IJobSystemAnimationHook
    {
        void ScheduleBlendingJob(IJob job);
        void ScheduleIKJob(IJob job);
        void SyncAnimationFrame();
    }

    public interface IJobSystemParticlesHook
    {
        void ScheduleEffectJob(IJob job);
        void BatchEffectJobs(List<IJob> jobs);
        void SyncParticleSystems();
    }

    public interface IJobSystemNetworkingHook
    {
        void ScheduleNetworkJob(IJob job);
        void BatchNetworkJobs(List<IJob> jobs);
        void SyncNetworkFrame();
    }

    public interface IJobSystemECSHook
    {
        void ScheduleEntityJob(IJob job);
        void BatchEntityJobs(List<IJob> jobs);
        void SyncEntityManager();
    }

    public interface IJobSystemEventBusHook
    {
        void ScheduleEventJob(IJob job);
        void PublishEvent(object eventData);
        void SubscribeToEvents();
    }

    public interface IJobSystemProfilerHook
    {
        void RegisterGlobalProfiler();
        void SubmitMetrics(Dictionary<string, object> metrics);
        void SyncProfilerData();
    }

    // Structures pour le debug & tests
    public interface IJobSystemCLI
    {
        void InspectJob(JobHandle handle);
        void ListAllJobs();
        void CancelJob(JobHandle handle);
        void ShowStatistics();
    }

    public interface IJobSystemDashboard
    {
        void Render();
        void RefreshData();
        void SetRefreshInterval(TimeSpan interval);
    }

    public interface IJobSystemConfigWizard
    {
        JobSystemConfig GenerateConfig();
        void ImportConfig(string filePath);
        void ExportConfig(string filePath);
    }

    public interface IJobSystemTemplateLibrary
    {
        IJob GetTemplate(string templateName);
        void RegisterTemplate(string name, IJob template);
        void ListTemplates();
    }

    public interface IJobSystemCookbook
    {
        void ShowRecipe(string recipeName);
        void ListRecipes();
        void AddRecipe(string name, Action recipeAction);
    }

    public interface IJobSystemMetricsAPI
    {
        Dictionary<string, object> GetMetrics();
        void RegisterMetric(string name, Func<object> getter);
        void ExportMetrics(string format, string filePath);
    }

    // IJobSystemGraphBuilder est parti avec le reste de l'intention, dans
    // Docs/Intention/IJobSystem.cs.txt : aucun usage hors de ce fichier, et son
    // Build() etait le dernier appelant du type absent IJobGraph. Le declarer
    // vide aurait produit exactement le repere sans contenu qu'on refuse ici.

    public interface IJobSystemContractValidator
    {
        ValidationResult ValidateInputs(IJob job, object inputs);
        ValidationResult ValidateOutputs(IJob job, object outputs);
        void RegisterContract(string contractId, ContractDefinition definition);
    }

    public struct ContractDefinition
    {
        public string Id;
        public List<InputOutputDefinition> Inputs;
        public List<InputOutputDefinition> Outputs;
    }

    public struct InputOutputDefinition
    {
        public string Name;
        public Type DataType;
        public bool IsRequired;
        public object DefaultValue;
    }

    public interface IJobSystemSandboxAPI
    {
        void LimitMemoryUsage(long bytes);
        void LimitExecutionTime(TimeSpan time);
        void RestrictFileAccess(List<string> allowedPaths);
    }

    public interface IJobSystemReplayAPI
    {
        void RecordFrame(int frameNumber);
        void ReplayFrame(int frameNumber);
        void ExportReplay(string filePath);
    }

    // Structures pour la distribution & cloud
    public interface IJobSystemClusterManager
    {
        void JoinCluster(string clusterAddress);
        void LeaveCluster();
        void DistributeJob(IJob job);
        void SyncClusterState();
    }

    public interface IJobSystemRemoteExecutor
    {
        JobHandle ExecuteOnRemote(string remoteAddress, IJob job);
        void SyncRemoteJobs();
        void MonitorRemoteNodes();
    }

    public struct RaftConfig
    {
        public List<string> ClusterNodes;
        public TimeSpan ElectionTimeout;
        public TimeSpan HeartbeatInterval;
        public bool EnablePersistence;
    }

    public struct CloudBatchConfig
    {
        public string Provider;
        public string Region;
        public int MaxInstances;
        public float CostPerHour;
    }

    public struct EdgeNodeConfig
    {
        public int MaxLoad;
        public TimeSpan Timeout;
        public List<string> AllowedCategories;
    }

    public interface IJobSystemGPUOffloadManager
    {
        void OffloadJobToGPU(IJob job);
        void SyncGPUResults();
        void MonitorGPUUsage();
    }

    public interface IJobSystemNetworkScheduler
    {
        void ScheduleNetworkJob(IJob job);
        void BatchNetworkJobs(List<IJob> jobs);
        void HandleNetworkLatency();
    }

    public interface IJobSystemTelemetryAggregator
    {
        void AggregateTelemetry();
        void ExportTelemetry(string destination);
        void SetAggregationInterval(TimeSpan interval);
    }

    public interface IJobSystemDistributedLockManager
    {
        void AcquireLock(string lockName);
        void ReleaseLock(string lockName);
        void MonitorLocks();
    }

    public interface IJobSystemCrossRegionScheduler
    {
        void ScheduleJobWithLatencyAwareness(IJob job);
        void OptimizeForRegionalPerformance();
        void MonitorCrossRegionMetrics();
    }

    // Structures pour l'expérimental & recherche
    public interface IJobSystemPredictivePrefetcher
    {
        void PrefetchJob(IJob job);
        void AnalyzeAccessPatterns();
        void OptimizePrefetchStrategy();
    }

    public interface IJobSystemAdaptiveBatcher
    {
        void BatchJobsDynamically(List<IJob> jobs);
        void AdjustBatchSizeBasedOnLoad();
        void MonitorBatchEfficiency();
    }

    public struct StressTestConfig
    {
        public int MaxConcurrentJobs;
        public TimeSpan TestDuration;
        public float FailureRate;
        public List<JobCategory> CategoriesUnderTest;
    }

    public struct ChaosInjectionConfig
    {
        public float FailureProbability;
        public TimeSpan InjectionInterval;
        public List<ChaosType> ChaosTypes;
    }

    public enum ChaosType
    {
        RandomCrash,
        NetworkDelay,
        MemoryPressure,
        CPUSpikes
    }

    public struct FuzzTestConfig
    {
        public int Iterations;
        public List<FuzzInputType> InputTypes;
        public List<FuzzMutationType> MutationTypes;
    }

    public enum FuzzInputType
    {
        Integer,
        String,
        Float,
        Boolean
    }

    public enum FuzzMutationType
    {
        BitFlip,
        ValueChange,
        BufferOverflow,
        NullPointer
    }

    public struct PerformanceBaseline
    {
        public string BaselineName;
        public Dictionary<string, float> Metrics;
        public DateTime CreationTime;
    }

    public struct PerformanceRegressionReport
    {
        public bool RegressionDetected;
        public Dictionary<string, float> MetricChanges;
        public float ConfidenceLevel;
        public DateTime ReportTime;
    }

    public struct MemoryLeakReport
    {
        public List<MemoryLeak> Leaks;
        public long TotalLeakedBytes;
        public DateTime DetectionTime;
    }

    public struct MemoryLeak
    {
        public string AllocationSite;
        public long SizeInBytes;
        public DateTime AllocationTime;
    }

    public struct BuildInfo
    {
        public string Version;
        public string CommitHash;
        public DateTime BuildTime;
        public List<string> Features;
    }

    public struct PerformanceComparisonReport
    {
        public BuildInfo BuildA;
        public BuildInfo BuildB;
        public Dictionary<string, ComparisonResult> Comparisons;
        public DateTime ReportTime;
    }

    public struct ComparisonResult
    {
        public float ValueA;
        public float ValueB;
        public float Difference;
        public float PercentageChange;
        public bool Improvement;
    }

    public struct UnitTestConfig
    {
        public List<string> TestSuites;
        public int MaxParallelTests;
        public TimeSpan Timeout;
        public bool GenerateCoverageReport;
    }

    public struct UnitTestReport
    {
        public int TotalTests;
        public int PassedTests;
        public int FailedTests;
        public List<UnitTestResult> Results;
        public TimeSpan TotalDuration;
    }

    public struct UnitTestResult
    {
        public string TestName;
        public TestStatus Status;
        public string ErrorMessage;
        public TimeSpan Duration;
    }

    public enum TestStatus
    {
        Passed,
        Failed,
        Skipped,
        Inconclusive
    }

    // Structures pour les structures existantes enrichies
    public struct JobSystemInitializationDiagnostics
    {
        public bool InitializationSuccessful;
        public List<string> InitializationWarnings;
        public TimeSpan InitializationDuration;
        public DateTime InitializationTimestamp;
    }

    public struct JobSystemShutdownDiagnostics
    {
        public bool ShutdownSuccessful;
        public List<string> ShutdownWarnings;
        public TimeSpan ShutdownDuration;
        public DateTime ShutdownTimestamp;
    }

    public enum JobSystemLifecycleState
    {
        NotInitialized,
        Initializing,
        Initialized,
        Starting,
        Running,
        Pausing,
        Paused,
        Resuming,
        Stopping,
        Stopped,
        Shutdown,
        Error
    }

    public struct JobSystemLifecycleTelemetry
    {
        public Dictionary<string, object> LifecycleMetrics;
        public DateTime Timestamp;
        public TimeSpan CollectionDuration;
    }

    public struct JobSystemStartupProfile
    {
        public Dictionary<string, TimeSpan> StartupPhases;
        public TimeSpan TotalStartupTime;
        public DateTime StartupTimestamp;
    }

    public struct JobSystemShutdownProfile
    {
        public Dictionary<string, TimeSpan> ShutdownPhases;
        public TimeSpan TotalShutdownTime;
        public DateTime ShutdownTimestamp;
    }

    public struct JobSystemLifecycleHeatmap
    {
        public Dictionary<int, float> LoadByPhase;
        public Dictionary<string, float> LoadByMetric;
        public float MaxLoad;
        public float MinLoad;
        public float AverageLoad;
        public DateTime ReportTime;
    }

    public struct JobSystemLifecycleTrend
    {
        public List<JobSystemLifecycleSample> Samples;
        public JobTrendDirection Direction;
        public DateTime ReportTime;
    }

    public struct JobSystemLifecycleSample
    {
        public TimeSpan CPUTimeMs;
        public long MemoryUsageBytes;
        public int ActiveJobs;
        public DateTime Timestamp;
    }

    public struct JobSystemLifecycleBudget
    {
        public float TotalBudgetMs;
        public float UsedTimeMs;
        public float RemainingTimeMs;
        public bool IsOverBudget;
        public Dictionary<string, float> CategoryBudgets;
        public Dictionary<string, float> CategoryUsage;
    }

    public struct JobSystemLifecycleErrorReport
    {
        public List<JobSystemLifecycleErrorEntry> Errors;
        public int ErrorCount;
        public Dictionary<JobErrorLevel, int> ErrorsByLevel;
        public Dictionary<JobCategory, int> ErrorsByCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemLifecycleErrorEntry
    {
        public DateTime Timestamp;
        public JobErrorLevel Level;
        public string Message;
        public Exception Exception;
        public string StackTrace;
        public JobHandle JobHandle;
        public JobCategory Category;
    }

    public struct JobSystemLifecyclePerformanceData
    {
        public JobCategory Category;
        public float AverageCPUTimeMs;
        public float MaxCPUTimeMs;
        public long JobsExecuted;
        public long JobsFailed;
        public long MemoryUsageBytes;
        public DateTime ReportTime;
    }

    public struct JobSystemLifecycleTimeline
    {
        public List<JobSystemLifecycleEvent> Events;
        public DateTime StartTime;
        public DateTime EndTime;
        public float TotalDurationMs;
        public int PhaseCount;
    }

    public struct JobSystemLifecycleEvent
    {
        public string PhaseName;
        public JobHandle Handle;
        public JobCategory Category;
        public int ThreadIndex;
        public DateTime StartTime;
        public DateTime EndTime;
        public float DurationMs;
        public JobStatus Status;
    }

    public struct JobSystemLifecycleEventHistory
    {
        public List<JobSystemLifecycleEvent> Events;
        public DateTime StartTime;
        public DateTime EndTime;
        public int EventCount;
    }

    public struct JobSystemLifecycleDependencyGraph
    {
        public Dictionary<JobHandle, List<JobHandle>> Adjacency;
        public List<JobHandle> ExecutionOrder;
        public bool HasCycle;
        public int NodeCount;
        public int EdgeCount;
    }

    public struct JobSystemLifecycleProfilerZone
    {
        public string ZoneName;
        public TimeSpan CPUTimeMs;
        public int CallCount;
        public float AverageTimeMs;
        public float MaxTimeMs;
        public float MinTimeMs;
    }

    public struct JobSystemLifecycleTelemetryData
    {
        public Dictionary<string, object> Metrics;
        public DateTime Timestamp;
        public TimeSpan CollectionDuration;
    }

    public struct JobSystemLifecycleVersionInfo
    {
        public string Version;
        public string BuildNumber;
        public DateTime BuildDate;
    }

    public struct JobSystemLifecycleBuildMetadata
    {
        public string Platform;
        public string Configuration;
        public string CompilerVersion;
        public List<string> Features;
    }

    // Structures pour la gestion des threads enrichies
    // Classe et non struct : ThreadAffinityManager publie sa configuration par
    // Volatile.Read/Write, qui exigent un type reference, et la remplace d'un
    // bloc sous _configLock plutot que d'en muter les champs.
    public class ThreadAffinityManagerConfig
    {
        public long CpuAffinityMask;
        public int ReservedCoreCount;
        public Dictionary<int, int> ThreadToCoreMap;
        public bool EnableHyperthreading;
        public ThreadAffinityMode Mode;

        // Les neuf membres ci-dessous sont reclames par ThreadAffinityManager.
        // Les cinq premiers ont leur type dicte par un usage precis :
        //   EnableAutoBalancing / SafeMode  testes en booleen        (ligne 613)
        //   RebalanceIntervalSec            compare a TotalSeconds   (615, 1197)
        //   RebalanceThreshold              compare a une fraction 0..1 (634, 1198)
        // Les quatre derniers ne sont que lus : leur type est deduit du nom, pas
        // mesure. A revoir si un appelant les contraint un jour.
        public bool EnableAutoBalancing;
        public bool SafeMode;
        public double RebalanceIntervalSec;
        public float RebalanceThreshold;
        public string DefaultProfile;
        public bool CoreParkingEnabled;
        public bool HugePagesEnabled;
        public bool PrefetcherEnabled;
        public float TurboBudgetPercentage;

        public ThreadAffinityManagerConfig() { }

        /// <summary>Constructeur de copie : `new ThreadAffinityManagerConfig(_config)`.</summary>
        public ThreadAffinityManagerConfig(ThreadAffinityManagerConfig autre)
        {
            CpuAffinityMask = autre.CpuAffinityMask;
            ReservedCoreCount = autre.ReservedCoreCount;
            ThreadToCoreMap = autre.ThreadToCoreMap != null ? new Dictionary<int, int>(autre.ThreadToCoreMap) : null;
            EnableHyperthreading = autre.EnableHyperthreading;
            Mode = autre.Mode;
            EnableAutoBalancing = autre.EnableAutoBalancing;
            SafeMode = autre.SafeMode;
            RebalanceIntervalSec = autre.RebalanceIntervalSec;
            RebalanceThreshold = autre.RebalanceThreshold;
            DefaultProfile = autre.DefaultProfile;
            CoreParkingEnabled = autre.CoreParkingEnabled;
            HugePagesEnabled = autre.HugePagesEnabled;
            PrefetcherEnabled = autre.PrefetcherEnabled;
            TurboBudgetPercentage = autre.TurboBudgetPercentage;
        }
    }

    public struct ThreadAffinityHeatmap
    {
        public Dictionary<int, float> LoadByThread;
        public Dictionary<JobCategory, float> LoadByCategory;
        public float MaxLoad;
        public float MinLoad;
        public float AverageLoad;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityTrend
    {
        public List<ThreadAffinitySample> Samples;
        public JobTrendDirection Direction;
        public DateTime ReportTime;
    }

    public struct ThreadAffinitySample
    {
        public float CPUTimeMs;
        public long MemoryUsageBytes;
        public int ActiveJobs;
        public DateTime Timestamp;
    }

    public struct ThreadAffinityBudget
    {
        public float TotalBudgetMs;
        public float UsedTimeMs;
        public float RemainingTimeMs;
        public bool IsOverBudget;
        public Dictionary<string, float> CategoryBudgets;
        public Dictionary<string, float> CategoryUsage;
    }

    public struct ThreadAffinityErrorReport
    {
        public List<ThreadAffinityErrorEntry> Errors;
        public int ErrorCount;
        public Dictionary<JobErrorLevel, int> ErrorsByLevel;
        public Dictionary<JobCategory, int> ErrorsByCategory;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityErrorEntry
    {
        public DateTime Timestamp;
        public JobErrorLevel Level;
        public string Message;
        public Exception Exception;
        public string StackTrace;
        public JobHandle JobHandle;
        public JobCategory Category;
    }

    public struct ThreadAffinityTelemetryData
    {
        public Dictionary<string, object> Metrics;
        public DateTime Timestamp;
        public TimeSpan CollectionDuration;
    }

    public struct ThreadAffinityPerformanceData
    {
        public JobCategory Category;
        public float AverageCPUTimeMs;
        public float MaxCPUTimeMs;
        public long JobsExecuted;
        public long JobsFailed;
        public long MemoryUsageBytes;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityProfilerZone
    {
        public string ZoneName;
        public TimeSpan CPUTimeMs;
        public int CallCount;
        public float AverageTimeMs;
        public float MaxTimeMs;
        public float MinTimeMs;
    }

    public struct ThreadAffinityVersionInfo
    {
        public string Version;
        public string BuildNumber;
        public DateTime BuildDate;
    }

    public struct ThreadAffinityBuildMetadata
    {
        public string Platform;
        public string Configuration;
        public string CompilerVersion;
        public List<string> Features;
    }

    public struct ThreadAffinityLockStatus
    {
        public List<ThreadAffinityLockInfo> ActiveLocks;
        public List<ThreadAffinityLockInfo> ContendedLocks;
        public Dictionary<string, float> AverageWaitTimeByLock;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityLockInfo
    {
        public string Name;
        public int OwnerThread;
        public int WaitingThreads;
        public float HoldTimeMs;
    }

    public struct ThreadAffinityCacheUsage
    {
        public float HitRate;
        public float MissRate;
        public int EntryCount;
        public int MaxSize;
        public long UsedSizeBytes;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityMemoryUsage
    {
        public long UsageBytes;
        public Dictionary<string, long> UsageByComponent;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobCount
    {
        public int Count;
        public Dictionary<JobCategory, int> CountByCategory;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobLatency
    {
        public Dictionary<JobHandle, TimeSpan> Latencies;
        public TimeSpan AverageLatency;
        public TimeSpan MaxLatency;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobThroughput
    {
        public float JobsPerSecond;
        public float PeakJobsPerSecond;
        public long TotalJobsProcessed;
        public Dictionary<JobCategory, float> ThroughputByCategory;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobFailureRate
    {
        public float FailureRate;
        public int TotalFailures;
        public Dictionary<JobCategory, int> FailuresByCategory;
        public Dictionary<string, int> FailuresByErrorType;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobRetryCount
    {
        public float AverageRetries;
        public int MaxRetries;
        public long TotalRetries;
        public float RetrySuccessRate;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobPriorityDistribution
    {
        public Dictionary<JobPriority, int> CountByPriority;
        public JobPriority DominantPriority;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobExecutionTime
    {
        public Dictionary<JobHandle, TimeSpan> ExecutionTimes;
        public TimeSpan AverageExecutionTime;
        public TimeSpan MaxExecutionTime;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobDependencyGraph
    {
        public Dictionary<JobHandle, List<JobHandle>> Adjacency;
        public List<JobHandle> ExecutionOrder;
        public bool HasCycle;
        public int NodeCount;
        public int EdgeCount;
    }

    public struct ThreadAffinityJobHeatmap
    {
        public Dictionary<int, float> LoadByThread;
        public Dictionary<JobCategory, float> LoadByCategory;
        public float MaxLoad;
        public float MinLoad;
        public float AverageLoad;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobTelemetryData
    {
        public Dictionary<string, object> Metrics;
        public DateTime Timestamp;
        public TimeSpan CollectionDuration;
    }

    public struct ThreadAffinityJobErrorSeverity
    {
        public Dictionary<JobCategory, Dictionary<JobErrorLevel, int>> SeverityByCategory;
        public JobCategory WorstCategory;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobPerformanceTrend
    {
        public List<ThreadAffinityJobPerformanceSample> Samples;
        public JobTrendDirection Direction;
        public DateTime ReportTime;
    }

    public struct ThreadAffinityJobPerformanceSample
    {
        public float CPUTimeMs;
        public long MemoryUsageBytes;
        public int ActiveJobs;
        public DateTime Timestamp;
    }

    // Structures pour le profiler enrichi
    public struct JobSystemProfilerConfig
    {
        public bool IsProfiling;
        public string CurrentSession;
        public Dictionary<string, JobProfilerData> ProfileData;
    }

    public struct JobSystemProfilerHeatmap
    {
        public Dictionary<int, float> LoadByThread;
        public Dictionary<JobCategory, float> LoadByCategory;
        public float MaxLoad;
        public float MinLoad;
        public float AverageLoad;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerTrend
    {
        public List<JobSystemProfilerSample> Samples;
        public JobTrendDirection Direction;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerSample
    {
        public float CPUTimeMs;
        public long MemoryUsageBytes;
        public int ActiveJobs;
        public DateTime Timestamp;
    }

    public struct JobSystemProfilerBudget
    {
        public float TotalBudgetMs;
        public float UsedTimeMs;
        public float RemainingTimeMs;
        public bool IsOverBudget;
        public Dictionary<string, float> CategoryBudgets;
        public Dictionary<string, float> CategoryUsage;
    }

    public struct JobSystemProfilerErrorReport
    {
        public List<JobSystemProfilerErrorEntry> Errors;
        public int ErrorCount;
        public Dictionary<JobErrorLevel, int> ErrorsByLevel;
        public Dictionary<JobCategory, int> ErrorsByCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerErrorEntry
    {
        public DateTime Timestamp;
        public JobErrorLevel Level;
        public string Message;
        public Exception Exception;
        public string StackTrace;
        public JobHandle JobHandle;
        public JobCategory Category;
    }

    public struct JobSystemProfilerPerformanceData
    {
        public JobCategory Category;
        public float AverageCPUTimeMs;
        public float MaxCPUTimeMs;
        public long JobsExecuted;
        public long JobsFailed;
        public long MemoryUsageBytes;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerProfilerZone
    {
        public string ZoneName;
        public TimeSpan CPUTimeMs;
        public int CallCount;
        public float AverageTimeMs;
        public float MaxTimeMs;
        public float MinTimeMs;
    }

    public struct JobSystemProfilerVersionInfo
    {
        public string Version;
        public string BuildNumber;
        public DateTime BuildDate;
    }

    public struct JobSystemProfilerBuildMetadata
    {
        public string Platform;
        public string Configuration;
        public string CompilerVersion;
        public List<string> Features;
    }

    public struct JobSystemProfilerLockStatus
    {
        public List<JobSystemProfilerLockInfo> ActiveLocks;
        public List<JobSystemProfilerLockInfo> ContendedLocks;
        public Dictionary<string, float> AverageWaitTimeByLock;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerLockInfo
    {
        public string Name;
        public int OwnerThread;
        public int WaitingThreads;
        public float HoldTimeMs;
    }

    public struct JobSystemProfilerCacheUsage
    {
        public float HitRate;
        public float MissRate;
        public int EntryCount;
        public int MaxSize;
        public long UsedSizeBytes;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerMemoryUsage
    {
        public long UsageBytes;
        public Dictionary<string, long> UsageByComponent;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobCount
    {
        public int Count;
        public Dictionary<JobCategory, int> CountByCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobLatency
    {
        public Dictionary<JobHandle, TimeSpan> Latencies;
        public TimeSpan AverageLatency;
        public TimeSpan MaxLatency;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobThroughput
    {
        public float JobsPerSecond;
        public float PeakJobsPerSecond;
        public long TotalJobsProcessed;
        public Dictionary<JobCategory, float> ThroughputByCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobFailureRate
    {
        public float FailureRate;
        public int TotalFailures;
        public Dictionary<JobCategory, int> FailuresByCategory;
        public Dictionary<string, int> FailuresByErrorType;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobRetryCount
    {
        public float AverageRetries;
        public int MaxRetries;
        public long TotalRetries;
        public float RetrySuccessRate;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobPriorityDistribution
    {
        public Dictionary<JobPriority, int> CountByPriority;
        public JobPriority DominantPriority;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobExecutionTime
    {
        public Dictionary<JobHandle, TimeSpan> ExecutionTimes;
        public TimeSpan AverageExecutionTime;
        public TimeSpan MaxExecutionTime;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobDependencyGraph
    {
        public Dictionary<JobHandle, List<JobHandle>> Adjacency;
        public List<JobHandle> ExecutionOrder;
        public bool HasCycle;
        public int NodeCount;
        public int EdgeCount;
    }

    public struct JobSystemProfilerJobHeatmap
    {
        public Dictionary<int, float> LoadByThread;
        public Dictionary<JobCategory, float> LoadByCategory;
        public float MaxLoad;
        public float MinLoad;
        public float AverageLoad;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobTelemetryData
    {
        public Dictionary<string, object> Metrics;
        public DateTime Timestamp;
        public TimeSpan CollectionDuration;
    }

    public struct JobSystemProfilerJobErrorSeverity
    {
        public Dictionary<JobCategory, Dictionary<JobErrorLevel, int>> SeverityByCategory;
        public JobCategory WorstCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobPerformanceTrend
    {
        public List<JobSystemProfilerJobPerformanceSample> Samples;
        public JobTrendDirection Direction;
        public DateTime ReportTime;
    }

    public struct JobSystemProfilerJobPerformanceSample
    {
        public float CPUTimeMs;
        public long MemoryUsageBytes;
        public int ActiveJobs;
        public DateTime Timestamp;
    }

    // Structures pour la sécurité enrichie
    public struct JobSystemSecurityConfig
    {
        public bool EnableSandboxing;
        public bool EnableValidation;
        public bool EnableMonitoring;
        public Dictionary<string, object> SecuritySettings;
    }

    public struct JobSystemSecurityHeatmap
    {
        public Dictionary<int, float> LoadByThread;
        public Dictionary<JobCategory, float> LoadByCategory;
        public float MaxLoad;
        public float MinLoad;
        public float AverageLoad;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityTrend
    {
        public List<JobSystemSecuritySample> Samples;
        public JobTrendDirection Direction;
        public DateTime ReportTime;
    }

    public struct JobSystemSecuritySample
    {
        public float CPUTimeMs;
        public long MemoryUsageBytes;
        public int ActiveJobs;
        public DateTime Timestamp;
    }

    public struct JobSystemSecurityBudget
    {
        public float TotalBudgetMs;
        public float UsedTimeMs;
        public float RemainingTimeMs;
        public bool IsOverBudget;
        public Dictionary<string, float> CategoryBudgets;
        public Dictionary<string, float> CategoryUsage;
    }

    public struct JobSystemSecurityErrorReport
    {
        public List<JobSystemSecurityErrorEntry> Errors;
        public int ErrorCount;
        public Dictionary<JobErrorLevel, int> ErrorsByLevel;
        public Dictionary<JobCategory, int> ErrorsByCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityErrorEntry
    {
        public DateTime Timestamp;
        public JobErrorLevel Level;
        public string Message;
        public Exception Exception;
        public string StackTrace;
        public JobHandle JobHandle;
        public JobCategory Category;
    }

    public struct JobSystemSecurityTelemetryData
    {
        public Dictionary<string, object> Metrics;
        public DateTime Timestamp;
        public TimeSpan CollectionDuration;
    }

    public struct JobSystemSecurityPerformanceData
    {
        public JobCategory Category;
        public float AverageCPUTimeMs;
        public float MaxCPUTimeMs;
        public long JobsExecuted;
        public long JobsFailed;
        public long MemoryUsageBytes;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityProfilerZone
    {
        public string ZoneName;
        public TimeSpan CPUTimeMs;
        public int CallCount;
        public float AverageTimeMs;
        public float MaxTimeMs;
        public float MinTimeMs;
    }

    public struct JobSystemSecurityVersionInfo
    {
        public string Version;
        public string BuildNumber;
        public DateTime BuildDate;
    }

    public struct JobSystemSecurityBuildMetadata
    {
        public string Platform;
        public string Configuration;
        public string CompilerVersion;
        public List<string> Features;
    }

    public struct JobSystemSecurityLockStatus
    {
        public List<JobSystemSecurityLockInfo> ActiveLocks;
        public List<JobSystemSecurityLockInfo> ContendedLocks;
        public Dictionary<string, float> AverageWaitTimeByLock;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityLockInfo
    {
        public string Name;
        public int OwnerThread;
        public int WaitingThreads;
        public float HoldTimeMs;
    }

    public struct JobSystemSecurityCacheUsage
    {
        public float HitRate;
        public float MissRate;
        public int EntryCount;
        public int MaxSize;
        public long UsedSizeBytes;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityMemoryUsage
    {
        public long UsageBytes;
        public Dictionary<string, long> UsageByComponent;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobCount
    {
        public int Count;
        public Dictionary<JobCategory, int> CountByCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobLatency
    {
        public Dictionary<JobHandle, TimeSpan> Latencies;
        public TimeSpan AverageLatency;
        public TimeSpan MaxLatency;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobThroughput
    {
        public float JobsPerSecond;
        public float PeakJobsPerSecond;
        public long TotalJobsProcessed;
        public Dictionary<JobCategory, float> ThroughputByCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobFailureRate
    {
        public float FailureRate;
        public int TotalFailures;
        public Dictionary<JobCategory, int> FailuresByCategory;
        public Dictionary<string, int> FailuresByErrorType;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobRetryCount
    {
        public float AverageRetries;
        public int MaxRetries;
        public long TotalRetries;
        public float RetrySuccessRate;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobPriorityDistribution
    {
        public Dictionary<JobPriority, int> CountByPriority;
        public JobPriority DominantPriority;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobExecutionTime
    {
        public Dictionary<JobHandle, TimeSpan> ExecutionTimes;
        public TimeSpan AverageExecutionTime;
        public TimeSpan MaxExecutionTime;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobDependencyGraph
    {
        public Dictionary<JobHandle, List<JobHandle>> Adjacency;
        public List<JobHandle> ExecutionOrder;
        public bool HasCycle;
        public int NodeCount;
        public int EdgeCount;
    }

    public struct JobSystemSecurityJobHeatmap
    {
        public Dictionary<int, float> LoadByThread;
        public Dictionary<JobCategory, float> LoadByCategory;
        public float MaxLoad;
        public float MinLoad;
        public float AverageLoad;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobTelemetryData
    {
        public Dictionary<string, object> Metrics;
        public DateTime Timestamp;
        public TimeSpan CollectionDuration;
    }

    public struct JobSystemSecurityJobErrorSeverity
    {
        public Dictionary<JobCategory, Dictionary<JobErrorLevel, int>> SeverityByCategory;
        public JobCategory WorstCategory;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobPerformanceTrend
    {
        public List<JobSystemSecurityJobPerformanceSample> Samples;
        public JobTrendDirection Direction;
        public DateTime ReportTime;
    }

    public struct JobSystemSecurityJobPerformanceSample
    {
        public float CPUTimeMs;
        public long MemoryUsageBytes;
        public int ActiveJobs;
        public DateTime Timestamp;
    }

    // Structures pour les jobs avec affinité
    public struct ThreadAffinity
    {
        public int ThreadId;
        public int CoreId;
        public float LoadPercentage;
        public bool IsAvailable;
    }

    public struct QoSRequirements
    {
        public float LatencyRequirement;
        public float ThroughputRequirement;
        public float ReliabilityRequirement;
        public float Priority;
    }

    public struct RetryPolicy
    {
        public int MaxRetries;
        public float BackoffMultiplier;
        public TimeSpan InitialDelay;
        public bool EnableExponentialBackoff;
    }

    public struct ServiceLevelAgreement
    {
        public TimeSpan MaximumExecutionTime;
        public float SuccessRateRequirement;
        public float AvailabilityRequirement;
        public float CostConstraint;
    }

    public struct PreemptionPoint
    {
        public string PointName;
        public int PriorityThreshold;
        public bool IsEnabled;
    }

    public struct ValidationResult
    {
        public bool IsValid;
        public List<string> Errors;
        public List<string> Warnings;
        public DateTime ValidationTime;
    }

    public struct JobSecurityContext
    {
        public string UserId;
        public string ProcessId;
        public List<string> Permissions;
        public bool IsSandboxed;
    }

    public struct JobSandboxConfig
    {
        public bool EnableMemoryIsolation;
        public bool EnableFileAccessRestriction;
        public bool EnableNetworkAccessRestriction;
        public long MaxMemoryUsage;
        public TimeSpan MaxExecutionTime;
    }

    public struct JobMemoryUsageReport
    {
        public long UsageBytes;
        public Dictionary<string, long> UsageByComponent;
        public DateTime ReportTime;
    }

    #endregion

    #region Attributs

    /// <summary>
    /// Indique que la méthode est thread-safe.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ThreadSafeAttribute : Attribute { }

    /// <summary>
    /// Indique que la méthode est asynchrone.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AsyncAttribute : Attribute { }

    /// <summary>
    /// Indique que la méthode est sur le chemin critique des performances.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CriticalPathAttribute : Attribute { }

    #endregion
}