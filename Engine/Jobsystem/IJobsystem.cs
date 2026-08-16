using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Core
{
    /// <summary>
    /// Interface principale du système de jobs parallèles AAA du moteur Snake2000.
    /// Respecte les règles d'architecture : Engine → Systems → Components → Utilities.
    /// </summary>
    /// <remarks>
    /// Ce système est le cœur de l'orchestration multithread du moteur.
    /// Il est conçu pour être thread-safe, sans allocation dynamique en boucle chaude,
    /// et interopérable avec tous les sous-systèmes (Animation, Physics, AI, Rendering).
    /// 
    /// Priorité n°1 dans la feuille de route (Fichiers à améliorer.txt).
    /// Prérequis pour : ThreadAffinityManager, Parallel Animation Jobs, GPUProfilerHook.
    /// </remarks>
    public interface IJobSystem
    {
        // ═══════════════════════════════════════════════════════════════
        // ⚙️ 1. ARCHITECTURE & CYCLE DE VIE (25 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialise le système de jobs.
        /// </summary>
        /// <remarks>
        /// Crée le pool de threads, configure les files d'attente et alloue les ressources.
        /// Doit être appelée avant toute planification de job.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Si le système est déjà initialisé.</exception>
        void Initialize();

        /// <summary>
        /// Initialise le système de jobs avec une configuration personnalisée.
        /// </summary>
        /// <param name="config">Configuration du système de jobs</param>
        /// <exception cref="ArgumentNullException">Si la configuration est null.</exception>
        void Initialize(JobSystemConfig config);

        /// <summary>
        /// Initialise le système de jobs en mode asynchrone.
        /// </summary>
        /// <returns>Tâche d'initialisation</returns>
        [Async]
        Task InitializeAsync();

        /// <summary>
        /// Met à jour le système de jobs à chaque frame.
        /// </summary>
        /// <param name="deltaTime">Temps écoulé depuis la dernière frame</param>
        /// <remarks>
        /// Distribue les jobs en attente aux threads disponibles,
        /// nettoie les jobs terminés et met à jour les statistiques.
        /// </remarks>
        [CriticalPath]
        void Update(float deltaTime);

        /// <summary>
        /// Arrête et nettoie le système de jobs.
        /// </summary>
        /// <remarks>
        /// Attend la complétion de tous les jobs en cours,
        /// libère les threads et les ressources allouées.
        /// </remarks>
        void Shutdown();

        /// <summary>
        /// Arrête et nettoie le système de jobs en mode asynchrone.
        /// </summary>
        /// <returns>Tâche de shutdown</returns>
        [Async]
        Task ShutdownAsync();

        /// <summary>
        /// Redémarre le système de jobs sans redémarrage global du moteur.
        /// </summary>
        /// <remarks>
        /// Effectue un Shutdown() suivi d'un Initialize() avec la configuration actuelle.
        /// </remarks>
        void Restart();

        /// <summary>
        /// Suspend temporairement la planification des jobs.
        /// </summary>
        /// <remarks>
        /// Les jobs en cours continuent à s'exécuter, mais aucun nouveau job n'est planifié.
        /// </remarks>
        void Pause();

        /// <summary>
        /// Reprend la planification des jobs après une pause.
        /// </summary>
        void Resume();

        /// <summary>
        /// Réinitialise le système de jobs sans le réinitialiser complètement.
        /// </summary>
        /// <remarks>
        /// Utile pour le hot-reload ou la reconfiguration dynamique.
        /// </remarks>
        void Reset();

        /// <summary>
        /// Recharge la configuration sans redémarrer le système.
        /// </summary>
        void ReloadConfiguration();

        /// <summary>
        /// Recharge la configuration du système sans redémarrer.
        /// </summary>
        void Reload();

        /// <summary>
        /// Obtient l'état actuel du système de jobs.
        /// </summary>
        /// <returns>État du système</returns>
        [ThreadSafe]
        JobSystemState GetState();

        /// <summary>
        /// Vérifie si le système de jobs est initialisé et opérationnel.
        /// </summary>
        /// <returns>True si le système est prêt</returns>
        [ThreadSafe]
        bool IsReady();

        /// <summary>
        /// Vérifie si le système de jobs est en cours d'exécution.
        /// </summary>
        /// <returns>True si des jobs sont actifs</returns>
        [ThreadSafe]
        bool IsRunning();

        /// <summary>
        /// Vérifie si le système de jobs est initialisé.
        /// </summary>
        /// <returns>True si le système est initialisé</returns>
        [ThreadSafe]
        bool IsInitialized();

        /// <summary>
        /// Obtient le nombre total de jobs en cours d'exécution ou en attente.
        /// </summary>
        /// <returns>Nombre total de jobs</returns>
        [ThreadSafe]
        int GetJobCount();

        /// <summary>
        /// Obtient le nombre de jobs complétés depuis l'initialisation.
        /// </summary>
        /// <returns>Nombre de jobs complétés</returns>
        [ThreadSafe]
        long GetCompletedJobCount();

        /// <summary>
        /// Obtient le nombre de jobs échoués depuis l'initialisation.
        /// </summary>
        /// <returns>Nombre de jobs échoués</returns>
        [ThreadSafe]
        int GetFailedJobCount();

        /// <summary>
        /// Obtient le nombre de jobs en attente d'exécution.
        /// </summary>
        /// <returns>Nombre de jobs en attente</returns>
        [ThreadSafe]
        int GetPendingJobCount();

        /// <summary>
        /// Obtient la profondeur de la file d'attente du système.
        /// </summary>
        /// <returns>Profondeur de la file</returns>
        [ThreadSafe]
        int GetJobQueueDepth();

        /// <summary>
        /// Obtient la version du système de jobs.
        /// </summary>
        /// <returns>Numéro de version</returns>
        [ThreadSafe]
        string GetJobSystemVersion();

        /// <summary>
        /// Obtient les métadonnées de build du système de jobs.
        /// </summary>
        /// <returns>Métadonnées de build</returns>
        [ThreadSafe]
        BuildMetadata GetJobSystemBuildMetadata();

        /// <summary>
        /// Obtient la configuration d'affinité des threads du système.
        /// </summary>
        /// <returns>Configuration d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityConfig GetJobSystemThreadAffinity();

        /// <summary>
        /// Obtient l'utilisation mémoire du système de jobs.
        /// </summary>
        /// <returns>Utilisation mémoire en octets</returns>
        [ThreadSafe]
        long GetJobSystemMemoryUsage();

        /// <summary>
        /// Obtient l'utilisation du cache du système de jobs.
        /// </summary>
        /// <returns>Utilisation du cache</returns>
        [ThreadSafe]
        JobCacheUsageReport GetJobSystemCacheUsage();

        /// <summary>
        /// Obtient l'utilisation du budget CPU du système de jobs.
        /// </summary>
        /// <returns>Utilisation du budget (0.0 à 1.0)</returns>
        [ThreadSafe]
        float GetJobSystemBudgetUsage();

        /// <summary>
        /// Obtient les données de performance du système de jobs.
        /// </summary>
        /// <returns>Données de performance</returns>
        [ThreadSafe]
        JobSystemProfiler GetJobSystemProfilerData();

        /// <summary>
        /// Obtient les données de télémétrie du système de jobs.
        /// </summary>
        /// <returns>Données de télémétrie</returns>
        [ThreadSafe]
        JobSystemTelemetryData GetJobSystemTelemetryData();

        /// <summary>
        /// Obtient le niveau de gravité des erreurs du système de jobs.
        /// </summary>
        /// <returns>Rapport de gravité</returns>
        [ThreadSafe]
        JobErrorSeverityReport GetJobSystemErrorSeverity();

        /// <summary>
        /// Obtient l'état de récupération du système de jobs.
        /// </summary>
        /// <returns>État de récupération</returns>
        [ThreadSafe]
        JobRecoveryStatus GetJobSystemRecoveryStatus();

        /// <summary>
        /// Obtient l'état de sécurité des threads du système de jobs.
        /// </summary>
        /// <returns>État de sécurité</returns>
        [ThreadSafe]
        ThreadSafetyReport GetJobSystemThreadSafetyStatus();

        /// <summary>
        /// Obtient l'état des verrous du système de jobs.
        /// </summary>
        /// <returns>État des verrous</returns>
        [ThreadSafe]
        JobLockStatusReport GetJobSystemLockStatus();

        /// <summary>
        /// Obtient la tendance des performances du système de jobs.
        /// </summary>
        /// <returns>Tendance des performances</returns>
        [ThreadSafe]
        JobPerformanceTrend GetJobSystemPerformanceTrend();

        /// <summary>
        /// Obtient la heatmap de charge du système de jobs.
        /// </summary>
        /// <returns>Heatmap de charge</returns>
        [ThreadSafe]
        JobSystemHeatmap GetJobSystemHeatmap();

        /// <summary>
        /// Obtient la timeline du système de jobs.
        /// </summary>
        /// <returns>Timeline des jobs</returns>
        [ThreadSafe]
        JobTimelineReport GetJobSystemTimeline();

        /// <summary>
        /// Obtient les diagnostics d'initialisation du système.
        /// </summary>
        /// <returns>Diagnostics d'initialisation</returns>
        [ThreadSafe]
        JobSystemInitializationDiagnostics GetInitializationDiagnostics();

        /// <summary>
        /// Obtient les diagnostics de shutdown du système.
        /// </summary>
        /// <returns>Diagnostics de shutdown</returns>
        [ThreadSafe]
        JobSystemShutdownDiagnostics GetShutdownDiagnostics();

        /// <summary>
        /// Obtient l'état de cycle de vie du système.
        /// </summary>
        /// <returns>État de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleState GetLifecycleState();

        /// <summary>
        /// Obtient la télémétrie de cycle de vie du système.
        /// </summary>
        /// <returns>Télémétrie de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleTelemetry GetLifecycleTelemetry();

        /// <summary>
        /// Obtient le profil de démarrage du système.
        /// </summary>
        /// <returns>Profil de démarrage</returns>
        [ThreadSafe]
        JobSystemStartupProfile GetStartupProfile();

        /// <summary>
        /// Obtient le profil d'arrêt du système.
        /// </summary>
        /// <returns>Profil d'arrêt</returns>
        [ThreadSafe]
        JobSystemShutdownProfile GetShutdownProfile();

        /// <summary>
        /// Obtient la heatmap de cycle de vie du système.
        /// </summary>
        /// <returns>Heatmap de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleHeatmap GetLifecycleHeatmap();

        /// <summary>
        /// Obtient la tendance de cycle de vie du système.
        /// </summary>
        /// <returns>Tendance de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleTrend GetLifecycleTrend();

        /// <summary>
        /// Obtient le budget de cycle de vie du système.
        /// </summary>
        /// <returns>Budget de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleBudget GetLifecycleBudget();

        /// <summary>
        /// Obtient le rapport d'erreurs de cycle de vie du système.
        /// </summary>
        /// <returns>Rapport d'erreurs de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleErrorReport GetLifecycleErrorReport();

        /// <summary>
        /// Obtient les données de performance de cycle de vie du système.
        /// </summary>
        /// <returns>Données de performance de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecyclePerformanceData GetLifecyclePerformanceData();

        /// <summary>
        /// Obtient la timeline de cycle de vie du système.
        /// </summary>
        /// <returns>Timeline de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleTimeline GetLifecycleTimeline();

        /// <summary>
        /// Obtient l'historique des événements de cycle de vie du système.
        /// </summary>
        /// <returns>Historique des événements de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleEventHistory GetLifecycleEventHistory();

        /// <summary>
        /// Obtient le graphe de dépendances de cycle de vie du système.
        /// </summary>
        /// <returns>Graphe de dépendances de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleDependencyGraph GetLifecycleDependencyGraph();

        /// <summary>
        /// Obtient la zone de profilage de cycle de vie du système.
        /// </summary>
        /// <returns>Zone de profilage de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleProfilerZone GetLifecycleProfilerZone();

        /// <summary>
        /// Obtient les données de télémétrie de cycle de vie du système.
        /// </summary>
        /// <returns>Données de télémétrie de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleTelemetryData GetLifecycleTelemetryData();

        /// <summary>
        /// Obtient les informations de version de cycle de vie du système.
        /// </summary>
        /// <returns>Informations de version de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleVersionInfo GetLifecycleVersionInfo();

        /// <summary>
        /// Obtient les métadonnées de build de cycle de vie du système.
        /// </summary>
        /// <returns>Métadonnées de build de cycle de vie</returns>
        [ThreadSafe]
        JobSystemLifecycleBuildMetadata GetLifecycleBuildMetadata();

        // Événements de cycle de vie
        event Action OnInitializeCompleted;
        event Action OnShutdownCompleted;
        event Action<Exception> OnJobSystemCrashed;

        // ═══════════════════════════════════════════════════════════════
        // 🧩 2. GESTION DES JOBS (50 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Planifie un job pour exécution.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <returns>Handle du job pour suivi et attente</returns>
        /// <remarks>
        /// Le job est ajouté à la file d'attente avec la priorité par défaut.
        /// Thread-safe. Peut être appelé depuis n'importe quel thread.
        /// </remarks>
        [ThreadSafe]
        [CriticalPath]
        JobHandle ScheduleJob(IJob job);

        /// <summary>
        /// Planifie un job avec une priorité spécifique.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="priority">Priorité d'exécution</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        [CriticalPath]
        JobHandle ScheduleJob(IJob job, JobPriority priority);

        /// <summary>
        /// Planifie un job avec des dépendances.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="dependencies">Handles des jobs dont dépend ce job</param>
        /// <returns>Handle du job</returns>
        /// <remarks>
        /// Le job ne sera pas exécuté tant que toutes les dépendances ne sont pas complétées.
        /// </remarks>
        [ThreadSafe]
        JobHandle ScheduleJob(IJob job, ReadOnlySpan<JobHandle> dependencies);

        /// <summary>
        /// Planifie un job avec une priorité et des dépendances.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="priority">Priorité d'exécution</param>
        /// <param name="dependencies">Handles des jobs dont dépend ce job</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJob(IJob job, JobPriority priority, ReadOnlySpan<JobHandle> dependencies);

        /// <summary>
        /// Planifie un job en mode asynchrone.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <returns>Tâche représentant la complétion du job</returns>
        [Async]
        [ThreadSafe]
        Task<JobResult> ScheduleJobAsync(IJob job);

        /// <summary>
        /// Planifie un job générique pour exécution.
        /// </summary>
        /// <typeparam name="T">Type du job</typeparam>
        /// <param name="job">Job à exécuter</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJob<T>(T job) where T : IJob;

        /// <summary>
        /// Planifie un batch de jobs pour exécution groupée.
        /// </summary>
        /// <param name="jobs">Liste des jobs à planifier</param>
        /// <returns>Handles des jobs planifiés</returns>
        /// <remarks>
        /// Optimise l'overhead de planification en regroupant les soumissions.
        /// </remarks>
        [ThreadSafe]
        List<JobHandle> ScheduleBatch(IReadOnlyList<IJob> jobs);

        /// <summary>
        /// Planifie un batch de jobs avec une priorité commune.
        /// </summary>
        /// <param name="jobs">Liste des jobs</param>
        /// <param name="priority">Priorité commune</param>
        /// <returns>Handles des jobs</returns>
        [ThreadSafe]
        List<JobHandle> ScheduleBatch(IReadOnlyList<IJob> jobs, JobPriority priority);

        /// <summary>
        /// Planifie un batch de jobs avec des priorités individuelles.
        /// </summary>
        /// <param name="jobs">Jobs avec leurs priorités</param>
        /// <returns>Handles des jobs</returns>
        [ThreadSafe]
        List<JobHandle> ScheduleBatch(IReadOnlyList<(IJob job, JobPriority priority)> jobs);

        /// <summary>
        /// Planifie un batch de jobs avec des dépendances globales.
        /// </summary>
        /// <param name="jobs">Liste des jobs</param>
        /// <param name="globalDependencies">Handles des jobs dont tous les jobs dépendent</param>
        /// <returns>Handles des jobs</returns>
        [ThreadSafe]
        List<JobHandle> ScheduleBatch(IReadOnlyList<IJob> jobs, ReadOnlySpan<JobHandle> globalDependencies);

        /// <summary>
        /// Planifie un ParallelFor sur un ensemble d'indices.
        /// </summary>
        /// <param name="count">Nombre d'itérations</param>
        /// <param name="job">Job à exécuter pour chaque indice</param>
        /// <returns>Handle du job combiné</returns>
        /// <remarks>
        /// Divise automatiquement le travail entre les threads disponibles.
        /// Utilisé pour le skinning, les particules, le pathfinding, etc.
        /// </remarks>
        [ThreadSafe]
        [CriticalPath]
        JobHandle ScheduleParallelFor(int count, IParallelForJob job);

        /// <summary>
        /// Planifie un ParallelFor avec une taille de batch personnalisée.
        /// </summary>
        /// <param name="count">Nombre d'itérations</param>
        /// <param name="innerLoopBatchCount">Taille de chaque batch</param>
        /// <param name="job">Job à exécuter</param>
        /// <returns>Handle du job combiné</returns>
        [ThreadSafe]
        JobHandle ScheduleParallelFor(int count, int innerLoopBatchCount, IParallelForJob job);

        /// <summary>
        /// Planifie un job de réduction (map-reduce).
        /// </summary>
        /// <param name="count">Nombre d'éléments</param>
        /// <param name="job">Job de réduction</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleReduce(int count, IReduceJob job);

        /// <summary>
        /// Annule un job en attente.
        /// </summary>
        /// <param name="handle">Handle du job à annuler</param>
        /// <returns>True si le job a été annulé avant son exécution</returns>
        /// <remarks>
        /// Si le job est déjà en cours d'exécution, il ne peut pas être annulé.
        /// </remarks>
        [ThreadSafe]
        bool CancelJob(JobHandle handle);

        /// <summary>
        /// Annule tous les jobs en attente d'une catégorie.
        /// </summary>
        /// <param name="category">Catégorie des jobs à annuler</param>
        /// <returns>Nombre de jobs annulés</returns>
        [ThreadSafe]
        int CancelJobsByCategory(JobCategory category);

        /// <summary>
        /// Annule tous les jobs en attente.
        /// </summary>
        /// <returns>Nombre de jobs annulés</returns>
        [ThreadSafe]
        int CancelAllJobs();

        /// <summary>
        /// Attend la complétion d'un job spécifique.
        /// </summary>
        /// <param name="handle">Handle du job à attendre</param>
        /// <remarks>
        /// Bloque le thread appelant jusqu'à la complétion du job.
        /// À éviter sur le thread principal ; préférer ScheduleJobAsync.
        /// </remarks>
        [ThreadSafe]
        void WaitForJob(JobHandle handle);

        /// <summary>
        /// Attend la complétion d'un job avec un timeout.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <param name="timeout">Durée maximale d'attente</param>
        /// <returns>True si le job est complété avant le timeout</returns>
        [ThreadSafe]
        bool WaitForJob(JobHandle handle, TimeSpan timeout);

        /// <summary>
        /// Attend la complétion de plusieurs jobs.
        /// </summary>
        /// <param name="handles">Handles des jobs à attendre</param>
        [ThreadSafe]
        void WaitForAllJobs(ReadOnlySpan<JobHandle> handles);

        /// <summary>
        /// Attend la complétion de tous les jobs en cours.
        /// </summary>
        /// <remarks>
        /// Barrière globale. Utilisé en fin de frame ou avant un shutdown.
        /// </remarks>
        [ThreadSafe]
        void WaitForAllJobs();

        /// <summary>
        /// Attend la complétion de tous les jobs en mode asynchrone.
        /// </summary>
        /// <returns>Tâche d'attente</returns>
        [Async]
        [ThreadSafe]
        Task WaitForAllJobsAsync();

        /// <summary>
        /// Obtient le statut d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Statut actuel du job</returns>
        [ThreadSafe]
        JobStatus GetJobStatus(JobHandle handle);

        /// <summary>
        /// Obtient le résultat d'un job terminé.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Résultat du job</returns>
        /// <exception cref="InvalidOperationException">Si le job n'est pas terminé.</exception>
        [ThreadSafe]
        JobResult GetJobResult(JobHandle handle);

        /// <summary>
        /// Obtient le résultat typé d'un job terminé.
        /// </summary>
        /// <typeparam name="T">Type du résultat</typeparam>
        /// <param name="handle">Handle du job</param>
        /// <returns>Résultat typé du job</returns>
        [ThreadSafe]
        T GetJobResult<T>(JobHandle handle);

        /// <summary>
        /// Vérifie si un job est terminé.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>True si le job est complété</returns>
        [ThreadSafe]
        bool IsJobComplete(JobHandle handle);

        /// <summary>
        /// Obtient le temps d'exécution d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Temps d'exécution</returns>
        [ThreadSafe]
        TimeSpan GetJobExecutionTime(JobHandle handle);

        /// <summary>
        /// Obtient la latence d'un job (temps entre planification et début d'exécution).
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Latence du job</returns>
        [ThreadSafe]
        TimeSpan GetJobLatency(JobHandle handle);

        /// <summary>
        /// Obtient le débit global du système de jobs.
        /// </summary>
        /// <returns>Rapport de débit</returns>
        [ThreadSafe]
        JobThroughputReport GetJobThroughput();

        /// <summary>
        /// Obtient le taux d'échec des jobs.
        /// </summary>
        /// <returns>Rapport d'échec</returns>
        [ThreadSafe]
        JobFailureReport GetJobFailureRate();

        /// <summary>
        /// Obtient le nombre de tentatives de retry des jobs.
        /// </summary>
        /// <returns>Rapport de retry</returns>
        [ThreadSafe]
        JobRetryReport GetJobRetryCount();

        /// <summary>
        /// Obtient la priorité d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Priorité du job</returns>
        [ThreadSafe]
        JobPriority GetJobPriority(JobHandle handle);

        /// <summary>
        /// Définit la priorité d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <param name="priority">Nouvelle priorité</param>
        /// <returns>True si la priorité a été modifiée</returns>
        [ThreadSafe]
        bool SetJobPriority(JobHandle handle, JobPriority priority);

        /// <summary>
        /// Obtient les dépendances d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Liste des handles des jobs dont dépend ce job</returns>
        [ThreadSafe]
        List<JobHandle> GetJobDependencies(JobHandle handle);

        /// <summary>
        /// Obtient l'utilisation mémoire d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Utilisation mémoire en octets</returns>
        [ThreadSafe]
        long GetJobMemoryUsage(JobHandle handle);

        /// <summary>
        /// Obtient le taux de cache hit d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Taux de cache hit (0.0 à 1.0)</returns>
        [ThreadSafe]
        float GetJobCacheHitRate(JobHandle handle);

        /// <summary>
        /// Obtient l'utilisation du budget CPU d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Utilisation du budget (0.0 à 1.0)</returns>
        [ThreadSafe]
        float GetJobBudgetUsage(JobHandle handle);

        /// <summary>
        /// Obtient la heatmap de charge d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Heatmap de charge</returns>
        [ThreadSafe]
        JobHeatmapReport GetJobHeatmap(JobHandle handle);

        /// <summary>
        /// Obtient l'affinité des threads d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Affinité des threads</returns>
        [ThreadSafe]
        ThreadAffinityConfig GetJobThreadAffinity(JobHandle handle);

        /// <summary>
        /// Obtient les données de télémétrie d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Données de télémétrie</returns>
        [ThreadSafe]
        JobTelemetryData GetJobTelemetryData(JobHandle handle);

        /// <summary>
        /// Obtient le niveau de gravité des erreurs d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Niveau de gravité</returns>
        [ThreadSafe]
        JobErrorLevel GetJobErrorSeverity(JobHandle handle);

        // Nouvelles méthodes de gestion des jobs (25 idées inédites)
        /// <summary>
        /// Planifie un job avec une deadline.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="deadline">Deadline d'exécution</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithDeadline(IJob job, TimeSpan deadline);

        /// <summary>
        /// Planifie un job avec un budget CPU.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="cpuBudget">Budget CPU en ms</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithBudget(IJob job, float cpuBudget);

        /// <summary>
        /// Planifie un job avec une affinité de thread.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="affinity">Affinité de thread</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithAffinity(IJob job, ThreadAffinity affinity);

        /// <summary>
        /// Planifie un job avec des exigences QoS.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="qos">Exigences QoS</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithQoS(IJob job, QoSRequirements qos);

        /// <summary>
        /// Planifie un job avec une politique de retry.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="policy">Politique de retry</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithRetryPolicy(IJob job, RetryPolicy policy);

        /// <summary>
        /// Planifie un job avec un SLA.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="sla">Service Level Agreement</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithSLA(IJob job, ServiceLevelAgreement sla);

        /// <summary>
        /// Planifie un job avec un point de préemption.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="point">Point de préemption</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithPreemption(IJob job, PreemptionPoint point);

        /// <summary>
        /// Planifie un job avec une priorité spécifique.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="priority">Priorité</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithPriority(IJob job, JobPriority priority);

        /// <summary>
        /// Planifie un job avec des dépendances.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="deps">Dépendances</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithDependencies(IJob job, IEnumerable<JobHandle> deps);

        /// <summary>
        /// Planifie un job avec des données de télémétrie.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="telemetry">Données de télémétrie</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithTelemetry(IJob job, JobTelemetryData telemetry);

        /// <summary>
        /// Planifie un job avec un suivi de budget.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="report">Rapport de suivi de budget</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithBudgetTracking(IJob job, JobBudgetTrendReport report);

        /// <summary>
        /// Planifie un job avec un suivi de heatmap.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="report">Rapport de suivi de heatmap</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithHeatmapTracking(IJob job, JobHeatmapReport report);

        /// <summary>
        /// Planifie un job avec un suivi d'erreurs.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="report">Rapport de suivi d'erreurs</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithErrorTracking(IJob job, JobErrorFrequencyReport report);

        /// <summary>
        /// Planifie un job avec un suivi de cache.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="report">Rapport de suivi de cache</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithCacheTracking(IJob job, JobCacheUsageReport report);

        /// <summary>
        /// Planifie un job avec un suivi de mémoire.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="report">Rapport de suivi de mémoire</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithMemoryTracking(IJob job, JobMemoryUsageReport report);

        /// <summary>
        /// Planifie un job avec un suivi de thread.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="telemetry">Télémétrie de thread</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithThreadTracking(IJob job, ThreadTelemetryData telemetry);

        /// <summary>
        /// Planifie un job avec un suivi de performance.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="trend">Tendance de performance</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithPerformanceTracking(IJob job, JobPerformanceTrend trend);

        /// <summary>
        /// Planifie un job avec une validation.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="validation">Résultat de validation</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithValidation(IJob job, ValidationResult validation);

        /// <summary>
        /// Planifie un job avec un contexte de sécurité.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="context">Contexte de sécurité</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithSecurity(IJob job, JobSecurityContext context);

        /// <summary>
        /// Planifie un job avec une configuration de sandbox.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="config">Configuration de sandbox</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithSandbox(IJob job, JobSandboxConfig config);

        /// <summary>
        /// Planifie un job avec un nombre de retry.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="retryCount">Nombre de retry</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithRetry(IJob job, int retryCount);

        /// <summary>
        /// Planifie un job avec un timeout.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="timeout">Timeout</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithTimeout(IJob job, TimeSpan timeout);

        /// <summary>
        /// Planifie un job avec un token d'annulation.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="token">Token d'annulation</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithCancellation(IJob job, CancellationToken token);

        /// <summary>
        /// Planifie un job avec une continuation.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="continuation">Action de continuation</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithContinuation(IJob job, Action<JobResult> continuation);

        /// <summary>
        /// Planifie un job avec un fallback.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="fallbackJob">Job de fallback</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithFallback(IJob job, IJob fallbackJob);

        // ═══════════════════════════════════════════════════════════════
        // 🧩 1. OPTIMISATION RUNTIME (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Active le profilage adaptatif pour ajuster le nombre de threads selon la charge.
        /// </summary>
        /// <param name="enabled">État du profilage adaptatif</param>
        void EnableAdaptiveProfiling(bool enabled);

        /// <summary>
        /// Obtient le nombre optimal de threads selon la charge observée.
        /// </summary>
        /// <returns>Nombre optimal de threads</returns>
        [ThreadSafe]
        int GetOptimalThreadCount();

        /// <summary>
        /// Planifie un job avec le scheduler hybride CPU/GPU.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <param name="gpuPreference">Préférence GPU</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobWithHybridScheduler(IJob job, GPUPreference gpuPreference);

        /// <summary>
        /// Active le predictive scheduler basé sur les patterns de frame précédentes.
        /// </summary>
        /// <param name="enabled">État du predictive scheduler</param>
        void EnablePredictiveScheduler(bool enabled);

        /// <summary>
        /// Obtient les prédictions du scheduler pour la frame suivante.
        /// </summary>
        /// <returns>Prédictions du scheduler</returns>
        [ThreadSafe]
        SchedulerPredictionReport GetSchedulerPredictions();

        /// <summary>
        /// Crée un manager de warmup pour pré-initialiser les jobs critiques.
        /// </summary>
        /// <returns>Manager de warmup</returns>
        IJobWarmupManager CreateWarmupManager();

        /// <summary>
        /// Crée un manager de cooling pour libérer les jobs inactifs.
        /// </summary>
        /// <returns>Manager de cooling</returns>
        IJobCoolingManager CreateCoolingManager();

        /// <summary>
        /// Planifie un job en mode burst pour exécution massive.
        /// </summary>
        /// <param name="job">Job à exécuter</param>
        /// <returns>Handle du job</returns>
        [ThreadSafe]
        JobHandle ScheduleJobInBurstMode(IJob job);

        /// <summary>
        /// Obtient le budget adaptatif du système de jobs.
        /// </summary>
        /// <returns>Budget adaptatif</returns>
        [ThreadSafe]
        JobSystemAdaptiveBudget GetAdaptiveBudget();

        /// <summary>
        /// Analyse la frame actuelle pour détecter les surcharges.
        /// </summary>
        /// <returns>Analyse de la frame</returns>
        [ThreadSafe]
        FrameAnalysisReport AnalyzeCurrentFrame();

        /// <summary>
        /// Active l'équilibrage automatique des jobs entre threads.
        /// </summary>
        /// <param name="enabled">État de l'équilibrage</param>
        void EnableAutoBalancing(bool enabled);

        /// <summary>
        /// Active le contrôle thermique pour éviter le throttling CPU.
        /// </summary>
        /// <param name="enabled">État du contrôle thermique</param>
        void EnableThermalControl(bool enabled);

        // ═══════════════════════════════════════════════════════════════
        // ⚙️ 2. SÉCURITÉ MULTITHREAD (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Détecte les verrous inutiles dans le système de jobs.
        /// </summary>
        /// <returns>Rapport de détection</returns>
        [ThreadSafe]
        ThreadLockInspectionReport InspectThreadLocks();

        /// <summary>
        /// Résout les conflits de ressources entre threads.
        /// </summary>
        /// <param name="conflict">Conflit à résoudre</param>
        /// <returns>True si le conflit a été résolu</returns>
        [ThreadSafe]
        bool ResolveThreadConflict(ThreadConflict conflict);

        /// <summary>
        /// Détecte les deadlocks avec rollback automatique.
        /// </summary>
        /// <returns>Rapport de détection</returns>
        [ThreadSafe]
        ThreadDeadlockDetectionReport DetectDeadlocksWithAutoRollback();

        /// <summary>
        /// Valide les files concurrentes pour la sécurité des threads.
        /// </summary>
        /// <returns>Rapport de validation</returns>
        [ThreadSafe]
        ThreadSafeQueueValidationReport ValidateThreadSafeQueues();

        /// <summary>
        /// Analyse les accès simultanés aux ressources partagées.
        /// </summary>
        /// <returns>Rapport d'analyse</returns>
        [ThreadSafe]
        ThreadRaceAnalysisReport AnalyzeThreadRaces();

        /// <summary>
        /// Surveille l'intégrité des threads du système.
        /// </summary>
        /// <returns>Rapport d'intégrité</returns>
        [ThreadSafe]
        ThreadIntegrityReport MonitorThreadIntegrity();

        /// <summary>
        /// Gère la récupération des threads bloqués.
        /// </summary>
        /// <param name="threadId">ID du thread bloqué</param>
        /// <returns>True si la récupération a réussi</returns>
        [ThreadSafe]
        bool RecoverBlockedThread(int threadId);

        /// <summary>
        /// Surveille les threads inactifs avec un watchdog.
        /// </summary>
        /// <returns>Rapport de surveillance</returns>
        [ThreadSafe]
        ThreadWatchdogReport MonitorInactiveThreads();

        /// <summary>
        /// Équilibre les priorités pour éviter les inversions de priorité.
        /// </summary>
        [ThreadSafe]
        void BalanceThreadPriorities();

        /// <summary>
        /// Optimise l'affinité des threads pour la localité mémoire.
        /// </summary>
        [ThreadSafe]
        void OptimizeThreadAffinity();

        // ═══════════════════════════════════════════════════════════════
        // 🧮 3. PROFILAGE & VISUALISATION (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Visualise la timeline des jobs par frame.
        /// </summary>
        /// <returns>Visualiseur de timeline</returns>
        IJobSystemTimelineVisualizer GetTimelineVisualizer();

        /// <summary>
        /// Obtient l'overlay de heatmap de charge CPU.
        /// </summary>
        /// <returns>Overlay de heatmap</returns>
        IJobSystemHeatmapOverlay GetHeatmapOverlay();

        /// <summary>
        /// Suivi des temps d'exécution des jobs.
        /// </summary>
        /// <returns>Graphe de latence</returns>
        IJobSystemLatencyGraph GetLatencyGraph();

        /// <summary>
        /// Exporte le flame graph des jobs.
        /// </summary>
        /// <returns>Exporter de flame graph</returns>
        IJobSystemFlameGraphExporter GetFlameGraphExporter();

        /// <summary>
        /// Affiche les métriques en temps réel dans un HUD.
        /// </summary>
        /// <returns>HUD de performance</returns>
        IJobSystemPerformanceHUD GetPerformanceHUD();

        /// <summary>
        /// Obtient l'overlay de profilage intégré à l'éditeur.
        /// </summary>
        /// <returns>Overlay de profilage</returns>
        IJobSystemProfilerOverlay GetProfilerOverlay();

        /// <summary>
        /// Visualise les budgets CPU/GPU par frame.
        /// </summary>
        /// <returns>Visualiseur de budget</returns>
        IJobSystemFrameBudgetVisualizer GetFrameBudgetVisualizer();

        /// <summary>
        /// Affiche la heatmap des allocations mémoire.
        /// </summary>
        /// <returns>Heatmap de mémoire</returns>
        IJobSystemMemoryHeatmap GetMemoryHeatmap();

        /// <summary>
        /// Affiche la charge par thread.
        /// </summary>
        /// <returns>Graphe de charge</returns>
        IJobSystemThreadLoadGraph GetThreadLoadGraph();

        /// <summary>
        /// Trace les transitions des événements.
        /// </summary>
        /// <returns>Timeline des événements</returns>
        IJobSystemEventTimeline GetEventTimeline();

        // ═══════════════════════════════════════════════════════════════
        // 🧩 4. INTEROPÉRABILITÉ MOTEUR (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Enregistre le hook pour synchroniser les jobs GPU.
        /// </summary>
        void RegisterRenderHook();

        /// <summary>
        /// Enregistre le hook pour les collisions parallèles.
        /// </summary>
        void RegisterPhysicsHook();

        /// <summary>
        /// Enregistre le hook pour le pathfinding multithread.
        /// </summary>
        void RegisterAIHook();

        /// <summary>
        /// Enregistre le hook pour la spatialisation sonore.
        /// </summary>
        void RegisterAudioHook();

        /// <summary>
        /// Enregistre le hook pour le blending parallèle.
        /// </summary>
        void RegisterAnimationHook();

        /// <summary>
        /// Enregistre le hook pour les effets visuels.
        /// </summary>
        void RegisterParticlesHook();

        /// <summary>
        /// Enregistre le hook pour les jobs réseau.
        /// </summary>
        void RegisterNetworkingHook();

        /// <summary>
        /// Enregistre le hook pour les entités massives.
        /// </summary>
        void RegisterECSHook();

        /// <summary>
        /// Enregistre le hook pour la communication inter-modules.
        /// </summary>
        void RegisterEventBusHook();

        /// <summary>
        /// Enregistre le hook pour le monitoring global.
        /// </summary>
        void RegisterProfilerHook();

        // ═══════════════════════════════════════════════════════════════
        // 🧩 5. DEBUG & TESTS (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Active le mode debug pour exécuter les jobs en séquentiel.
        /// </summary>
        /// <param name="enabled">État du mode debug</param>
        void EnableDebugMode(bool enabled);

        /// <summary>
        /// Active le mode replay pour rejouer une frame.
        /// </summary>
        /// <param name="enabled">État du mode replay</param>
        void EnableReplayMode(bool enabled);

        /// <summary>
        /// Exécute la suite de stress test.
        /// </summary>
        /// <param name="config">Configuration du test</param>
        void RunStressTestSuite(StressTestConfig config);

        /// <summary>
        /// Injecte des erreurs pour simuler des conditions extrêmes.
        /// </summary>
        /// <param name="config">Configuration de l'injection</param>
        void InjectChaos(ChaosInjectionConfig config);

        /// <summary>
        /// Teste la robustesse du système avec le fuzzing.
        /// </summary>
        /// <param name="config">Configuration du fuzzing</param>
        void RunFuzzTest(FuzzTestConfig config);

        /// <summary>
        /// Détecte les régressions de performance.
        /// </summary>
        /// <param name="baseline">Référence de baseline</param>
        /// <returns>Rapport de régression</returns>
        PerformanceRegressionReport DetectPerformanceRegressions(PerformanceBaseline baseline);

        /// <summary>
        /// Active le thread sanitizer pour les accès concurrents.
        /// </summary>
        /// <param name="enabled">État du sanitizer</param>
        void EnableThreadSanitizer(bool enabled);

        /// <summary>
        /// Détecte les fuites de mémoire dans le système de jobs.
        /// </summary>
        /// <returns>Rapport de détection</returns>
        MemoryLeakReport DetectMemoryLeaks();

        /// <summary>
        /// Compare les performances entre builds.
        /// </summary>
        /// <param name="buildA">Premier build</param>
        /// <param name="buildB">Second build</param>
        /// <returns>Rapport de comparaison</returns>
        PerformanceComparisonReport CompareBuilds(BuildInfo buildA, BuildInfo buildB);

        /// <summary>
        /// Exécute le framework de tests unitaires pour les jobs.
        /// </summary>
        /// <param name="config">Configuration des tests</param>
        /// <returns>Rapport des tests</returns>
        UnitTestReport RunUnitTestFramework(UnitTestConfig config);

        // ═══════════════════════════════════════════════════════════════
        // 🧠 6. ERGONOMIE DÉVELOPPEUR (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtient l'interface CLI pour inspecter les jobs.
        /// </summary>
        /// <returns>Interface CLI</returns>
        IJobSystemCLI GetCLI();

        /// <summary>
        /// Obtient le dashboard dans l'éditeur.
        /// </summary>
        /// <returns>Dashboard</returns>
        IJobSystemDashboard GetDashboard();

        /// <summary>
        /// Génère une configuration avec un assistant.
        /// </summary>
        /// <returns>Assistant de configuration</returns>
        IJobSystemConfigWizard GetConfigWizard();

        /// <summary>
        /// Obtient la bibliothèque de templates de jobs.
        /// </summary>
        /// <returns>Bibliothèque de templates</returns>
        IJobSystemTemplateLibrary GetTemplateLibrary();

        /// <summary>
        /// Obtient le livre de recettes des jobs.
        /// </summary>
        /// <returns>Livre de recettes</returns>
        IJobSystemCookbook GetCookbook();

        /// <summary>
        /// Expose les métriques via une API.
        /// </summary>
        /// <returns>API de métriques</returns>
        IJobSystemMetricsAPI GetMetricsAPI();

        /// <summary>
        /// Crée un constructeur de graphes de jobs.
        /// </summary>
        /// <returns>Constructeur de graphes</returns>
        IJobSystemGraphBuilder GetGraphBuilder();

        /// <summary>
        /// Valide les contrats des jobs (inputs/outputs).
        /// </summary>
        /// <returns>Validateur de contrats</returns>
        IJobSystemContractValidator GetContractValidator();

        /// <summary>
        /// Limite les ressources via une API sandbox.
        /// </summary>
        /// <returns>API sandbox</returns>
        IJobSystemSandboxAPI GetSandboxAPI();

        /// <summary>
        /// Rejoue les jobs via une API.
        /// </summary>
        /// <returns>API de replay</returns>
        IJobSystemReplayAPI GetReplayAPI();

        // ═══════════════════════════════════════════════════════════════
        // 🧩 7. DISTRIBUTION & CLOUD (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Gère l'exécution sur plusieurs machines.
        /// </summary>
        /// <returns>Manager de cluster</returns>
        IJobSystemClusterManager GetClusterManager();

        /// <summary>
        /// Exécute les jobs distants.
        /// </summary>
        /// <returns>Exécuteur distant</returns>
        IJobSystemRemoteExecutor GetRemoteExecutor();

        /// <summary>
        /// Configure le consensus Raft pour la tolérance aux pannes.
        /// </summary>
        /// <param name="config">Configuration Raft</param>
        void ConfigureRaftConsensus(RaftConfig config);

        /// <summary>
        /// Configure le batch cloud pour le calcul distribué.
        /// </summary>
        /// <param name="config">Configuration du batch</param>
        void ConfigureCloudBatch(CloudBatchConfig config);

        /// <summary>
        /// Supporte les nœuds edge pour les périphériques légers.
        /// </summary>
        /// <param name="config">Configuration des nœuds edge</param>
        void EnableEdgeNodeSupport(EdgeNodeConfig config);

        /// <summary>
        /// Gère le déchargement GPU pour les tâches compute.
        /// </summary>
        /// <returns>Manager de déchargement GPU</returns>
        IJobSystemGPUOffloadManager GetGPUOffloadManager();

        /// <summary>
        /// Planifie les jobs réseau.
        /// </summary>
        /// <returns>Scheduler réseau</returns>
        IJobSystemNetworkScheduler GetNetworkScheduler();

        /// <summary>
        /// Agrège la télémétrie pour les clusters.
        /// </summary>
        /// <returns>Agrégateur de télémétrie</returns>
        IJobSystemTelemetryAggregator GetTelemetryAggregator();

        /// <summary>
        /// Gère les verrous distribués.
        /// </summary>
        /// <returns>Manager de verrous distribués</returns>
        IJobSystemDistributedLockManager GetDistributedLockManager();

        /// <summary>
        /// Planifie les jobs entre régions pour les latences.
        /// </summary>
        /// <returns>Scheduler multi-région</returns>
        IJobSystemCrossRegionScheduler GetCrossRegionScheduler();

        // ═══════════════════════════════════════════════════════════════
        // 🧩 8. EXPÉRIMENTAL & RECHERCHE (10 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Active le scheduler ML pour prédire les durées.
        /// </summary>
        /// <param name="enabled">État du scheduler ML</param>
        void EnableMLScheduler(bool enabled);

        /// <summary>
        /// Active le scheduler par renforcement pour adapter les politiques.
        /// </summary>
        /// <param name="enabled">État du scheduler RL</param>
        void EnableReinforcementScheduler(bool enabled);

        /// <summary>
        /// Préfetche les jobs de manière prédictive.
        /// </summary>
        /// <returns>Préfetcheur prédictif</returns>
        IJobSystemPredictivePrefetcher GetPredictivePrefetcher();

        /// <summary>
        /// Regroupe dynamiquement les jobs.
        /// </summary>
        /// <returns>Regroupeur adaptatif</returns>
        IJobSystemAdaptiveBatcher GetAdaptiveBatcher();

        /// <summary>
        /// Active le mode de calcul approximatif pour les jobs non critiques.
        /// </summary>
        /// <param name="enabled">État du mode approximatif</param>
        void EnableApproximateComputing(bool enabled);

        /// <summary>
        /// Active le scheduler probabiliste pour le temps réel souple.
        /// </summary>
        /// <param name="enabled">État du scheduler probabiliste</param>
        void EnableProbabilisticScheduler(bool enabled);

        /// <summary>
        /// Exécute les jobs de manière spéculative.
        /// </summary>
        /// <param name="enabled">État de l'exécution spéculative</param>
        void EnableSpeculativeExecution(bool enabled);

        /// <summary>
        /// Active le scheduler hybride CPU/GPU.
        /// </summary>
        /// <param name="enabled">État du scheduler hybride</param>
        void EnableHybridScheduler(bool enabled);

        /// <summary>
        /// Optimise la consommation d'énergie.
        /// </summary>
        /// <param name="enabled">État de l'optimisation énergétique</param>
        void EnableEnergyOptimizer(bool enabled);

        /// <summary>
        /// Active le mode auto-réparateur pour corriger les anomalies.
        /// </summary>
        /// <param name="enabled">État du mode auto-réparateur</param>
        void EnableSelfHealingMode(bool enabled);

        // ═══════════════════════════════════════════════════════════════
        // 🧱 3. GESTION DES THREADS (25 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Définit l'affinité des threads du système de jobs.
        /// </summary>
        /// <param name="affinity">Configuration d'affinité</param>
        /// <remarks>
        /// Contrôle quels cœurs CPU sont utilisés par le job system.
        /// Requis pour le Thread Affinity Control mentionné dans les idées avancées.
        /// </remarks>
        [ThreadSafe]
        void SetThreadAffinity(ThreadAffinityConfig affinity);

        /// <summary>
        /// Obtient la configuration d'affinité actuelle.
        /// </summary>
        /// <returns>Configuration d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityConfig GetThreadAffinity();

        /// <summary>
        /// Obtient le nombre de threads workers.
        /// </summary>
        /// <returns>Nombre de threads</returns>
        [ThreadSafe]
        int GetWorkerThreadCount();

        /// <summary>
        /// Obtient le nombre maximum de threads workers.
        /// </summary>
        /// <returns>Nombre maximum de threads</returns>
        [ThreadSafe]
        int GetMaxWorkerThreadCount();

        /// <summary>
        /// Définit le nombre de threads workers.
        /// </summary>
        /// <param name="count">Nombre de threads souhaité</param>
        /// <remarks>
        /// Peut déclencher la création ou la destruction de threads.
        /// </remarks>
        void SetWorkerThreadCount(int count);

        /// <summary>
        /// Obtient la charge de chaque thread worker.
        /// </summary>
        /// <returns>Rapport de charge par thread</returns>
        /// <remarks>
        /// Thread-safe. Fournit la charge CPU de chaque thread du pool.
        /// Utile pour l'équilibrage de charge et le Thread Affinity Control.
        /// </remarks>
        [ThreadSafe]
        ThreadLoadReport GetThreadLoad();

        /// <summary>
        /// Obtient les statistiques du pool de threads.
        /// </summary>
        /// <returns>Statistiques du pool</returns>
        [ThreadSafe]
        ThreadPoolStats GetThreadPoolStats();

        /// <summary>
        /// Obtient le contexte d'un thread worker spécifique.
        /// </summary>
        /// <param name="threadIndex">Index du thread</param>
        /// <returns>Contexte du thread</returns>
        [ThreadSafe]
        WorkerThreadContext GetWorkerThreadContext(int threadIndex);

        /// <summary>
        /// Obtient le temps d'exécution d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Temps d'exécution</returns>
        [ThreadSafe]
        TimeSpan GetThreadExecutionTime(int threadId);

        /// <summary>
        /// Obtient la latence d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Latence du thread</returns>
        [ThreadSafe]
        TimeSpan GetThreadLatency(int threadId);

        /// <summary>
        /// Obtient le débit d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Débit du thread</returns>
        [ThreadSafe]
        float GetThreadThroughput(int threadId);

        /// <summary>
        /// Obtient le taux d'échec d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Taux d'échec du thread</returns>
        [ThreadSafe]
        float GetThreadFailureRate(int threadId);

        /// <summary>
        /// Obtient le nombre de tentatives de retry d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Nombre de retries du thread</returns>
        [ThreadSafe]
        int GetThreadRetryCount(int threadId);

        /// <summary>
        /// Obtient la priorité d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Priorité du thread</returns>
        [ThreadSafe]
        ThreadPriorityLevel GetThreadPriority(int threadId);

        /// <summary>
        /// Définit la priorité d'un thread worker.
        /// </summary>
        /// <param name="threadIndex">Index du thread</param>
        /// <param name="priority">Priorité du thread</param>
        [ThreadSafe]
        void SetWorkerThreadPriority(int threadIndex, ThreadPriorityLevel priority);

        /// <summary>
        /// Obtient les dépendances d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Liste des dépendances du thread</returns>
        [ThreadSafe]
        List<JobHandle> GetThreadDependencies(int threadId);

        /// <summary>
        /// Active ou désactive le work-stealing entre threads.
        /// </summary>
        /// <param name="enabled">État du work-stealing</param>
        /// <remarks>
        /// Le work-stealing permet aux threads inactifs de voler des jobs
        /// aux threads surchargés. Améliore l'équilibrage de charge.
        /// </remarks>
        void SetWorkStealingEnabled(bool enabled);

        /// <summary>
        /// Vérifie si le work-stealing est activé.
        /// </summary>
        /// <returns>True si le work-stealing est actif</returns>
        [ThreadSafe]
        bool IsWorkStealingEnabled();

        /// <summary>
        /// Obtient les statistiques de work-stealing.
        /// </summary>
        /// <returns>Statistiques de vol de travail</returns>
        [ThreadSafe]
        WorkStealingStats GetWorkStealingStats();

        /// <summary>
        /// Affecte une catégorie de jobs à un thread spécifique.
        /// </summary>
        /// <param name="category">Catégorie de jobs</param>
        /// <param name="threadIndex">Index du thread cible</param>
        /// <remarks>
        /// Utilisé pour le Thread Affinity Control : par exemple,
        /// affecter les jobs d'animation au thread 0, la physique au thread 1, etc.
        /// </remarks>
        [ThreadSafe]
        void SetCategoryAffinity(JobCategory category, int threadIndex);

        /// <summary>
        /// Obtient l'affectation de thread d'une catégorie de jobs.
        /// </summary>
        /// <param name="category">Catégorie de jobs</param>
        /// <returns>Index du thread affecté, ou -1 si aucune affectation</returns>
        [ThreadSafe]
        int GetCategoryAffinity(JobCategory category);

        /// <summary>
        /// Obtient la carte d'affinité complète.
        /// </summary>
        /// <returns>Carte d'affinité de toutes les catégories</returns>
        [ThreadSafe]
        CategoryAffinityMap GetCategoryAffinityMap();

        /// <summary>
        /// Suspend un thread worker temporairement.
        /// </summary>
        /// <param name="threadIndex">Index du thread</param>
        [ThreadSafe]
        void SuspendWorkerThread(int threadIndex);

        /// <summary>
        /// Reprend un thread worker suspendu.
        /// </summary>
        /// <param name="threadIndex">Index du thread</param>
        [ThreadSafe]
        void ResumeWorkerThread(int threadIndex);

        /// <summary>
        /// Obtient l'utilisation mémoire d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Utilisation mémoire en octets</returns>
        [ThreadSafe]
        long GetThreadMemoryUsage(int threadId);

        /// <summary>
        /// Obtient le taux de cache hit d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Taux de cache hit (0.0 à 1.0)</returns>
        [ThreadSafe]
        float GetThreadCacheHitRate(int threadId);

        /// <summary>
        /// Obtient l'utilisation du budget CPU d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Utilisation du budget (0.0 à 1.0)</returns>
        [ThreadSafe]
        float GetThreadBudgetUsage(int threadId);

        /// <summary>
        /// Obtient la heatmap de charge d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Heatmap de charge</returns>
        [ThreadSafe]
        ThreadHeatmapReport GetThreadHeatmap(int threadId);

        /// <summary>
        /// Obtient les données de télémétrie d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Données de télémétrie</returns>
        [ThreadSafe]
        ThreadTelemetryData GetThreadTelemetryData(int threadId);

        /// <summary>
        /// Obtient le niveau de gravité des erreurs d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Niveau de gravité</returns>
        [ThreadSafe]
        JobErrorLevel GetThreadErrorSeverity(int threadId);

        /// <summary>
        /// Obtient la tendance des performances d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Tendance des performances</returns>
        [ThreadSafe]
        ThreadPerformanceTrend GetThreadPerformanceTrend(int threadId);

        // Nouvelles méthodes de gestion des threads (25 idées inédites)
        /// <summary>
        /// Obtient la configuration du gestionnaire d'affinité de threads.
        /// </summary>
        /// <returns>Configuration du gestionnaire d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityManagerConfig GetThreadAffinityManagerConfig();

        /// <summary>
        /// Obtient la heatmap d'affinité des threads.
        /// </summary>
        /// <returns>Heatmap d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityHeatmap GetThreadAffinityHeatmap();

        /// <summary>
        /// Obtient la tendance d'affinité des threads.
        /// </summary>
        /// <returns>Tendance d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityTrend GetThreadAffinityTrend();

        /// <summary>
        /// Obtient le budget d'affinité des threads.
        /// </summary>
        /// <returns>Budget d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityBudget GetThreadAffinityBudget();

        /// <summary>
        /// Obtient le rapport d'erreurs d'affinité des threads.
        /// </summary>
        /// <returns>Rapport d'erreurs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityErrorReport GetThreadAffinityErrorReport();

        /// <summary>
        /// Obtient les données de télémétrie d'affinité des threads.
        /// </summary>
        /// <returns>Données de télémétrie d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityTelemetryData GetThreadAffinityTelemetryData();

        /// <summary>
        /// Obtient les données de performance d'affinité des threads.
        /// </summary>
        /// <returns>Données de performance d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityPerformanceData GetThreadAffinityPerformanceData();

        /// <summary>
        /// Obtient la zone de profilage d'affinité des threads.
        /// </summary>
        /// <returns>Zone de profilage d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityProfilerZone GetThreadAffinityProfilerZone();

        /// <summary>
        /// Obtient les informations de version d'affinité des threads.
        /// </summary>
        /// <returns>Informations de version d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityVersionInfo GetThreadAffinityVersionInfo();

        /// <summary>
        /// Obtient les métadonnées de build d'affinité des threads.
        /// </summary>
        /// <returns>Métadonnées de build d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityBuildMetadata GetThreadAffinityBuildMetadata();

        /// <summary>
        /// Obtient l'état des verrous d'affinité des threads.
        /// </summary>
        /// <returns>État des verrous d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityLockStatus GetThreadAffinityLockStatus();

        /// <summary>
        /// Obtient l'utilisation du cache d'affinité des threads.
        /// </summary>
        /// <returns>Utilisation du cache d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityCacheUsage GetThreadAffinityCacheUsage();

        /// <summary>
        /// Obtient l'utilisation mémoire d'affinité des threads.
        /// </summary>
        /// <returns>Utilisation mémoire d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityMemoryUsage GetThreadAffinityMemoryUsage();

        /// <summary>
        /// Obtient le nombre de jobs d'affinité des threads.
        /// </summary>
        /// <returns>Nombre de jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobCount GetThreadAffinityJobCount();

        /// <summary>
        /// Obtient la latence des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Latence des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobLatency GetThreadAffinityJobLatency();

        /// <summary>
        /// Obtient le débit des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Débit des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobThroughput GetThreadAffinityJobThroughput();

        /// <summary>
        /// Obtient le taux d'échec des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Taux d'échec des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobFailureRate GetThreadAffinityJobFailureRate();

        /// <summary>
        /// Obtient le nombre de retry des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Nombre de retry des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobRetryCount GetThreadAffinityJobRetryCount();

        /// <summary>
        /// Obtient la distribution de priorité des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Distribution de priorité des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobPriorityDistribution GetThreadAffinityJobPriorityDistribution();

        /// <summary>
        /// Obtient le temps d'exécution des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Temps d'exécution des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobExecutionTime GetThreadAffinityJobExecutionTime();

        /// <summary>
        /// Obtient le graphe de dépendances des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Graphe de dépendances des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobDependencyGraph GetThreadAffinityJobDependencyGraph();

        /// <summary>
        /// Obtient la heatmap des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Heatmap des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobHeatmap GetThreadAffinityJobHeatmap();

        /// <summary>
        /// Obtient les données de télémétrie des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Données de télémétrie des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobTelemetryData GetThreadAffinityJobTelemetryData();

        /// <summary>
        /// Obtient le niveau de gravité des erreurs des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Niveau de gravité des erreurs des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobErrorSeverity GetThreadAffinityJobErrorSeverity();

        /// <summary>
        /// Obtient la tendance de performance des jobs d'affinité des threads.
        /// </summary>
        /// <returns>Tendance de performance des jobs d'affinité</returns>
        [ThreadSafe]
        ThreadAffinityJobPerformanceTrend GetThreadAffinityJobPerformanceTrend();

        // ═══════════════════════════════════════════════════════════════
        // 📊 4. PROFILAGE & MONITORING (25 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtient le temps d'exécution d'un job.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Temps d'exécution</returns>
        [ThreadSafe]
        TimeSpan GetJobExecutionTime(JobHandle handle);

        /// <summary>
        /// Obtient la latence d'un job (temps entre planification et début d'exécution).
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Latence du job</returns>
        [ThreadSafe]
        TimeSpan GetJobLatency(JobHandle handle);

        /// <summary>
        /// Obtient le temps total d'un job (latence + exécution).
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Temps total</returns>
        [ThreadSafe]
        TimeSpan GetJobTotalTime(JobHandle handle);

        /// <summary>
        /// Obtient le débit du système de jobs.
        /// </summary>
        /// <returns>Rapport de débit</returns>
        [ThreadSafe]
        JobThroughputReport GetJobThroughput();

        /// <summary>
        /// Obtient le taux d'échec des jobs.
        /// </summary>
        /// <returns>Rapport d'échec</returns>
        [ThreadSafe]
        JobFailureReport GetJobFailureRate();

        /// <summary>
        /// Obtient le nombre de tentatives de retry des jobs.
        /// </summary>
        /// <returns>Rapport de retry</returns>
        [ThreadSafe]
        JobRetryReport GetJobRetryCount();

        /// <summary>
        /// Obtient les données de performance d'une catégorie de jobs.
        /// </summary>
        /// <param name="category">Catégorie de jobs</param>
        /// <returns>Données de performance</returns>
        [ThreadSafe]
        JobCategoryPerformanceData GetCategoryPerformanceData(JobCategory category);

        /// <summary>
        /// Obtient le temps d'exécution moyen par catégorie.
        /// </summary>
        /// <returns>Dictionnaire catégorie → temps moyen</returns>
        [ThreadSafe]
        Dictionary<JobCategory, float> GetAverageExecutionTimeByCategory();

        /// <summary>
        /// Obtient la distribution des priorités des jobs actifs.
        /// </summary>
        /// <returns>Distribution des priorités</returns>
        [ThreadSafe]
        JobPriorityDistribution GetPriorityDistribution();

        /// <summary>
        /// Obtient la timeline des jobs pour la frame actuelle.
        /// </summary>
        /// <returns>Timeline des jobs</returns>
        [ThreadSafe]
        JobTimelineReport GetFrameTimeline();

        /// <summary>
        /// Obtient la timeline des jobs pour les N dernières frames.
        /// </summary>
        /// <param name="frameCount">Nombre de frames</param>
        /// <returns>Timeline multi-frame</returns>
        [ThreadSafe]
        JobTimelineReport GetTimeline(int frameCount);

        /// <summary>
        /// Obtient la heatmap de charge CPU des threads.
        /// </summary>
        /// <returns>Heatmap de charge</returns>
        /// <remarks>
        /// Visualise la charge CPU de chaque thread worker.
        /// Utile pour identifier les déséquilibres.
        /// </remarks>
        [ThreadSafe]
        CPUHeatmapReport GetThreadHeatmap();

        /// <summary>
        /// Obtient l'historique des événements du système de jobs.
        /// </summary>
        /// <returns>Historique des événements</returns>
        [ThreadSafe]
        JobEventHistory GetEventHistory();

        /// <summary>
        /// Obtient les données de télémétrie du système de jobs.
        /// </summary>
        /// <returns>Données de télémétrie</returns>
        [ThreadSafe]
        JobSystemTelemetryData GetTelemetryData();

        /// <summary>
        /// Démarre une session de profilage des jobs.
        /// </summary>
        /// <param name="sessionName">Nom de la session</param>
        void StartProfilingSession(string sessionName);

        /// <summary>
        /// Arrête la session de profilage actuelle.
        /// </summary>
        /// <returns>Rapport de profilage</returns>
        JobProfilingReport StopProfilingSession();

        /// <summary>
        /// Obtient le profiler du système de jobs.
        /// </summary>
        /// <returns>Profiler</returns>
        [ThreadSafe]
        JobSystemProfiler GetProfiler();

        /// <summary>
        /// Obtient les métriques globales du système de jobs.
        /// </summary>
        /// <returns>Métriques globales</returns>
        [ThreadSafe]
        JobSystemMetrics GetJobSystemMetrics();

        /// <summary>
        /// Obtient les données de performance du système de jobs.
        /// </summary>
        /// <returns>Données de performance</returns>
        [ThreadSafe]
        JobSystemPerformanceData GetJobSystemPerformanceData();

        /// <summary>
        /// Obtient les données de télémétrie du système de jobs.
        /// </summary>
        /// <returns>Données de télémétrie</returns>
        [ThreadSafe]
        JobSystemTelemetryData GetJobSystemTelemetry();

        /// <summary>
        /// Obtient la heatmap de charge du système de jobs.
        /// </summary>
        /// <returns>Heatmap de charge</returns>
        [ThreadSafe]
        JobSystemHeatmap GetJobSystemHeatmap();

        /// <summary>
        /// Obtient la timeline du système de jobs.
        /// </summary>
        /// <returns>Timeline des jobs</returns>
        [ThreadSafe]
        JobTimelineReport GetJobSystemTimeline();

        /// <summary>
        /// Obtient l'historique des événements du système de jobs.
        /// </summary>
        /// <returns>Historique des événements</returns>
        [ThreadSafe]
        JobEventHistory GetJobSystemEventHistory();

        /// <summary>
        /// Obtient les conflits de dépendances du système de jobs.
        /// </summary>
        /// <returns>Conflits de dépendances</returns>
        [ThreadSafe]
        JobDependencyConflictReport GetJobSystemDependencyConflicts();

        /// <summary>
        /// Obtient l'utilisation des ressources du système de jobs.
        /// </summary>
        /// <returns>Utilisation des ressources</returns>
        [ThreadSafe]
        JobSystemResourceUsageReport GetJobSystemResourceUsage();

        /// <summary>
        /// Obtient le statut de streaming du système de jobs.
        /// </summary>
        /// <returns>Statut de streaming</returns>
        [ThreadSafe]
        JobSystemStreamingStatusReport GetJobSystemStreamingStatus();

        /// <summary>
        /// Obtient la charge des threads du système de jobs.
        /// </summary>
        /// <returns>Charge des threads</returns>
        [ThreadSafe]
        ThreadLoadReport GetJobSystemThreadLoad();

        /// <summary>
        /// Obtient la profondeur de la file d'attente du système de jobs.
        /// </summary>
        /// <returns>Profondeur de la file</returns>
        [ThreadSafe]
        int GetJobSystemJobQueueDepth();

        /// <summary>
        /// Obtient la tendance du budget CPU du système de jobs.
        /// </summary>
        /// <returns>Tendance du budget</returns>
        [ThreadSafe]
        JobBudgetTrendReport GetJobSystemFrameBudgetTrend();

        /// <summary>
        /// Obtient la fréquence des erreurs du système de jobs.
        /// </summary>
        /// <returns>Fréquence des erreurs</returns>
        [ThreadSafe]
        JobErrorFrequencyReport GetJobSystemErrorFrequency();

        /// <summary>
        /// Obtient le taux de cache hit du système de jobs.
        /// </summary>
        /// <returns>Taux de cache hit</returns>
        [ThreadSafe]
        float GetJobSystemCacheHitRate();

        /// <summary>
        /// Obtient les fuites de mémoire du système de jobs.
        /// </summary>
        /// <returns>Fuites de mémoire</returns>
        [ThreadSafe]
        JobMemoryLeakReport GetJobSystemMemoryLeaks();

        /// <summary>
        /// Obtient les conflits de threads du système de jobs.
        /// </summary>
        /// <returns>Conflits de threads</returns>
        [ThreadSafe]
        ThreadConflictReport GetJobSystemThreadConflicts();

        /// <summary>
        /// Obtient la latence des jobs du système de jobs.
        /// </summary>
        /// <returns>Latence des jobs</returns>
        [ThreadSafe]
        JobLatencyReport GetJobSystemJobLatency();

        /// <summary>
        /// Obtient le débit des jobs du système de jobs.
        /// </summary>
        /// <returns>Débit des jobs</returns>
        [ThreadSafe]
        JobThroughputReport GetJobSystemJobThroughput();

        /// <summary>
        /// Obtient le taux d'échec des jobs du système de jobs.
        /// </summary>
        /// <returns>Taux d'échec des jobs</returns>
        [ThreadSafe]
        JobFailureReport GetJobSystemJobFailureRate();

        /// <summary>
        /// Obtient le nombre de tentatives des jobs du système de jobs.
        /// </summary>
        /// <returns>Nombre de tentatives</returns>
        [ThreadSafe]
        JobRetryReport GetJobSystemJobRetryCount();

        /// <summary>
        /// Obtient la distribution des priorités des jobs du système de jobs.
        /// </summary>
        /// <returns>Distribution des priorités</returns>
        [ThreadSafe]
        JobPriorityDistribution GetJobSystemJobPriorityDistribution();

        /// <summary>
        /// Obtient le temps d'exécution des jobs du système de jobs.
        /// </summary>
        /// <returns>Temps d'exécution</returns>
        [ThreadSafe]
        JobExecutionTimeReport GetJobSystemJobExecutionTime();

        /// <summary>
        /// Obtient le graphe de dépendances des jobs du système de jobs.
        /// </summary>
        /// <returns>Graphe de dépendances</returns>
        [ThreadSafe]
        JobDependencyGraph GetJobSystemJobDependencyGraph();

        /// <summary>
        /// Exporte les données de profilage en JSON.
        /// </summary>
        /// <param name="filePath">Chemin du fichier de sortie</param>
        void ExportProfilerDataToJson(string filePath);

        /// <summary>
        /// Exporte les données de profilage en CSV.
        /// </summary>
        /// <param name="filePath">Chemin du fichier de sortie</param>
        void ExportProfilerDataToCsv(string filePath);

        // Nouvelles méthodes de profilage & monitoring (25 idées inédites)
        /// <summary>
        /// Obtient la configuration du profiler du système de jobs.
        /// </summary>
        /// <returns>Configuration du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerConfig GetProfilerConfig();

        /// <summary>
        /// Obtient la heatmap du profiler du système de jobs.
        /// </summary>
        /// <returns>Heatmap du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerHeatmap GetProfilerHeatmap();

        /// <summary>
        /// Obtient la tendance du profiler du système de jobs.
        /// </summary>
        /// <returns>Tendance du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerTrend GetProfilerTrend();

        /// <summary>
        /// Obtient le budget du profiler du système de jobs.
        /// </summary>
        /// <returns>Budget du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerBudget GetProfilerBudget();

        /// <summary>
        /// Obtient le rapport d'erreurs du profiler du système de jobs.
        /// </summary>
        /// <returns>Rapport d'erreurs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerErrorReport GetProfilerErrorReport();

        /// <summary>
        /// Obtient les données de télémétrie du profiler du système de jobs.
        /// </summary>
        /// <returns>Données de télémétrie du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerTelemetryData GetProfilerTelemetryData();

        /// <summary>
        /// Obtient les données de performance du profiler du système de jobs.
        /// </summary>
        /// <returns>Données de performance du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerPerformanceData GetProfilerPerformanceData();

        /// <summary>
        /// Obtient la zone de profilage du profiler du système de jobs.
        /// </summary>
        /// <returns>Zone de profilage du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerProfilerZone GetProfilerProfilerZone();

        /// <summary>
        /// Obtient les informations de version du profiler du système de jobs.
        /// </summary>
        /// <returns>Informations de version du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerVersionInfo GetProfilerVersionInfo();

        /// <summary>
        /// Obtient les métadonnées de build du profiler du système de jobs.
        /// </summary>
        /// <returns>Métadonnées de build du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerBuildMetadata GetProfilerBuildMetadata();

        /// <summary>
        /// Obtient l'état des verrous du profiler du système de jobs.
        /// </summary>
        /// <returns>État des verrous du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerLockStatus GetProfilerLockStatus();

        /// <summary>
        /// Obtient l'utilisation du cache du profiler du système de jobs.
        /// </summary>
        /// <returns>Utilisation du cache du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerCacheUsage GetProfilerCacheUsage();

        /// <summary>
        /// Obtient l'utilisation mémoire du profiler du système de jobs.
        /// </summary>
        /// <returns>Utilisation mémoire du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerMemoryUsage GetProfilerMemoryUsage();

        /// <summary>
        /// Obtient le nombre de jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Nombre de jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobCount GetProfilerJobCount();

        /// <summary>
        /// Obtient la latence des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Latence des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobLatency GetProfilerJobLatency();

        /// <summary>
        /// Obtient le débit des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Débit des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobThroughput GetProfilerJobThroughput();

        /// <summary>
        /// Obtient le taux d'échec des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Taux d'échec des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobFailureRate GetProfilerJobFailureRate();

        /// <summary>
        /// Obtient le nombre de retry des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Nombre de retry des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobRetryCount GetProfilerJobRetryCount();

        /// <summary>
        /// Obtient la distribution de priorité des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Distribution de priorité des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobPriorityDistribution GetProfilerJobPriorityDistribution();

        /// <summary>
        /// Obtient le temps d'exécution des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Temps d'exécution des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobExecutionTime GetProfilerJobExecutionTime();

        /// <summary>
        /// Obtient le graphe de dépendances des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Graphe de dépendances des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobDependencyGraph GetProfilerJobDependencyGraph();

        /// <summary>
        /// Obtient la heatmap des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Heatmap des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobHeatmap GetProfilerJobHeatmap();

        /// <summary>
        /// Obtient les données de télémétrie des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Données de télémétrie des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobTelemetryData GetProfilerJobTelemetryData();

        /// <summary>
        /// Obtient le niveau de gravité des erreurs des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Niveau de gravité des erreurs des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobErrorSeverity GetProfilerJobErrorSeverity();

        /// <summary>
        /// Obtient la tendance de performance des jobs du profiler du système de jobs.
        /// </summary>
        /// <returns>Tendance de performance des jobs du profiler</returns>
        [ThreadSafe]
        JobSystemProfilerJobPerformanceTrend GetProfilerJobPerformanceTrend();

        // ═══════════════════════════════════════════════════════════════
        // 🛡️ 5. SÉCURITÉ & ROBUSTESSE (25 idées)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Détecte les deadlocks potentiels dans les jobs actifs.
        /// </summary>
        /// <returns>Rapport de détection de deadlocks</returns>
        /// <remarks>
        /// Analyse le graphe de dépendances pour identifier les cycles.
        /// </remarks>
        [ThreadSafe]
        DeadlockDetectionReport DetectDeadlocks();

        /// <summary>
        /// Détecte les conflits de threads dans les jobs actifs.
        /// </summary>
        /// <returns>Rapport de conflits</returns>
        /// <remarks>
        /// Identifie les accès concurrents non synchronisés aux données partagées.
        /// </remarks>
        [ThreadSafe]
        ThreadConflictReport DetectThreadConflicts();

        /// <summary>
        /// Valide l'intégrité d'un job avant planification.
        /// </summary>
        /// <param name="job">Job à valider</param>
        /// <returns>Résultat de la validation</returns>
        [ThreadSafe]
        JobValidationResult ValidateJob(IJob job);

        /// <summary>
        /// Valide l'intégrité d'un ensemble de jobs.
        /// </summary>
        /// <param name="jobs">Jobs à valider</param>
        /// <returns>Résultat de la validation</returns>
        [ThreadSafe]
        ValidationResult ValidateJobs(IReadOnlyList<IJob> jobs);

        /// <summary>
        /// Valide l'intégrité d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Résultat de la validation</returns>
        [ThreadSafe]
        ThreadValidationResult ValidateThread(int threadId);

        /// <summary>
        /// Valide l'intégrité du système de jobs.
        /// </summary>
        /// <returns>Résultat de la validation</returns>
        [ThreadSafe]
        SystemValidationResult ValidateJobSystemIntegrity();

        /// <summary>
        /// Valide les dépendances d'un ensemble de jobs.
        /// </summary>
        /// <param name="handles">Handles des jobs</param>
        /// <returns>Rapport de validation</returns>
        [ThreadSafe]
        DependencyValidationReport ValidateJobDependencies(ReadOnlySpan<JobHandle> handles);

        /// <summary>
        /// Valide les dépendances d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Rapport de validation</returns>
        [ThreadSafe]
        DependencyValidationReport ValidateThreadDependencies(int threadId);

        /// <summary>
        /// Tente de récupérer après un échec de job.
        /// </summary>
        /// <param name="handle">Handle du job échoué</param>
        /// <param name="strategy">Stratégie de récupération</param>
        /// <returns>True si la récupération a réussi</returns>
        [ThreadSafe]
        bool RecoverFromJobFailure(JobHandle handle, RecoveryStrategy strategy);

        /// <summary>
        /// Tente de récupérer après un échec de thread.
        /// </summary>
        /// <param name="threadId">ID du thread échoué</param>
        /// <param name="strategy">Stratégie de récupération</param>
        /// <returns>True si la récupération a réussi</returns>
        [ThreadSafe]
        bool RecoverFromThreadFailure(int threadId, RecoveryStrategy strategy);

        /// <summary>
        /// Tente de récupérer après un échec global du système.
        /// </summary>
        /// <param name="strategy">Stratégie de récupération</param>
        /// <returns>True si la récupération a réussi</returns>
        [ThreadSafe]
        bool RecoverFromFailure(RecoveryStrategy strategy);

        /// <summary>
        /// Relance les jobs échoués.
        /// </summary>
        /// <returns>Nombre de jobs relancés</returns>
        [ThreadSafe]
        int RetryFailedJobs();

        /// <summary>
        /// Relance les threads échoués.
        /// </summary>
        /// <returns>Nombre de threads relancés</returns>
        [ThreadSafe]
        int RetryFailedThreads();

        /// <summary>
        /// Obtient le rapport d'erreurs du système de jobs.
        /// </summary>
        /// <returns>Rapport d'erreurs</returns>
        [ThreadSafe]
        JobErrorReport GetErrorReport();

        /// <summary>
        /// Obtient le rapport d'erreurs d'un job spécifique.
        /// </summary>
        /// <param name="handle">Handle du job</param>
        /// <returns>Rapport d'erreurs du job</returns>
        [ThreadSafe]
        JobErrorDetail GetJobErrorDetail(JobHandle handle);

        /// <summary>
        /// Obtient la sévérité des erreurs par catégorie.
        /// </summary>
        /// <returns>Rapport de sévérité</returns>
        [ThreadSafe]
        JobErrorSeverityReport GetErrorSeverityByCategory();

        /// <summary>
        /// Vérifie la sécurité thread-safe d'un job.
        /// </summary>
        /// <param name="job">Job à vérifier</param>
        /// <returns>Rapport de sécurité thread</returns>
        [ThreadSafe]
        ThreadSafetyReport ValidateThreadSafety(IJob job);

        /// <summary>
        /// Vérifie la sécurité thread-safe d'un thread.
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Rapport de sécurité thread</returns>
        [ThreadSafe]
        ThreadSafetyReport ValidateThreadThreadSafety(int threadId);

        /// <summary>
        /// Obtient l'état des verrous du système de jobs.
        /// </summary>
        /// <returns>Rapport des verrous</returns>
        [ThreadSafe]
        JobLockStatusReport GetLockStatus();

        /// <summary>
        /// Obtient l'état de récupération du système de jobs.
        /// </summary>
        /// <returns>État de récupération</returns>
        [ThreadSafe]
        JobRecoveryStatus GetRecoveryStatus();

        /// <summary>
        /// Enregistre une erreur de job dans le logger.
        /// </summary>
        /// <param name="error">Erreur à enregistrer</param>
        /// <param name="level">Niveau de gravité</param>
        void LogJobError(Exception error, JobErrorLevel level);

        /// <summary>
        /// Obtient le logger d'erreurs du système de jobs.
        /// </summary>
        /// <returns>Logger d'erreurs</returns>
        [ThreadSafe]
        JobErrorLogger GetErrorLogger();

        // Nouvelles méthodes de sécurité & robustesse (25 idées inédites)
        /// <summary>
        /// Obtient la configuration de sécurité du système de jobs.
        /// </summary>
        /// <returns>Configuration de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityConfig GetSecurityConfig();

        /// <summary>
        /// Obtient la heatmap de sécurité du système de jobs.
        /// </summary>
        /// <returns>Heatmap de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityHeatmap GetSecurityHeatmap();

        /// <summary>
        /// Obtient la tendance de sécurité du système de jobs.
        /// </summary>
        /// <returns>Tendance de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityTrend GetSecurityTrend();

        /// <summary>
        /// Obtient le budget de sécurité du système de jobs.
        /// </summary>
        /// <returns>Budget de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityBudget GetSecurityBudget();

        /// <summary>
        /// Obtient le rapport d'erreurs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Rapport d'erreurs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityErrorReport GetSecurityErrorReport();

        /// <summary>
        /// Obtient les données de télémétrie de sécurité du système de jobs.
        /// </summary>
        /// <returns>Données de télémétrie de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityTelemetryData GetSecurityTelemetryData();

        /// <summary>
        /// Obtient les données de performance de sécurité du système de jobs.
        /// </summary>
        /// <returns>Données de performance de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityPerformanceData GetSecurityPerformanceData();

        /// <summary>
        /// Obtient la zone de profilage de sécurité du système de jobs.
        /// </summary>
        /// <returns>Zone de profilage de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityProfilerZone GetSecurityProfilerZone();

        /// <summary>
        /// Obtient les informations de version de sécurité du système de jobs.
        /// </summary>
        /// <returns>Informations de version de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityVersionInfo GetSecurityVersionInfo();

        /// <summary>
        /// Obtient les métadonnées de build de sécurité du système de jobs.
        /// </summary>
        /// <returns>Métadonnées de build de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityBuildMetadata GetSecurityBuildMetadata();

        /// <summary>
        /// Obtient l'état des verrous de sécurité du système de jobs.
        /// </summary>
        /// <returns>État des verrous de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityLockStatus GetSecurityLockStatus();

        /// <summary>
        /// Obtient l'utilisation du cache de sécurité du système de jobs.
        /// </summary>
        /// <returns>Utilisation du cache de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityCacheUsage GetSecurityCacheUsage();

        /// <summary>
        /// Obtient l'utilisation mémoire de sécurité du système de jobs.
        /// </summary>
        /// <returns>Utilisation mémoire de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityMemoryUsage GetSecurityMemoryUsage();

        /// <summary>
        /// Obtient le nombre de jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Nombre de jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobCount GetSecurityJobCount();

        /// <summary>
        /// Obtient la latence des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Latence des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobLatency GetSecurityJobLatency();

        /// <summary>
        /// Obtient le débit des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Débit des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobThroughput GetSecurityJobThroughput();

        /// <summary>
        /// Obtient le taux d'échec des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Taux d'échec des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobFailureRate GetSecurityJobFailureRate();

        /// <summary>
        /// Obtient le nombre de retry des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Nombre de retry des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobRetryCount GetSecurityJobRetryCount();

        /// <summary>
        /// Obtient la distribution de priorité des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Distribution de priorité des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobPriorityDistribution GetSecurityJobPriorityDistribution();

        /// <summary>
        /// Obtient le temps d'exécution des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Temps d'exécution des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobExecutionTime GetSecurityJobExecutionTime();

        /// <summary>
        /// Obtient le graphe de dépendances des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Graphe de dépendances des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobDependencyGraph GetSecurityJobDependencyGraph();

        /// <summary>
        /// Obtient la heatmap des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Heatmap des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobHeatmap GetSecurityJobHeatmap();

        /// <summary>
        /// Obtient les données de télémétrie des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Données de télémétrie des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobTelemetryData GetSecurityJobTelemetryData();

        /// <summary>
        /// Obtient le niveau de gravité des erreurs des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Niveau de gravité des erreurs des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobErrorSeverity GetSecurityJobErrorSeverity();

        /// <summary>
        /// Obtient la tendance de performance des jobs de sécurité du système de jobs.
        /// </summary>
        /// <returns>Tendance de performance des jobs de sécurité</returns>
        [ThreadSafe]
        JobSystemSecurityJobPerformanceTrend GetSecurityJobPerformanceTrend();

        // ═══════════════════════════════════════════════════════════════
        // 💾 6. OPTIMISATION, CACHE & BUDGET
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtient le taux de cache hit des jobs.
        /// </summary>
        /// <returns>Taux de cache hit (0.0 à 1.0)</returns>
        /// <remarks>
        /// Mesure l'efficacité du cache de résultats de jobs.
        /// </remarks>
        [ThreadSafe]
        float GetJobCacheHitRate();

        /// <summary>
        /// Obtient l'utilisation mémoire du système de jobs.
        /// </summary>
        /// <returns>Utilisation mémoire en octets</returns>
        [ThreadSafe]
        long GetJobMemoryUsage();

        /// <summary>
        /// Obtient l'utilisation mémoire par catégorie de jobs.
        /// </summary>
        /// <returns>Utilisation mémoire par catégorie</returns>
        [ThreadSafe]
        Dictionary<JobCategory, long> GetMemoryUsageByCategory();

        /// <summary>
        /// Obtient l'utilisation du budget CPU alloué aux jobs.
        /// </summary>
        /// <returns>Utilisation du budget (0.0 à 1.0)</returns>
        /// <remarks>
        /// Mesure la proportion du budget frame utilisée par le job system.
        /// </remarks>
        [ThreadSafe]
        float GetJobBudgetUsage();

        /// <summary>
        /// Définit le budget CPU alloué au système de jobs par frame.
        /// </summary>
        /// <param name="budgetMs">Budget en millisecondes</param>
        void SetFrameBudget(float budgetMs);

        /// <summary>
        /// Définit le budget CPU par catégorie de jobs.
        /// </summary>
        /// <param name="budgets">Budgets par catégorie</param>
        void SetCategoryBudgets(Dictionary<JobCategory, float> budgets);

        /// <summary>
        /// Obtient l'allocateur de budget du système de jobs.
        /// </summary>
        /// <returns>Allocateur de budget</returns>
        [ThreadSafe]
        JobBudgetAllocator GetBudgetAllocator();

        /// <summary>
        /// Efface le cache de résultats des jobs.
        /// </summary>
        void ClearJobCache();

        /// <summary>
        /// Invalide une entrée spécifique du cache.
        /// </summary>
        /// <param name="key">Clé de l'entrée à invalider</param>
        void InvalidateCacheEntry(string key);

        /// <summary>
        /// Obtient l'utilisation du cache du système de jobs.
        /// </summary>
        /// <returns>Rapport d'utilisation du cache</returns>
        [ThreadSafe]
        JobCacheUsageReport GetCacheUsage();

        /// <summary>
        /// Obtient la heatmap de charge CPU par sous-système.
        /// </summary>
        /// <returns>Heatmap de charge</returns>
        [ThreadSafe]
        JobSystemHeatmap GetSystemHeatmap();

        /// <summary>
        /// Obtient la tendance des performances du système de jobs.
        /// </summary>
        /// <returns>Rapport de tendance</returns>
        [ThreadSafe]
        JobPerformanceTrend GetPerformanceTrend();

        /// <summary>
        /// Active ou désactive le mode d'économie d'énergie.
        /// </summary>
        /// <param name="enabled">État du mode économie</param>
        /// <remarks>
        /// En mode économie, le système réduit le nombre de threads actifs
        /// et limite la fréquence de planification.
        /// </remarks>
        void SetPowerSavingMode(bool enabled);

        /// <summary>
        /// Vérifie si le mode d'économie d'énergie est actif.
        /// </summary>
        /// <returns>True si le mode économie est actif</returns>
        [ThreadSafe]
        bool IsPowerSavingMode();

        // ═══════════════════════════════════════════════════════════════
        // 🔌 7. INTEROPÉRABILITÉ MOTEUR (HOOKS)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Enregistre les hooks pour le moteur d'animation.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à IAnimationEngine pour les Parallel Animation Jobs.
        /// Permet de planifier les évaluations de blendspace, IK, skinning, etc.
        /// en parallèle sur plusieurs threads.
        /// </remarks>
        void RegisterAnimationHooks();

        /// <summary>
        /// Enregistre les hooks pour le moteur physique.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à PhysicsEngine pour les collision jobs,
        /// raycasts parallèles, et simulations de particules physiques.
        /// </remarks>
        void RegisterPhysicsHooks();

        /// <summary>
        /// Enregistre les hooks pour le moteur IA.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à AIEngine pour les pathfinding jobs,
        /// behavior tree evaluations, et GOAP planning.
        /// </remarks>
        void RegisterAIHooks();

        /// <summary>
        /// Enregistre les hooks pour le moteur de rendu.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à IRenderEngine pour les GPU profiling jobs,
        /// culling parallèle, et préparation des draw calls.
        /// </remarks>
        void RegisterRenderHooks();

        /// <summary>
        /// Enregistre les hooks pour le moteur audio.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à IAudioEngine pour le mixage parallèle
        /// et le traitement DSP.
        /// </remarks>
        void RegisterAudioHooks();

        /// <summary>
        /// Enregistre les hooks pour le système de mouvement.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à MovementSystem et MovementAnimationBridgeSystem
        /// pour orchestrer les appels sur les threads appropriés.
        /// Prérequis pour le Thread Affinity Control.
        /// </remarks>
        void RegisterMovementHooks();

        /// <summary>
        /// Enregistre les hooks pour le bus d'événements.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à EventBus pour la notification
        /// des événements de cycle de vie des jobs.
        /// </remarks>
        void RegisterEventBusHooks();

        /// <summary>
        /// Enregistre les hooks pour le profiler global.
        /// </summary>
        /// <remarks>
        /// Connecte le job system au Profiler global du moteur.
        /// </remarks>
        void RegisterProfilerHooks();

        /// <summary>
        /// Enregistre les hooks pour le debug overlay.
        /// </summary>
        /// <remarks>
        /// Connecte le job system au DebugOverlay pour l'affichage
        /// des informations de debug en temps réel.
        /// </remarks>
        void RegisterDebugHooks();

        /// <summary>
        /// Enregistre les hooks pour le GPU profiler.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à GPUProfilerHook pour les mesures
        /// de performance GPU (squelettes, vertex shader, etc.).
        /// </remarks>
        void RegisterGPUProfilerHooks();

        /// <summary>
        /// Enregistre les hooks pour le système de gestion des ressources.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à ResourceManager pour le chargement
        /// asynchrone des ressources.
        /// </remarks>
        void RegisterResourceHooks();

        /// <summary>
        /// Enregistre les hooks pour le système de streaming.
        /// </summary>
        /// <remarks>
        /// Connecte le job system au streaming pour le chargement
        /// progressif des assets.
        /// </remarks>
        void RegisterStreamingHooks();

        /// <summary>
        /// Enregistre les hooks pour le système de sauvegarde.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à SaveSystem pour les écritures
        /// asynchrones sur disque.
        /// </remarks>
        void RegisterSaveSystemHooks();

        /// <summary>
        /// Enregistre les hooks pour le système de réseau.
        /// </summary>
        /// <remarks>
        /// Connecte le job system à NetSyncSystem pour le traitement
        /// parallèle des paquets réseau.
        /// </remarks>
        void RegisterNetworkHooks();

        /// <summary>
        /// Enregistre les zones de profilage du système de jobs.
        /// </summary>
        /// <remarks>
        /// Crée des zones de profilage pour mesurer les coûts par catégorie.
        /// </remarks>
        void RegisterProfilerZones();

        /// <summary>
        /// Enregistre les gestionnaires d'erreurs du système de jobs.
        /// </summary>
        void RegisterErrorHandlers();

        /// <summary>
        /// Enregistre les hooks de nettoyage à l'arrêt.
        /// </summary>
        void RegisterShutdownHooks();

        // ═══════════════════════════════════════════════════════════════
        // 🐛 8. DEBUG & VISUALISATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtient l'overlay de debug du système de jobs.
        /// </summary>
        /// <returns>Overlay de debug</returns>
        [ThreadSafe]
        JobDebugOverlay GetDebugOverlay();

        /// <summary>
        /// Active ou désactive une visualisation de debug.
        /// </summary>
        /// <param name="type">Type de visualisation</param>
        /// <param name="enabled">État d'activation</param>
        void ToggleDebugVisualization(JobDebugVisualizationType type, bool enabled);

        /// <summary>
        /// Active le mode de trace détaillée.
        /// </summary>
        /// <param name="enabled">État de la trace</param>
        /// <remarks>
        /// En mode trace, chaque job est enregistré avec ses timestamps.
        /// Impact significatif sur les performances ; à utiliser uniquement en debug.
        /// </remarks>
        void SetTraceMode(bool enabled);

        /// <summary>
        /// Exporte la timeline des jobs au format JSON.
        /// </summary>
        /// <param name="outputPath">Chemin du fichier de sortie</param>
        void ExportTimelineToJson(string outputPath);

        /// <summary>
        /// Exporte la timeline des jobs au format Chrome Trace.
        /// </summary>
        /// <param name="outputPath">Chemin du fichier de sortie</param>
        /// <remarks>
        /// Format compatible avec chrome://tracing.
        /// </remarks>
        void ExportTimelineToChromeTrace(string outputPath);

        /// <summary>
        /// Obtient un snapshot de l'état actuel du système de jobs.
        /// </summary>
        /// <returns>Snapshot complet</returns>
        [ThreadSafe]
        JobSystemSnapshot GetSnapshot();

        /// <summary>
        /// Obtient les informations de version du système de jobs.
        /// </summary>
        /// <returns>Informations de version</returns>
        [ThreadSafe]
        VersionInfo GetVersionInfo();

        /// <summary>
        /// Obtient les métadonnées de build du système de jobs.
        /// </summary>
        /// <returns>Métadonnées de build</returns>
        [ThreadSafe]
        BuildMetadata GetBuildMetadata();

        // ═══════════════════════════════════════════════════════════════
        // ⚡ 9. CONFIGURATION & EXTENSIBILITÉ
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtient la configuration actuelle du système de jobs.
        /// </summary>
        /// <returns>Configuration</returns>
        [ThreadSafe]
        JobSystemConfig GetConfiguration();

        /// <summary>
        /// Applique une nouvelle configuration au système de jobs.
        /// </summary>
        /// <param name="config">Nouvelle configuration</param>
        /// <remarks>
        /// Peut nécessiter un redémarrage du système selon les paramètres modifiés.
        /// </remarks>
        void ApplyConfiguration(JobSystemConfig config);

        /// <summary>
        /// Recharge la configuration sans redémarrer le système.
        /// </summary>
        void ReloadConfiguration();

        /// <summary>
        /// Enregistre un type de job personnalisé.
        /// </summary>
        /// <param name="factory">Fabrique de jobs</param>
        /// <remarks>
        /// Permet d'étendre le système avec des types de jobs personnalisés.
        /// </remarks>
        void RegisterCustomJobType(IJobFactory factory);

        /// <summary>
        /// Enregistre une extension du système de jobs.
        /// </summary>
        /// <typeparam name="T">Type de l'extension</typeparam>
        void RegisterExtension<T>() where T : IJobSystemExtension;

        /// <summary>
        /// Obtient une extension du système de jobs.
        /// </summary>
        /// <typeparam name="T">Type de l'extension</typeparam>
        /// <returns>Extension demandée</returns>
        T GetExtension<T>() where T : IJobSystemExtension;

        /// <summary>
        /// Obtient la liste des sous-systèmes du job system.
        /// </summary>
        /// <returns>Liste des sous-systèmes</returns>
        [ThreadSafe]
        List<JobSubsystemInfo> GetSubsystems();

        /// <summary>
        /// Obtient l'état d'un sous-système du job system.
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>État du sous-système</returns>
        [ThreadSafe]
        JobSubsystemState GetSubsystemState(JobSubsystemType type);

        /// <summary>
        /// Valide l'intégrité de tous les sous-systèmes.
        /// </summary>
        /// <returns>Rapport d'intégrité</returns>
        [ThreadSafe]
        JobSubsystemIntegrityReport ValidateSubsystemIntegrity();
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

    public interface IJobSystemGraphBuilder
    {
        void AddJob(IJob job);
        void AddDependency(JobHandle from, JobHandle to);
        IJobGraph Build();
    }

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