using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace Engine.Animation
{
    /// <summary>
    /// Interface principale pour le moteur d'animation AAA avec toutes les extensions
    /// Respecte les règles d'architecture : Engine → Systems → Components → Utilities
    /// </summary>
    /// <remarks>
    /// Ce moteur est conçu pour être thread-safe et permettre des opérations asynchrones.
    /// Les sous-systèmes peuvent être initialisés/rechargés indépendamment pour une flexibilité maximale.
    /// </remarks>
    public interface IAnimationEngine
    {
        // ⚙️ 1. ARCHITECTURE & INFRASTRUCTURE
        /// <summary>
        /// Initialise le moteur d'animation
        /// </summary>
        /// <remarks>
        /// Cette méthode initialise tous les sous-systèmes et configure les ressources nécessaires.
        /// Doit être appelée avant toute autre méthode du moteur.
        /// </remarks>
        /// <example>
        /// <code>
        /// var engine = new AnimationEngine();
        /// engine.Initialize();
        /// </code>
        /// </example>
        /// <exception cref="InvalidOperationException">Si le moteur est déjà initialisé.</exception>
        void Initialize();
        
        /// <summary>
        /// Met à jour le moteur d'animation à chaque frame
        /// </summary>
        /// <param name="deltaTime">Temps écoulé depuis la dernière frame</param>
        /// <remarks>
        /// Cette méthode met à jour tous les systèmes actifs en fonction du deltaTime.
        /// Elle est généralement appelée dans la boucle principale du jeu.
        /// </remarks>
        /// <example>
        /// <code>
        /// float lastTime = Time.Now;
        /// while (gameRunning)
        /// {
        ///     float currentTime = Time.Now;
        ///     float deltaTime = currentTime - lastTime;
        ///     animationEngine.Update(deltaTime);
        ///     lastTime = currentTime;
        /// }
        /// </code>
        /// </example>
        /// <exception cref="InvalidOperationException">Si le moteur n'est pas initialisé.</exception>
        void Update(float deltaTime);
        
        /// <summary>
        /// Arrête et nettoie le moteur d'animation
        /// </summary>
        /// <remarks>
        /// Libère toutes les ressources allouées par le moteur.
        /// Doit être appelée avant la fermeture de l'application.
        /// </remarks>
        void Shutdown();

        // Sous-systèmes modulaires - respecte la modularité
        /// <summary>
        /// Obtient un sous-système d'animation spécifique
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Sous-système d'animation</returns>
        /// <remarks>
        /// Thread-safe. Permet d'accéder aux sous-systèmes pour des configurations avancées.
        /// </remarks>
        /// <exception cref="ArgumentException">Si le type de sous-système n'est pas valide.</exception>
        [ThreadSafe]
        IAnimationSubsystem GetSubsystem(AnimationSubsystemType type);
        
        /// <summary>
        /// Enregistre un nouveau sous-système
        /// </summary>
        /// <param name="subsystem">Sous-système à enregistrer</param>
        /// <remarks>
        /// Permet d'étendre le moteur avec des fonctionnalités personnalisées.
        /// Le sous-système sera géré par le moteur.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Si le sous-système est null.</exception>
        /// <exception cref="InvalidOperationException">Si le sous-système est déjà enregistré.</exception>
        void RegisterSubsystem(IAnimationSubsystem subsystem);
        
        /// <summary>
        /// Supprime un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système à supprimer</param>
        /// <remarks>
        /// Désactive et supprime le sous-système du moteur.
        /// Appelle automatiquement Shutdown() sur le sous-système.
        /// </remarks>
        /// <exception cref="ArgumentException">Si le type de sous-système n'est pas valide.</exception>
        void RemoveSubsystem(AnimationSubsystemType type);

        // Contexte global - données d'exécution partagées
        /// <summary>
        /// Obtient le contexte global du moteur
        /// </summary>
        /// <returns>Contexte du moteur</returns>
        /// <remarks>
        /// Thread-safe. Contient les dépendances globales du moteur.
        /// </remarks>
        [ThreadSafe]
        AnimationEngineContext GetContext();
        
        /// <summary>
        /// Définit le contexte global du moteur
        /// </summary>
        /// <param name="context">Nouveau contexte</param>
        /// <remarks>
        /// Permet de configurer les dépendances du moteur.
        /// Ne doit pas être appelé pendant une mise à jour.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Si le contexte est null.</exception>
        void SetContext(AnimationEngineContext context);
        
        /// <summary>
        /// Recharge le contexte global sans redémarrer le moteur
        /// </summary>
        /// <remarks>
        /// Permet de recharger les dépendances sans arrêter le moteur.
        /// Utile pour le hot-reload ou la configuration dynamique.
        /// </remarks>
        void ReloadContext();

        // Initialisation et rechargement des sous-systèmes
        /// <summary>
        /// Initialise tous les sous-systèmes enregistrés
        /// </summary>
        /// <remarks>
        /// Appelle Initialize() sur tous les sous-systèmes enregistrés.
        /// Peut être utilisé pour réinitialiser l'état du moteur.
        /// </remarks>
        void InitializeSubsystems();
        
        /// <summary>
        /// Initialise les sous-systèmes en parallèle
        /// </summary>
        /// <returns>Tâche d'initialisation</returns>
        /// <remarks>
        /// Lance l'initialisation de tous les sous-systèmes en parallèle.
        /// Utilise le JobSystem interne pour paralléliser les tâches.
        /// </remarks>
        /// <example>
        /// <code>
        /// await animationEngine.InitializeSubsystemsAsync();
        /// Console.WriteLine("Tous les sous-systèmes sont prêts !");
        /// </code>
        /// </example>
        [Async]
        Task InitializeSubsystemsAsync();
        
        /// <summary>
        /// Recharge les dépendances des sous-systèmes sans redémarrer
        /// </summary>
        /// <remarks>
        /// Appelle Shutdown() puis Initialize() sur tous les sous-systèmes.
        /// Permet de recharger les configurations ou les ressources.
        /// </remarks>
        void ReloadSubsystems();
        
        /// <summary>
        /// Recharge les dépendances des sous-systèmes en parallèle
        /// </summary>
        /// <returns>Tâche de rechargement</returns>
        /// <remarks>
        /// Effectue le rechargement en parallèle pour gagner du temps.
        /// Les sous-systèmes sont rechargés dans l'ordre de dépendance.
        /// </remarks>
        [Async]
        Task ReloadSubsystemsAsync();
        
        /// <summary>
        /// Valide l'intégrité des sous-systèmes
        /// </summary>
        /// <returns>True si tous les sous-systèmes sont valides</returns>
        /// <remarks>
        /// Vérifie que tous les sous-systèmes sont correctement configurés.
        /// Utile pour le debugging ou la vérification avant une mise à jour.
        /// </remarks>
        bool ValidateSubsystemIntegrity();
        
        /// <summary>
        /// Valide l'intégrité des sous-systèmes avec rapport détaillé
        /// </summary>
        /// <returns>Rapport d'intégrité détaillé</returns>
        /// <remarks>
        /// Fournit un rapport complet sur l'état de chaque sous-système.
        /// Utile pour le diagnostic ou l'analyse post-mortem.
        /// </remarks>
        SubsystemIntegrityReport ValidateSubsystemIntegrityDetailed();
        
        /// <summary>
        /// Obtient l'état d'un sous-système spécifique
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>État du sous-système</returns>
        /// <remarks>
        /// Thread-safe. Permet de surveiller l'état d'un sous-système particulier.
        /// </remarks>
        [ThreadSafe]
        AnimationSubsystemStatus GetSubsystemStatus(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient la liste des dépendances entre sous-systèmes
        /// </summary>
        /// <returns>Liste des dépendances</returns>
        /// <remarks>
        /// Permet de visualiser les relations entre les sous-systèmes.
        /// Utile pour le diagnostic ou l'optimisation.
        /// </remarks>
        List<AnimationSubsystemDependency> GetSubsystemDependencies();
        
        /// <summary>
        /// Obtient l'ordre de chargement des sous-systèmes
        /// </summary>
        /// <returns>Liste ordonnée des types de sous-systèmes</returns>
        /// <remarks>
        /// Détermine l'ordre dans lequel les sous-systèmes doivent être initialisés.
        /// Respecte les dépendances entre sous-systèmes.
        /// </remarks>
        List<AnimationSubsystemType> GetSubsystemLoadOrder();
        
        /// <summary>
        /// Obtient l'ordre d'initialisation des sous-systèmes
        /// </summary>
        /// <returns>Liste ordonnée des types de sous-systèmes</returns>
        /// <remarks>
        /// Similaire à GetSubsystemLoadOrder mais inclut les dépendances dynamiques.
        /// </remarks>
        List<AnimationSubsystemType> GetSubsystemInitializationOrder();
        
        /// <summary>
        /// Obtient le graphe des dépendances entre sous-systèmes
        /// </summary>
        /// <returns>Graphe des dépendances</returns>
        /// <remarks>
        /// Représente visuellement les relations de dépendance.
        /// Utile pour les outils de diagnostic ou de visualisation.
        /// </remarks>
        DependencyGraph GetSubsystemDependenciesGraph();
        
        /// <summary>
        /// Obtient les données de performance d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Données de performance</returns>
        /// <remarks>
        /// Thread-safe. Fournit des métriques de performance pour un sous-système.
        /// </remarks>
        [ThreadSafe]
        AnimationSubsystemPerformanceData GetSubsystemPerformanceData(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient le rapport d'erreurs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Rapport d'erreurs</returns>
        /// <remarks>
        /// Thread-safe. Fournit les erreurs récentes d'un sous-système.
        /// </remarks>
        [ThreadSafe]
        AnimationSubsystemErrorReport GetSubsystemErrorReport(AnimationSubsystemType type);

        // Hooks pour intégration avec d'autres systèmes
        /// <summary>
        /// Enregistre les hooks pour le profiler global
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de profilage global.
        /// Permet de mesurer les coûts CPU/GPU du moteur.
        /// </remarks>
        void RegisterProfilerHooks();
        
        /// <summary>
        /// Enregistre les hooks pour le debug overlay
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de debug visuel.
        /// Permet d'afficher des informations en temps réel.
        /// </remarks>
        void RegisterDebugHooks();
        
        /// <summary>
        /// Enregistre les hooks pour la synchronisation audio
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système audio.
        /// Permet la synchronisation des sons avec les animations.
        /// </remarks>
        void RegisterAudioHooks();
        
        /// <summary>
        /// Enregistre les hooks pour le rendu et le skinning GPU
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de rendu.
        /// Permet la synchronisation du skinning GPU.
        /// </remarks>
        void RegisterRenderHooks();
        
        /// <summary>
        /// Enregistre les hooks pour la physique et le Root Motion
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système physique.
        /// Permet l'application du Root Motion à la physique.
        /// </remarks>
        void RegisterPhysicsHooks();
        
        /// <summary>
        /// Enregistre les hooks pour l'IA et la prédiction de mouvement
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système IA.
        /// Permet la synchronisation des comportements IA avec les animations.
        /// </remarks>
        void RegisterAIHooks();
        
        /// <summary>
        /// Enregistre les hooks pour le bus d'événements
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au bus d'événements global.
        /// Permet la communication inter-systèmes.
        /// </remarks>
        void RegisterEventBusHooks();
        
        /// <summary>
        /// Enregistre les hooks pour la gestion des ressources
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de gestion des ressources.
        /// Permet le chargement/déchargement des animations.
        /// </remarks>
        void RegisterResourceHooks();
        
        /// <summary>
        /// Enregistre les hooks pour la gestion des scènes
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de gestion des scènes.
        /// Permet l'activation/désactivation des animations par scène.
        /// </remarks>
        void RegisterSceneHooks();
        
        /// <summary>
        /// Enregistre les hooks pour la gestion des threads
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de gestion des threads.
        /// Permet l'affinité des sous-systèmes à des threads spécifiques.
        /// </remarks>
        void RegisterThreadHooks();
        
        /// <summary>
        /// Enregistre les hooks pour le scheduling des jobs
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de jobs parallèles.
        /// Permet l'exécution asynchrone des tâches d'animation.
        /// </remarks>
        void RegisterJobHooks();
        
        /// <summary>
        /// Enregistre les hooks pour la gestion des caches
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de gestion des caches.
        /// Permet l'optimisation des interpolations et des calculs.
        /// </remarks>
        void RegisterCacheHooks();
        
        /// <summary>
        /// Enregistre les hooks pour le contrôle du budget CPU
        /// </summary>
        /// <remarks>
        /// Connecte le moteur au système de contrôle du budget CPU.
        /// Permet de limiter le temps de calcul par frame.
        /// </remarks>
        void RegisterBudgetHooks();
        
        /// <summary>
        /// Enregistre les zones de profilage
        /// </summary>
        /// <remarks>
        /// Crée des zones de profilage pour mesurer les coûts par sous-système.
        /// Chaque zone correspond à un sous-système spécifique.
        /// </remarks>
        void RegisterProfilerZones();
        
        /// <summary>
        /// Enregistre les gestionnaires d'erreurs
        /// </summary>
        /// <remarks>
        /// Configure les gestionnaires d'erreurs pour les sous-systèmes.
        /// Permet de gérer les exceptions de manière centralisée.
        /// </remarks>
        void RegisterErrorHandlers();
        
        /// <summary>
        /// Enregistre les hooks pour le nettoyage à l'arrêt
        /// </summary>
        /// <remarks>
        /// Configure les hooks pour le nettoyage des ressources à l'arrêt.
        /// Assure une fermeture propre du moteur.
        /// </remarks>
        void RegisterShutdownHooks();

        // Temps d'initialisation et arrêt
        /// <summary>
        /// Obtient le temps d'initialisation d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Temps d'initialisation</returns>
        /// <remarks>
        /// Thread-safe. Mesure le temps nécessaire pour initialiser un sous-système.
        /// Utile pour l'optimisation du boot.
        /// </remarks>
        [ThreadSafe]
        TimeSpan GetSubsystemStartupTime(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient le temps d'arrêt d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Temps d'arrêt</returns>
        /// <remarks>
        /// Thread-safe. Mesure le temps nécessaire pour arrêter un sous-système.
        /// Utile pour l'optimisation du shutdown.
        /// </remarks>
        [ThreadSafe]
        TimeSpan GetSubsystemShutdownTime(AnimationSubsystemType type);

        // Multithreading et affinité
        /// <summary>
        /// Obtient la carte d'affinité des threads des sous-systèmes
        /// </summary>
        /// <returns>Carte d'affinité</returns>
        /// <remarks>
        /// Thread-safe. Montre sur quel thread chaque sous-système est exécuté.
        /// Utile pour l'optimisation du multithreading.
        /// </remarks>
        [ThreadSafe]
        ThreadAffinityMap GetSubsystemThreadAffinityMap();
        
        /// <summary>
        /// Obtient la charge des threads des sous-systèmes
        /// </summary>
        /// <returns>Charge par thread</returns>
        /// <remarks>
        /// Thread-safe. Fournit la charge CPU de chaque thread du moteur.
        /// Utile pour l'équilibrage de charge.
        /// </remarks>
        [ThreadSafe]
        ThreadLoadReport GetSubsystemThreadLoad();

        // Gestion de la mémoire
        /// <summary>
        /// Obtient l'empreinte mémoire d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Empreinte mémoire</returns>
        /// <remarks>
        /// Thread-safe. Mesure la quantité de mémoire utilisée par un sous-système.
        /// Utile pour la détection des fuites mémoire.
        /// </remarks>
        [ThreadSafe]
        long GetSubsystemMemoryFootprint(AnimationSubsystemType type);

        // Caches
        /// <summary>
        /// Obtient l'utilisation du cache d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Utilisation du cache</returns>
        /// <remarks>
        /// Thread-safe. Mesure l'utilisation du cache par un sous-système.
        /// Utile pour l'optimisation des performances.
        /// </remarks>
        [ThreadSafe]
        CacheUsageReport GetSubsystemCacheUsage(AnimationSubsystemType type);

        // Budget CPU
        /// <summary>
        /// Obtient l'utilisation du budget CPU d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Utilisation du budget</returns>
        /// <remarks>
        /// Thread-safe. Mesure le temps CPU utilisé par un sous-système.
        /// Utile pour le contrôle de la performance.
        /// </remarks>
        [ThreadSafe]
        float GetSubsystemBudgetUsage(AnimationSubsystemType type);

        // Profilage et données
        /// <summary>
        /// Obtient les données de profilage d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Données de profilage</returns>
        /// <remarks>
        /// Thread-safe. Fournit des métriques de performance détaillées.
        /// Utile pour l'analyse approfondie.
        /// </remarks>
        [ThreadSafe]
        AnimationSubsystemTelemetryData GetSubsystemTelemetryData(AnimationSubsystemType type);

        // Version et métadonnées
        /// <summary>
        /// Obtient les informations de version d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Informations de version</returns>
        /// <remarks>
        /// Thread-safe. Fournit les numéros de version et de build.
        /// Utile pour le support et le debugging.
        /// </remarks>
        [ThreadSafe]
        VersionInfo GetSubsystemVersionInfo(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient les métadonnées de build d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Métadonnées de build</returns>
        /// <remarks>
        /// Thread-safe. Fournit les détails du build (plateforme, configuration).
        /// Utile pour le support et l'analyse.
        /// </remarks>
        [ThreadSafe]
        BuildMetadata GetSubsystemBuildMetadata(AnimationSubsystemType type);

        // Erreurs et sécurité
        /// <summary>
        /// Obtient la sévérité des erreurs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Sévérité des erreurs</returns>
        /// <remarks>
        /// Thread-safe. Classe les erreurs par niveau de gravité.
        /// Utile pour la priorisation des corrections.
        /// </remarks>
        [ThreadSafe]
        ErrorSeverityReport GetSubsystemErrorSeverity(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient l'état de récupération d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>État de récupération</returns>
        /// <remarks>
        /// Thread-safe. Indique si le sous-système a réussi à se rétablir d'erreurs.
        /// Utile pour la surveillance de la stabilité.
        /// </remarks>
        [ThreadSafe]
        RecoveryStatusReport GetSubsystemRecoveryStatus(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient l'état de sécurité des threads d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>État de sécurité des threads</returns>
        /// <remarks>
        /// Thread-safe. Vérifie la conformité multithreading du sous-système.
        /// Utile pour la détection de conditions de course.
        /// </remarks>
        [ThreadSafe]
        ThreadSafetyReport GetSubsystemThreadSafetyStatus(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient l'état des verrous d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>État des verrous</returns>
        /// <remarks>
        /// Thread-safe. Indique les verrous actifs et leur statut.
        /// Utile pour la détection de deadlocks.
        /// </remarks>
        [ThreadSafe]
        LockStatusReport GetSubsystemLockStatus(AnimationSubsystemType type);

        // Suivi des performances
        /// <summary>
        /// Obtient la tendance des performances d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Tendance des performances</returns>
        /// <remarks>
        /// Thread-safe. Analyse l'évolution des performances dans le temps.
        /// Utile pour la détection de problèmes progressifs.
        /// </remarks>
        [ThreadSafe]
        PerformanceTrendReport GetSubsystemPerformanceTrend(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient la heatmap de charge CPU des sous-systèmes
        /// </summary>
        /// <returns>Heatmap de charge</returns>
        /// <remarks>
        /// Thread-safe. Visualise la charge CPU des sous-systèmes.
        /// Utile pour l'identification des goulets d'étranglement.
        /// </remarks>
        [ThreadSafe]
        CPUHeatmapReport GetSubsystemHeatmap();
        
        /// <summary>
        /// Obtient la timeline des appels des sous-systèmes
        /// </summary>
        /// <returns>Timeline des appels</returns>
        /// <remarks>
        /// Thread-safe. Trace chronologiquement les appels aux sous-systèmes.
        /// Utile pour l'analyse de la séquence d'exécution.
        /// </remarks>
        [ThreadSafe]
        TimelineReport GetSubsystemTimeline();
        
        /// <summary>
        /// Obtient l'historique des événements des sous-systèmes
        /// </summary>
        /// <returns>Historique des événements</returns>
        /// <remarks>
        /// Thread-safe. Fournit les événements récents des sous-systèmes.
        /// Utile pour le debugging historique.
        /// </remarks>
        [ThreadSafe]
        EventHistoryReport GetSubsystemEventHistory();

        // Dépendances et conflits
        /// <summary>
        /// Obtient les conflits de dépendances des sous-systèmes
        /// </summary>
        /// <returns>Conflits de dépendances</returns>
        /// <remarks>
        /// Thread-safe. Identifie les problèmes de dépendance entre sous-systèmes.
        /// Utile pour le diagnostic de configuration.
        /// </remarks>
        [ThreadSafe]
        DependencyConflictReport GetSubsystemDependencyConflicts();

        // Ressources et streaming
        /// <summary>
        /// Obtient l'utilisation des ressources des sous-systèmes
        /// </summary>
        /// <returns>Utilisation des ressources</returns>
        /// <remarks>
        /// Thread-safe. Mesure l'utilisation des ressources (fichiers, mémoire).
        /// Utile pour l'optimisation des assets.
        /// </remarks>
        [ThreadSafe]
        ResourceUsageReport GetSubsystemResourceUsage();
        
        /// <summary>
        /// Obtient l'état de streaming des sous-systèmes
        /// </summary>
        /// <returns>État de streaming</returns>
        /// <remarks>
        /// Thread-safe. Indique l'état de chargement des ressources en streaming.
        /// Utile pour la gestion de la bande passante.
        /// </remarks>
        [ThreadSafe]
        StreamingStatusReport GetSubsystemStreamingStatus();

        // Jobs
        /// <summary>
        /// Obtient la profondeur de la file des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Profondeur de la file</returns>
        /// <remarks>
        /// Thread-safe. Mesure le nombre de jobs en attente.
        /// Utile pour l'équilibrage de charge.
        /// </remarks>
        [ThreadSafe]
        int GetSubsystemJobQueueDepth(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient la latence des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Latence des jobs</returns>
        /// <remarks>
        /// Thread-safe. Mesure le temps de traitement des jobs.
        /// Utile pour l'optimisation de la file d'attente.
        /// </remarks>
        [ThreadSafe]
        JobLatencyReport GetSubsystemJobLatency(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient le débit des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Débit des jobs</returns>
        /// <remarks>
        /// Thread-safe. Mesure le nombre de jobs traités par seconde.
        /// Utile pour l'évaluation de la capacité.
        /// </remarks>
        [ThreadSafe]
        JobThroughputReport GetSubsystemJobThroughput(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient le taux d'échec des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Taux d'échec des jobs</returns>
        /// <remarks>
        /// Thread-safe. Mesure le pourcentage de jobs échoués.
        /// Utile pour la surveillance de la fiabilité.
        /// </remarks>
        [ThreadSafe]
        JobFailureReport GetSubsystemJobFailureRate(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient le nombre de tentatives des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Nombre de tentatives</returns>
        /// <remarks>
        /// Thread-safe. Mesure le nombre de tentatives avant succès.
        /// Utile pour l'analyse de la robustesse.
        /// </remarks>
        [ThreadSafe]
        JobRetryReport GetSubsystemJobRetryCount(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient la distribution des priorités des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Distribution des priorités</returns>
        /// <remarks>
        /// Thread-safe. Analyse la répartition des priorités des jobs.
        /// Utile pour l'équilibrage des priorités.
        /// </remarks>
        [ThreadSafe]
        JobPriorityReport GetSubsystemJobPriorityDistribution(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient le temps d'exécution moyen des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Temps d'exécution moyen</returns>
        /// <remarks>
        /// Thread-safe. Mesure le temps moyen de traitement d'un job.
        /// Utile pour l'optimisation des algorithmes.
        /// </remarks>
        [ThreadSafe]
        JobExecutionTimeReport GetSubsystemJobExecutionTime(AnimationSubsystemType type);
        
        /// <summary>
        /// Obtient le graphe des dépendances des jobs d'un sous-système
        /// </summary>
        /// <param name="type">Type du sous-système</param>
        /// <returns>Graphe des dépendances des jobs</returns>
        /// <remarks>
        /// Thread-safe. Visualise les relations de dépendance entre jobs.
        /// Utile pour l'optimisation de l'ordonnancement.
        /// </remarks>
        [ThreadSafe]
        JobDependencyGraph GetSubsystemJobDependencyGraph(AnimationSubsystemType type);

        // Planificateur - respecte la séparation runtime/setup
        /// <summary>
        /// Obtient le planificateur d'animations
        /// </summary>
        /// <returns>Planificateur d'animations</returns>
        /// <remarks>
        /// Thread-safe. Permet de planifier des tâches d'animation.
        /// </remarks>
        [ThreadSafe]
        AnimationScheduler GetScheduler();
        
        /// <summary>
        /// Planifie une tâche d'animation
        /// </summary>
        /// <param name="task">Tâche à planifier</param>
        /// <param name="priority">Priorité de la tâche</param>
        /// <remarks>
        /// Ajoute une tâche au planificateur avec une priorité donnée.
        /// La tâche sera exécutée selon la politique de priorité.
        /// </remarks>
        void ScheduleAnimationUpdate(AnimationUpdateTask task, AnimationPriority priority);
        
        /// <summary>
        /// Met en pause le planificateur
        /// </summary>
        /// <remarks>
        /// Empêche l'exécution des tâches planifiées.
        /// Utile pour les cinématiques ou les transitions.
        /// </remarks>
        void PauseScheduler();
        
        /// <summary>
        /// Reprend le planificateur
        /// </summary>
        /// <remarks>
        /// Reprend l'exécution des tâches planifiées.
        /// Doit être appelé après PauseScheduler().
        /// </remarks>
        void ResumeScheduler();

        // Thread context - respecte la sécurité multithreading
        /// <summary>
        /// Obtient le contexte d'un thread spécifique
        /// </summary>
        /// <param name="threadId">ID du thread</param>
        /// <returns>Contexte du thread</returns>
        /// <remarks>
        /// Thread-safe. Fournit les informations sur un thread du moteur.
        /// </remarks>
        [ThreadSafe]
        AnimationThreadContext GetThreadContext(int threadId);
        
        /// <summary>
        /// Définit l'affinité d'une instance d'animation à un thread
        /// </summary>
        /// <param name="animationInstanceId">ID de l'instance</param>
        /// <param name="threadId">ID du thread</param>
        /// <remarks>
        /// Affecte une instance d'animation à un thread spécifique.
        /// Utile pour l'optimisation du multithreading.
        /// </remarks>
        void SetThreadAffinity(int animationInstanceId, int threadId);

        // Budget frame - respecte la performance
        /// <summary>
        /// Obtient l'allocateur de budget CPU
        /// </summary>
        /// <returns>Allocateur de budget</returns>
        /// <remarks>
        /// Thread-safe. Gère la limite de temps CPU par frame.
        /// </remarks>
        [ThreadSafe]
        FrameBudgetAllocator GetFrameBudget();
        
        /// <summary>
        /// Définit les paramètres de budget CPU
        /// </summary>
        /// <param name="settings">Paramètres de budget</param>
        /// <remarks>
        /// Configure la limite de temps CPU pour les animations.
        /// Empêche les goulets d'étranglement.
        /// </remarks>
        void SetFrameBudget(FrameBudgetSettings settings);

        // Système de jobs - respecte le multithreading
        /// <summary>
        /// Obtient le système de jobs parallèles
        /// </summary>
        /// <returns>Système de jobs</returns>
        /// <remarks>
        /// Thread-safe. Gère l'exécution des tâches en parallèle.
        /// </remarks>
        [ThreadSafe]
        AnimationJobSystem GetJobSystem();
        
        /// <summary>
        /// Planifie un job d'animation
        /// </summary>
        /// <param name="job">Job à planifier</param>
        /// <returns>Handle du job</returns>
        /// <remarks>
        /// Ajoute un job au système de jobs parallèles.
        /// Retourne un handle pour attendre la complétion.
        /// </remarks>
        AnimationJobHandle ScheduleAnimationJob(AnimationJob job);

        // Gestion du cache - évite les allocations dynamiques
        /// <summary>
        /// Obtient le gestionnaire de cache
        /// </summary>
        /// <returns>Gestionnaire de cache</returns>
        /// <remarks>
        /// Thread-safe. Gère les interpolations et les calculs mis en cache.
        /// </remarks>
        [ThreadSafe]
        AnimationCacheManager GetCacheManager();
        
        /// <summary>
        /// Efface tout le cache d'animation
        /// </summary>
        /// <remarks>
        /// Libère tous les objets mis en cache.
        /// Utile pour libérer de la mémoire ou forcer des recalculs.
        /// </remarks>
        void ClearAnimationCache();
        
        /// <summary>
        /// Invalide le cache d'une instance spécifique
        /// </summary>
        /// <param name="instance">Instance à invalider</param>
        /// <remarks>
        /// Force le recalcul des données pour une instance spécifique.
        /// Utile pour les changements de paramètres.
        /// </remarks>
        void InvalidateCache(AnimationClipInstance instance);

        // Bus d'événements - communication via EventBus
        /// <summary>
        /// Obtient le bus d'événements d'animation
        /// </summary>
        /// <returns>Bus d'événements</returns>
        /// <remarks>
        /// Thread-safe. Gère la communication inter-systèmes.
        /// </remarks>
        [ThreadSafe]
        AnimationEventBus GetEventBus();
        
        /// <summary>
        /// S'abonne à un événement d'animation
        /// </summary>
        /// <param name="eventName">Nom de l'événement</param>
        /// <param name="callback">Fonction de rappel</param>
        /// <remarks>
        /// Enregistre une fonction de rappel pour un événement spécifique.
        /// </remarks>
        void SubscribeToAnimationEvent(string eventName, Action<AnimationEventArgs> callback);
        
        /// <summary>
        /// Déclenche un événement d'animation
        /// </summary>
        /// <param name="evt">Événement à déclencher</param>
        /// <remarks>
        /// Publie un événement sur le bus d'événements.
        /// Les abonnés reçoivent la notification.
        /// </remarks>
        void RaiseEvent(AnimationEvent evt);

        // Gestion des ressources - respecte la gestion de mémoire
        /// <summary>
        /// Obtient le gestionnaire de ressources
        /// </summary>
        /// <returns>Gestionnaire de ressources</returns>
        /// <remarks>
        /// Thread-safe. Gère le chargement/déchargement des animations.
        /// </remarks>
        [ThreadSafe]
        AnimationResourceManager GetResourceManager();
        
        /// <summary>
        /// Enregistre un chargeur de ressources personnalisé
        /// </summary>
        /// <param name="loader">Chargeur de ressources</param>
        /// <remarks>
        /// Permet d'ajouter un chargeur de ressources spécifique.
        /// Utile pour des formats personnalisés.
        /// </remarks>
        void RegisterResourceLoader(IAnimationResourceLoader loader);

        // Profiler - hook pour le profiling
        /// <summary>
        /// Obtient le profiler d'animation
        /// </summary>
        /// <returns>Profiler</returns>
        /// <remarks>
        /// Thread-safe. Fournit des données de performance.
        /// </remarks>
        [ThreadSafe]
        AnimationProfiler GetProfiler();
        
        /// <summary>
        /// Démarre une session de profilage
        /// </summary>
        /// <param name="sessionName">Nom de la session</param>
        /// <remarks>
        /// Commence à collecter des données de performance.
        /// </remarks>
        void StartProfilingSession(string sessionName);
        
        /// <summary>
        /// Arrête la session de profilage actuelle
        /// </summary>
        /// <remarks>
        /// Arrête la collecte de données de performance.
        /// </remarks>
        void StopProfilingSession();

        // Overlay de debug - hook pour le debug
        /// <summary>
        /// Obtient le overlay de debug
        /// </summary>
        /// <returns>Overlay de debug</returns>
        /// <remarks>
        /// Thread-safe. Gère l'affichage des informations de debug.
        /// </remarks>
        [ThreadSafe]
        AnimationDebugOverlay GetDebugOverlay();
        
        /// <summary>
        /// Active/désactive un type de visualisation
        /// </summary>
        /// <param name="type">Type de visualisation</param>
        /// <param name="enabled">État d'activation</param>
        /// <remarks>
        /// Contrôle l'affichage des éléments de debug.
        /// </remarks>
        void ToggleDebugVisualization(DebugVisualizationType type, bool enabled);

        // Logger d'erreurs - respecte la robustesse
        /// <summary>
        /// Obtient le logger d'erreurs
        /// </summary>
        /// <returns>Logger d'erreurs</returns>
        /// <remarks>
        /// Thread-safe. Gère l'enregistrement des erreurs.
        /// </remarks>
        [ThreadSafe]
        AnimationErrorLogger GetErrorLogger();
        
        /// <summary>
        /// Enregistre une erreur d'animation
        /// </summary>
        /// <param name="error">Erreur à enregistrer</param>
        /// <param name="level">Niveau de gravité</param>
        /// <remarks>
        /// Enregistre une erreur avec un niveau de gravité donné.
        /// Utile pour le diagnostic post-mortem.
        /// </remarks>
        void LogAnimationError(Exception error, AnimationErrorLevel level);

        // Registre d'états - gestion des instances actives
        /// <summary>
        /// Obtient le registre des états d'animation
        /// </summary>
        /// <returns>Registre des états</returns>
        /// <remarks>
        /// Thread-safe. Gère les instances d'animation actives.
        /// </remarks>
        [ThreadSafe]
        AnimationStateRegistry GetStateRegistry();
        
        /// <summary>
        /// Enregistre un nouvel état d'animation
        /// </summary>
        /// <param name="state">État à enregistrer</param>
        /// <returns>Référence de l'état</returns>
        /// <remarks>
        /// Enregistre un nouvel état et retourne une référence unique.
        /// </remarks>
        AnimationStateReference RegisterAnimationState(AnimationState state);
        
        /// <summary>
        /// Désenregistre un état d'animation
        /// </summary>
        /// <param name="reference">Référence de l'état à désenregistrer</param>
        /// <remarks>
        /// Supprime un état du registre.
        /// </remarks>
        void UnregisterAnimationState(AnimationStateReference reference);

        // Compilateur de graphes - optimisation
        /// <summary>
        /// Obtient le compilateur de graphes d'animation
        /// </summary>
        /// <returns>Compilateur de graphes</returns>
        /// <remarks>
        /// Thread-safe. Compile les graphes d'animation pour optimisation.
        /// </remarks>
        [ThreadSafe]
        AnimationGraphCompiler GetGraphCompiler();
        
        /// <summary>
        /// Compile un graphe d'animation
        /// </summary>
        /// <param name="graph">Graphe à compiler</param>
        /// <returns>Graphe compilé</returns>
        /// <remarks>
        /// Convertit un graphe d'animation en bytecode optimisé.
        /// Améliore les performances d'exécution.
        /// </remarks>
        CompiledAnimationGraph CompileAnimationGraph(AnimationGraph graph);

        // Système de compression - performance
        /// <summary>
        /// Obtient le compresseur de données d'animation
        /// </summary>
        /// <returns>Compresseur de données</returns>
        /// <remarks>
        /// Thread-safe. Réduit la taille mémoire des animations.
        /// </remarks>
        [ThreadSafe]
        AnimationDataCompressor GetDataCompressor();
        
        /// <summary>
        /// Compresse les données d'un clip
        /// </summary>
        /// <param name="clip">Clip à compresser</param>
        /// <returns>Données compressées</returns>
        /// <remarks>
        /// Réduit la taille mémoire d'un clip d'animation.
        /// Utile pour le streaming ou le stockage.
        /// </remarks>
        byte[] CompressAnimationData(AnimationClip clip);
        
        /// <summary>
        /// Décompresse les données d'animation
        /// </summary>
        /// <param name="compressedData">Données compressées</param>
        /// <returns>Clip d'animation décompressé</returns>
        /// <remarks>
        /// Restaure un clip d'animation à partir de données compressées.
        /// Doit être utilisé avec des données produites par CompressAnimationData.
        /// </remarks>
        AnimationClip DecompressAnimationData(byte[] compressedData);

        // Streaming - gestion mémoire
        /// <summary>
        /// Obtient le système de streaming
        /// </summary>
        /// <returns>Système de streaming</returns>
        /// <remarks>
        /// Thread-safe. Gère le chargement dynamique des animations.
        /// </remarks>
        [ThreadSafe]
        AnimationStreamingSystem GetStreamingSystem();
        
        /// <summary>
        /// Charge un clip en mode asynchrone avec options
        /// </summary>
        /// <param name="path">Chemin du clip</param>
        /// <param name="options">Options de chargement</param>
        /// <returns>Tâche de chargement</returns>
        /// <remarks>
        /// Charge un clip en arrière-plan sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<AnimationClip> LoadClipAsync(string path, AnimationLoadOptions options);

        // 🧩 2. GESTION DES SQUELETTES & CLIPS
        /// <summary>
        /// Crée un nouveau squelette d'animation
        /// </summary>
        /// <param name="name">Nom du squelette</param>
        /// <param name="bones">Définitions des os</param>
        /// <returns>Nouveau squelette</returns>
        /// <remarks>
        /// Crée un squelette à partir d'une définition d'os.
        /// </remarks>
        AnimationSkeleton CreateSkeleton(string name, List<BoneDefinition> bones);
        
        /// <summary>
        /// Détruit un squelette
        /// </summary>
        /// <param name="skeleton">Squelette à détruire</param>
        /// <remarks>
        /// Libère les ressources associées au squelette.
        /// </remarks>
        void DestroySkeleton(AnimationSkeleton skeleton);

        // Chargement asynchrone et gestion des squelettes
        /// <summary>
        /// Charge un squelette en mode asynchrone
        /// </summary>
        /// <param name="path">Chemin du squelette</param>
        /// <returns>Tâche de chargement</returns>
        /// <remarks>
        /// Charge un squelette en arrière-plan sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<AnimationSkeleton> LoadSkeletonAsync(string path);
        
        /// <summary>
        /// Charge un squelette depuis le réseau
        /// </summary>
        /// <param name="url">URL du squelette</param>
        /// <returns>Tâche de chargement</returns>
        /// <remarks>
        /// Charge un squelette depuis une URL distante.
        /// Utile pour le streaming ou les contenus générés par l'utilisateur.
        /// </remarks>
        [Async]
        Task<AnimationSkeleton> LoadSkeletonFromNetwork(string url);
        
        /// <summary>
        /// Charge un squelette depuis un format binaire compressé
        /// </summary>
        /// <param name="data">Données binaires</param>
        /// <returns>Squelette chargé</returns>
        /// <remarks>
        /// Crée un squelette à partir de données binaires.
        /// Utile pour les formats optimisés ou compressés.
        /// </remarks>
        AnimationSkeleton LoadSkeletonFromBinary(byte[] data);
        
        /// <summary>
        /// Décharge un squelette de la mémoire en mode asynchrone
        /// </summary>
        /// <param name="skeleton">Squelette à décharger</param>
        /// <returns>Tâche de déchargement</returns>
        /// <remarks>
        /// Libère un squelette en arrière-plan.
        /// </remarks>
        [Async]
        Task UnloadSkeletonAsync(AnimationSkeleton skeleton);
        
        /// <summary>
        /// Valide la topologie d'un squelette
        /// </summary>
        /// <param name="skeleton">Squelette à valider</param>
        /// <returns>True si la topologie est valide</returns>
        /// <remarks>
        /// Vérifie que la hiérarchie des os est correcte.
        /// </remarks>
        bool ValidateSkeletonTopology(AnimationSkeleton skeleton);
        
        /// <summary>
        /// Compresse la hiérarchie d'un squelette
        /// </summary>
        /// <param name="skeleton">Squelette à compresser</param>
        /// <returns>Données compressées</returns>
        /// <remarks>
        /// Réduit la taille mémoire de la hiérarchie des os.
        /// </remarks>
        byte[] CompressSkeletonHierarchy(AnimationSkeleton skeleton);

        // Support multi-squelette
        /// <summary>
        /// Crée un squelette multi-parties
        /// </summary>
        /// <param name="skeletons">Liste des squelettes</param>
        /// <returns>Squelette multi-parties</returns>
        /// <remarks>
        /// Combine plusieurs squelettes en un seul squelette composite.
        /// </remarks>
        MultiSkeleton CreateMultiSkeleton(List<AnimationSkeleton> skeletons);
        
        /// <summary>
        /// Ajoute un squelette à un squelette multi-parties
        /// </summary>
        /// <param name="multi">Squelette multi-parties</param>
        /// <param name="skeleton">Squelette à ajouter</param>
        /// <remarks>
        /// Ajoute un squelette au squelette composite.
        /// </remarks>
        void AddSkeletonToMulti(MultiSkeleton multi, AnimationSkeleton skeleton);
        
        /// <summary>
        /// Supprime un squelette d'un squelette multi-parties
        /// </summary>
        /// <param name="multi">Squelette multi-parties</param>
        /// <param name="skeleton">Squelette à supprimer</param>
        /// <remarks>
        /// Supprime un squelette du squelette composite.
        /// </remarks>
        void RemoveSkeletonFromMulti(MultiSkeleton multi, AnimationSkeleton skeleton);

        // Squelettes dynamiques
        /// <summary>
        /// Ajoute un os à un squelette
        /// </summary>
        /// <param name="skeleton">Squelette cible</param>
        /// <param name="bone">Définition de l'os</param>
        /// <remarks>
        /// Modifie dynamiquement la structure du squelette.
        /// </remarks>
        void AddBoneToSkeleton(AnimationSkeleton skeleton, BoneDefinition bone);
        
        /// <summary>
        /// Supprime un os d'un squelette
        /// </summary>
        /// <param name="skeleton">Squelette cible</param>
        /// <param name="boneName">Nom de l'os</param>
        /// <remarks>
        /// Modifie dynamiquement la structure du squelette.
        /// </remarks>
        void RemoveBoneFromSkeleton(AnimationSkeleton skeleton, string boneName);

        // Compression des squelettes
        /// <summary>
        /// Compresse un squelette
        /// </summary>
        /// <param name="skeleton">Squelette à compresser</param>
        /// <returns>Données compressées</returns>
        /// <remarks>
        /// Réduit la taille mémoire du squelette.
        /// </remarks>
        byte[] CompressSkeleton(AnimationSkeleton skeleton);
        
        /// <summary>
        /// Décompresse un squelette
        /// </summary>
        /// <param name="compressedData">Données compressées</param>
        /// <returns>Squelette décompressé</returns>
        /// <remarks>
        /// Restaure un squelette à partir de données compressées.
        /// </remarks>
        AnimationSkeleton DecompressSkeleton(byte[] compressedData);

        // Retargeting automatique
        /// <summary>
        /// Effectue un retargeting automatique entre squelettes
        /// </summary>
        /// <param name="source">Squelette source</param>
        /// <param name="target">Squelette cible</param>
        /// <returns>Résultat du retargeting</returns>
        /// <remarks>
        /// Convertit une animation d'un squelette à un autre.
        /// </remarks>
        AnimationRetargetingResult AutoRetargetSkeleton(AnimationSkeleton source, AnimationSkeleton target);

        // Morph targets
        /// <summary>
        /// Applique un morph target à une instance
        /// </summary>
        /// <param name="instance">Instance cible</param>
        /// <param name="targetName">Nom du morph target</param>
        /// <param name="weight">Poids d'application</param>
        /// <remarks>
        /// Applique un morph target à une instance d'animation.
        /// </remarks>
        void ApplyMorphTarget(AnimationClipInstance instance, string targetName, float weight);
        
        /// <summary>
        /// Enregistre un morph target
        /// </summary>
        /// <param name="skeleton">Squelette associé</param>
        /// <param name="targetName">Nom du morph target</param>
        /// <param name="data">Données du morph target</param>
        /// <remarks>
        /// Enregistre un morph target pour un squelette spécifique.
        /// </remarks>
        void RegisterMorphTarget(AnimationSkeleton skeleton, string targetName, MorphTargetData data);

        // Blendshapes GPU
        /// <summary>
        /// Active le skinning GPU pour les blendshapes
        /// </summary>
        /// <param name="mesh">Maillage concerné</param>
        /// <remarks>
        /// Utilise le GPU pour le skinning des blendshapes.
        /// </remarks>
        void EnableGPUBlendshapes(SkinnedMesh mesh);
        
        /// <summary>
        /// Désactive le skinning GPU pour les blendshapes
        /// </summary>
        /// <param name="mesh">Maillage concerné</param>
        /// <remarks>
        /// Désactive le skinning GPU et revient au skinning CPU.
        /// </remarks>
        void DisableGPUBlendshapes(SkinnedMesh mesh);

        // Chargement asynchrone
        /// <summary>
        /// Charge un clip en mode asynchrone
        /// </summary>
        /// <param name="path">Chemin du clip</param>
        /// <returns>Tâche de chargement</returns>
        /// <remarks>
        /// Charge un clip d'animation en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationClip> LoadClipAsync(string path);
        
        /// <summary>
        /// Précharge les clips critiques
        /// </summary>
        /// <param name="clipPaths">Chemins des clips à précharger</param>
        /// <remarks>
        /// Charge les clips en mémoire avant leur utilisation.
        /// </remarks>
        void PreloadCriticalClips(List<string> clipPaths);

        // Streaming
        /// <summary>
        /// Démarre le streaming d'un clip en mode asynchrone
        /// </summary>
        /// <param name="path">Chemin du clip</param>
        /// <param name="settings">Paramètres de streaming</param>
        /// <returns>Tâche de streaming</returns>
        /// <remarks>
        /// Démarre le chargement progressif d'un clip.
        /// </remarks>
        [Async]
        Task StreamClipAsync(string path, AnimationStreamingSettings settings);
        
        /// <summary>
        /// Décharge un clip en mode asynchrone
        /// </summary>
        /// <param name="clip">Clip à décharger</param>
        /// <returns>Tâche de déchargement</returns>
        /// <remarks>
        /// Libère un clip de la mémoire en arrière-plan.
        /// </remarks>
        [Async]
        Task UnloadAnimationClipAsync(AnimationClip clip);
        
        /// <summary>
        /// Précharge un ensemble de clips
        /// </summary>
        /// <param name="clipSet">Ensemble de clips à précharger</param>
        /// <remarks>
        /// Charge un ensemble de clips en mémoire.
        /// </remarks>
        void PreloadAnimationSet(List<string> clipSet);
        
        /// <summary>
        /// Annule le streaming d'un clip
        /// </summary>
        /// <param name="path">Chemin du clip</param>
        /// <remarks>
        /// Arrête le chargement progressif d'un clip.
        /// </remarks>
        void CancelClipStream(string path);

        // Gestion des clips
        /// <summary>
        /// Charge un clip d'animation
        /// </summary>
        /// <param name="path">Chemin du clip</param>
        /// <returns>Clip chargé</returns>
        /// <remarks>
        /// Charge un clip d'animation depuis le disque.
        /// </remarks>
        AnimationClip LoadClip(string path);
        
        /// <summary>
        /// Décharge un clip d'animation
        /// </summary>
        /// <param name="clip">Clip à décharger</param>
        /// <remarks>
        /// Libère un clip d'animation de la mémoire.
        /// </remarks>
        void UnloadClip(AnimationClip clip);
        
        /// <summary>
        /// Crée une instance d'un clip
        /// </summary>
        /// <param name="clip">Clip source</param>
        /// <param name="skeleton">Squelette associé</param>
        /// <returns>Nouvelle instance</returns>
        /// <remarks>
        /// Crée une instance exécutable d'un clip.
        /// </remarks>
        AnimationClipInstance CreateInstance(AnimationClip clip, AnimationSkeleton skeleton);
        
        /// <summary>
        /// Détruit une instance de clip
        /// </summary>
        /// <param name="instance">Instance à détruire</param>
        /// <remarks>
        /// Libère une instance de clip.
        /// </remarks>
        void DestroyInstance(AnimationClipInstance instance);

        // Gestion avancée des clips
        /// <summary>
        /// Décharge un clip spécifique
        /// </summary>
        /// <param name="path">Chemin du clip</param>
        /// <returns>Clip déchargé</returns>
        /// <remarks>
        /// Libère un clip spécifique de la mémoire.
        /// </remarks>
        AnimationClip UnloadAnimationClip(string path);
        
        /// <summary>
        /// Précharge des clips critiques
        /// </summary>
        /// <param name="clipPathsList">Liste des chemins</param>
        /// <returns>Clips préchargés</returns>
        /// <remarks>
        /// Charge une liste de clips en mémoire.
        /// </remarks>
        AnimationClip PreloadCriticalClips(List<string> clipPathsList);
        
        /// <summary>
        /// Obtient les métadonnées d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Métadonnées</returns>
        /// <remarks>
        /// Thread-safe. Fournit des informations sur un clip.
        /// </remarks>
        [ThreadSafe]
        AnimationClipMetadata GetClipMetadata(AnimationClip clip);
        
        /// <summary>
        /// Obtient les dépendances d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Liste des clips dépendants</returns>
        /// <remarks>
        /// Thread-safe. Fournit les clips requis par un clip.
        /// </remarks>
        [ThreadSafe]
        List<AnimationClip> GetClipDependencies(AnimationClip clip);
        
        /// <summary>
        /// Obtient l'utilisation mémoire d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Utilisation mémoire en octets</returns>
        /// <remarks>
        /// Thread-safe. Mesure la mémoire utilisée par un clip.
        /// </remarks>
        [ThreadSafe]
        long GetClipMemoryUsage(AnimationClip clip);
        
        /// <summary>
        /// Obtient le nombre de frames d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Nombre de frames</returns>
        /// <remarks>
        /// Thread-safe. Compte le nombre de frames dans un clip.
        /// </remarks>
        [ThreadSafe]
        int GetClipFrameCount(AnimationClip clip);
        
        /// <summary>
        /// Obtient la durée d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Durée en secondes</returns>
        /// <remarks>
        /// Thread-safe. Calcule la durée totale d'un clip.
        /// </returns>
        [ThreadSafe]
        float GetClipDuration(AnimationClip clip);
        
        /// <summary>
        /// Obtient la fréquence d'échantillonnage d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Fréquence en Hz</returns>
        /// <remarks>
        /// Thread-safe. Fournit la fréquence d'échantillonnage.
        /// </remarks>
        [ThreadSafe]
        int GetClipFrameRate(AnimationClip clip);
        
        /// <summary>
        /// Obtient le taux de compression d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Taux de compression</returns>
        /// <remarks>
        /// Thread-safe. Calcule le taux de compression du clip.
        /// </remarks>
        [ThreadSafe]
        float GetClipCompressionRatio(AnimationClip clip);
        
        /// <summary>
        /// Obtient la qualité d'interpolation des frames d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Qualité d'interpolation</returns>
        /// <remarks>
        /// Thread-safe. Évalue la qualité des interpolations.
        /// </remarks>
        [ThreadSafe]
        float GetClipFrameInterpolationQuality(AnimationClip clip);
        
        /// <summary>
        /// Obtient la qualité de compression d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Qualité de compression</returns>
        /// <remarks>
        /// Thread-safe. Évalue la perte due à la compression.
        /// </remarks>
        [ThreadSafe]
        float GetClipCompressionQuality(AnimationClip clip);
        
        /// <summary>
        /// Obtient l'état de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>État de streaming</returns>
        /// <remarks>
        /// Thread-safe. Indique l'avancement du streaming.
        /// </remarks>
        [ThreadSafe]
        AnimationClipStreamingStatus GetClipStreamingStatus(AnimationClip clip);
        
        /// <summary>
        /// Obtient le rapport d'erreurs d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Rapport d'erreurs</returns>
        /// <remarks>
        /// Thread-safe. Fournit les erreurs liées à un clip.
        /// </remarks>
        [ThreadSafe]
        AnimationClipErrorReport GetClipErrorReport(AnimationClip clip);
        
        /// <summary>
        /// Obtient l'état de retargeting d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>État de retargeting</returns>
        /// <remarks>
        /// Thread-safe. Indique si le retargeting est possible.
        /// </remarks>
        [ThreadSafe]
        AnimationClipRetargetingStatus GetClipRetargetingStatus(AnimationClip clip);
        
        /// <summary>
        /// Obtient les données de motion matching d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Données de motion matching</returns>
        /// <remarks>
        /// Thread-safe. Fournit les données pour le motion matching.
        /// </remarks>
        [ThreadSafe]
        MotionMatchingDatabase GetClipMotionMatchingData(AnimationClip clip);
        
        /// <summary>
        /// Obtient les courbes d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Liste des courbes</returns>
        /// <remarks>
        /// Thread-safe. Fournit les courbes d'animation.
        /// </remarks>
        [ThreadSafe]
        List<AnimationCurve> GetClipCurveData(AnimationClip clip);
        
        /// <summary>
        /// Obtient les événements d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Liste des événements</returns>
        /// <remarks>
        /// Thread-safe. Fournit les événements programmés.
        /// </remarks>
        [ThreadSafe]
        List<AnimationEvent> GetClipEventData(AnimationClip clip);

        // Gestion avancée du streaming
        /// <summary>
        /// Obtient la latence de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Latence de streaming</returns>
        /// <remarks>
        /// Thread-safe. Mesure le délai de chargement.
        /// </remarks>
        [ThreadSafe]
        TimeSpan GetClipStreamingLatency(AnimationClip clip);
        
        /// <summary>
        /// Obtient la bande passante de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Bande passante de streaming</returns>
        /// <remarks>
        /// Thread-safe. Mesure le débit de chargement.
        /// </remarks>
        [ThreadSafe]
        float GetClipStreamingBandwidth(AnimationClip clip);
        
        /// <summary>
        /// Obtient les erreurs de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Erreurs de streaming</returns>
        /// <remarks>
        /// Thread-safe. Fournit les erreurs de chargement.
        /// </remarks>
        [ThreadSafe]
        List<StreamingError> GetClipStreamingErrors(AnimationClip clip);
        
        /// <summary>
        /// Obtient le nombre de tentatives de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Nombre de tentatives</returns>
        /// <remarks>
        /// Thread-safe. Compte les tentatives de chargement.
        /// </remarks>
        [ThreadSafe]
        int GetClipStreamingRetryCount(AnimationClip clip);
        
        /// <summary>
        /// Obtient le taux de cache de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Taux de cache de streaming</returns>
        /// <remarks>
        /// Thread-safe. Mesure l'efficacité du cache.
        /// </remarks>
        [ThreadSafe]
        float GetClipStreamingCacheHitRate(AnimationClip clip);
        
        /// <summary>
        /// Obtient l'affinité de thread de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Affinité de thread de streaming</returns>
        /// <remarks>
        /// Thread-safe. Indique le thread de chargement.
        /// </remarks>
        [ThreadSafe]
        int GetClipStreamingThreadAffinity(AnimationClip clip);
        
        /// <summary>
        /// Obtient l'utilisation de budget de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Utilisation de budget de streaming</returns>
        /// <remarks>
        /// Thread-safe. Mesure le coût CPU du streaming.
        /// </remarks>
        [ThreadSafe]
        float GetClipStreamingBudgetUsage(AnimationClip clip);
        
        /// <summary>
        /// Obtient les données de profilage de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Données de profilage de streaming</returns>
        /// <remarks>
        /// Thread-safe. Fournit les métriques de performance.
        /// </remarks>
        [ThreadSafe]
        AnimationClipStreamingProfilerData GetClipStreamingProfilerData(AnimationClip clip);
        
        /// <summary>
        /// Obtient les données de télémétrie de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Données de télémétrie de streaming</returns>
        /// <remarks>
        /// Thread-safe. Fournit les données de monitoring.
        /// </remarks>
        [ThreadSafe]
        AnimationClipStreamingTelemetryData GetClipStreamingTelemetryData(AnimationClip clip);
        
        /// <summary>
        /// Obtient les informations de version de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Informations de version de streaming</returns>
        /// <remarks>
        /// Thread-safe. Fournit les numéros de version.
        /// </remarks>
        [ThreadSafe]
        VersionInfo GetClipStreamingVersionInfo(AnimationClip clip);
        
        /// <summary>
        /// Obtient les métadonnées de build de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Métadonnées de build de streaming</returns>
        /// <remarks>
        /// Thread-safe. Fournit les détails du build.
        /// </remarks>
        [ThreadSafe]
        BuildMetadata GetClipStreamingBuildMetadata(AnimationClip clip);
        
        /// <summary>
        /// Obtient la sévérité des erreurs de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>Sévérité des erreurs de streaming</returns>
        /// <remarks>
        /// Thread-safe. Classe les erreurs par gravité.
        /// </remarks>
        [ThreadSafe]
        ErrorSeverityReport GetClipStreamingErrorSeverity(AnimationClip clip);
        
        /// <summary>
        /// Obtient l'état de récupération de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>État de récupération de streaming</returns>
        /// <remarks>
        /// Thread-safe. Indique le statut de récupération.
        /// </remarks>
        [ThreadSafe]
        RecoveryStatusReport GetClipStreamingRecoveryStatus(AnimationClip clip);
        
        /// <summary>
        /// Obtient l'état de sécurité des threads de streaming d'un clip
        /// </summary>
        /// <param name="clip">Clip concerné</param>
        /// <returns>État de sécurité des threads de streaming</returns>
        /// <remarks>
        /// Thread-safe. Vérifie la sécurité multithread.
        /// </remarks>
        [ThreadSafe]
        ThreadSafetyReport GetClipStreamingThreadSafetyStatus(AnimationClip clip);

        // Types spéciaux de clips
        /// <summary>
        /// Crée un clip additif
        /// </summary>
        /// <param name="baseClip">Clip de base</param>
        /// <param name="additiveClip">Clip additif</param>
        /// <returns>Clip combiné</returns>
        /// <remarks>
        /// Combine deux clips en un clip additif.
        /// </remarks>
        AnimationClip CreateAdditiveClip(AnimationClip baseClip, AnimationClip additiveClip);
        
        /// <summary>
        /// Crée un clip procédural
        /// </summary>
        /// <param name="generator">Générateur de clip</param>
        /// <returns>Clip procédural</returns>
        /// <remarks>
        /// Génère un clip à partir d'une fonction.
        /// </remarks>
        AnimationClip CreateProceduralClip(Func<float, AnimationClip> generator);
        
        /// <summary>
        /// Crée un clip d'événement
        /// </summary>
        /// <param name="eventData">Données de l'événement</param>
        /// <returns>Clip d'événement</returns>
        /// <remarks>
        /// Crée un clip qui déclenche un événement.
        /// </remarks>
        AnimationClip CreateEventClip(AnimationEvent eventData);
        
        /// <summary>
        /// Crée un clip de transition
        /// </summary>
        /// <param name="from">Clip de départ</param>
        /// <param name="to">Clip d'arrivée</param>
        /// <param name="duration">Durée de la transition</param>
        /// <returns>Clip de transition</returns>
        /// <remarks>
        /// Crée un clip pour une transition fluide.
        /// </remarks>
        AnimationClip CreateTransitionClip(AnimationClip from, AnimationClip to, float duration);
        
        /// <summary>
        /// Crée un clip de secours
        /// </summary>
        /// <param name="fallback">Clip de secours</param>
        /// <returns>Clip de secours</returns>
        /// <remarks>
        /// Crée un clip à utiliser en cas d'erreur.
        /// </remarks>
        AnimationClip CreateFallbackClip(AnimationClip fallback);
        
        /// <summary>
        /// Crée un clip avec LOD
        /// </summary>
        /// <param name="original">Clip original</param>
        /// <param name="lodSettings">Paramètres de LOD</param>
        /// <returns>Clip avec LOD</returns>
        /// <remarks>
        /// Crée une version simplifiée d'un clip.
        /// </remarks>
        AnimationClip CreateLODClip(AnimationClip original, AnimationLODSettings lodSettings);
        
        /// <summary>
        /// Crée un clip de debug
        /// </summary>
        /// <param name="pose">Pose de debug</param>
        /// <returns>Clip de debug</returns>
        /// <remarks>
        /// Crée un clip pour le debug visuel.
        /// </remarks>
        AnimationClip CreateDebugClip(PoseSnapshot pose);
        
        /// <summary>
        /// Crée un clip de cinématique
        /// </summary>
        /// <param name="sequence">Séquence de cinématique</param>
        /// <returns>Clip de cinématique</returns>
        /// <remarks>
        /// Crée un clip pour une séquence cinématique.
        /// </remarks>
        AnimationClip CreateCinematicClip(CinematicSequence sequence);
        
        /// <summary>
        /// Crée un clip de gameplay
        /// </summary>
        /// <param name="data">Données de gameplay</param>
        /// <returns>Clip de gameplay</returns>
        /// <remarks>
        /// Crée un clip pour une action de gameplay.
        /// </remarks>
        AnimationClip CreateGameplayClip(GameplayAnimationData data);
        
        /// <summary>
        /// Crée un clip de caméra
        /// </summary>
        /// <param name="data">Données de caméra</param>
        /// <returns>Clip de caméra</returns>
        /// <remarks>
        /// Crée un clip pour le mouvement de caméra.
        /// </remarks>
        AnimationClip CreateCameraClip(CameraAnimationData data);

        // 🎨 3. BLENDSPACES & TRANSITIONS
        /// <summary>
        /// Évalue un blendspace 1D
        /// </summary>
        /// <param name="blendSpace">Blendspace à évaluer</param>
        /// <param name="parameter">Paramètre d'évaluation</param>
        /// <returns>Instance de clip résultante</returns>
        /// <remarks>
        /// Calcule le résultat d'un blendspace 1D.
        /// </remarks>
        AnimationClipInstance EvaluateBlendSpace1D(BlendSpace1D blendSpace, float parameter);
        
        /// <summary>
        /// Évalue un blendspace 2D
        /// </summary>
        /// <param name="blendSpace">Blendspace à évaluer</param>
        /// <param name="parameters">Paramètres d'évaluation</param>
        /// <returns>Instance de clip résultante</returns>
        /// <remarks>
        /// Calcule le résultat d'un blendspace 2D.
        /// </remarks>
        AnimationClipInstance EvaluateBlendSpace2D(BlendSpace2D blendSpace, Vector2 parameters);
        
        /// <summary>
        /// Évalue un blendspace 3D
        /// </summary>
        /// <param name="blendSpace">Blendspace à évaluer</param>
        /// <param name="parameters">Paramètres d'évaluation</param>
        /// <returns>Instance de clip résultante</returns>
        /// <remarks>
        /// Calcule le résultat d'un blendspace 3D.
        /// </remarks>
        AnimationClipInstance EvaluateBlendSpace3D(BlendSpace3D blendSpace, Vector3 parameters);

        // Gestion avancée des blendspaces
        /// <summary>
        /// Crée un blendspace dynamiquement en mode asynchrone
        /// </summary>
        /// <param name="type">Type de blendspace</param>
        /// <returns>Tâche de création</returns>
        /// <remarks>
        /// Crée un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationBlendSpace> CreateBlendSpaceAsync(AnimationBlendSpaceType type);
        
        /// <summary>
        /// Évalue un blendspace avec des paramètres en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace à évaluer</param>
        /// <param name="parameters">Paramètres d'évaluation</param>
        /// <returns>Tâche d'évaluation</returns>
        /// <remarks>
        /// Évalue un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationBlendWeights> EvaluateBlendSpaceAsync(AnimationBlendSpace blendSpace, List<float> parameters);
        
        /// <summary>
        /// Met à jour un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace à mettre à jour</param>
        /// <param name="settings">Paramètres de mise à jour</param>
        /// <returns>Tâche de mise à jour</returns>
        /// <remarks>
        /// Met à jour un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task UpdateBlendSpaceAsync(AnimationBlendSpace blendSpace, AnimationBlendSpaceSettings settings);
        
        /// <summary>
        /// Efface le cache d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace à effacer</param>
        /// <returns>Tâche d'effacement</returns>
        /// <remarks>
        /// Efface le cache d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task ClearBlendSpaceCacheAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les poids d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des poids</returns>
        /// <remarks>
        /// Récupère les poids d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationBlendWeights> GetBlendSpaceWeightsAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les dimensions d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des dimensions</returns>
        /// <remarks>
        /// Récupère les dimensions d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationBlendSpaceDimensions> GetBlendSpaceDimensionsAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient l'utilisation mémoire d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération de l'utilisation mémoire</returns>
        /// <remarks>
        /// Récupère l'utilisation mémoire d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<long> GetBlendSpaceMemoryUsageAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de performance d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de performance</returns>
        /// <remarks>
        /// Récupère les données de performance d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationBlendSpacePerformanceData> GetBlendSpacePerformanceDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient le rapport d'erreurs d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération du rapport d'erreurs</returns>
        /// <remarks>
        /// Récupère le rapport d'erreurs d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationBlendSpaceErrorReport> GetBlendSpaceErrorReportAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de transition d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de transition</returns>
        /// <remarks>
        /// Récupère les données de transition d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationBlendSpaceTransitionData> GetBlendSpaceTransitionDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les courbes d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des courbes</returns>
        /// <remarks>
        /// Récupère les courbes d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<List<AnimationCurve>> GetBlendSpaceCurveDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les paramètres de LOD d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des paramètres de LOD</returns>
        /// <remarks>
        /// Récupère les paramètres de LOD d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationLODSettings> GetBlendSpaceLODSettingsAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de retargeting d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de retargeting</returns>
        /// <remarks>
        /// Récupère les données de retargeting d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationRetargetingData> GetBlendSpaceRetargetingDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de motion matching d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de motion matching</returns>
        /// <remarks>
        /// Récupère les données de motion matching d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<MotionMatchingDatabase> GetBlendSpaceMotionMatchingDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données d'IK d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données d'IK</returns>
        /// <remarks>
        /// Récupère les données d'IK d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationIKSettings> GetBlendSpaceIKDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de synchronisation audio d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de synchronisation audio</returns>
        /// <remarks>
        /// Récupère les données de synchronisation audio d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationAudioSyncData> GetBlendSpaceAudioSyncDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de synchronisation de rendu d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de synchronisation de rendu</returns>
        /// <remarks>
        /// Récupère les données de synchronisation de rendu d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationRenderSyncData> GetBlendSpaceRenderSyncDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de synchronisation physique d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de synchronisation physique</returns>
        /// <remarks>
        /// Récupère les données de synchronisation physique d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationPhysicsSyncData> GetBlendSpacePhysicsSyncDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données d'IA d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données d'IA</returns>
        /// <remarks>
        /// Récupère les données d'IA d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationAIData> GetBlendSpaceAIDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de debug d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de debug</returns>
        /// <remarks>
        /// Récupère les données de debug d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationDebugData> GetBlendSpaceDebugDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les données de profilage d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des données de profilage</returns>
        /// <remarks>
        /// Récupère les données de profilage d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationProfilerData> GetBlendSpaceProfilerDataAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient l'état du cache d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération de l'état du cache</returns>
        /// <remarks>
        /// Récupère l'état du cache d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<AnimationCacheStatus> GetBlendSpaceCacheStatusAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient l'affinité de thread d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération de l'affinité de thread</returns>
        /// <remarks>
        /// Récupère l'affinité de thread d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<int> GetBlendSpaceThreadAffinityAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient l'utilisation de budget d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération de l'utilisation de budget</returns>
        /// <remarks>
        /// Récupère l'utilisation de budget d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<float> GetBlendSpaceBudgetUsageAsync(AnimationBlendSpace blendSpace);
        
        /// <summary>
        /// Obtient les événements d'un blendspace en mode asynchrone
        /// </summary>
        /// <param name="blendSpace">Blendspace concerné</param>
        /// <returns>Tâche de récupération des événements</returns>
        /// <remarks>
        /// Récupère les événements d'un blendspace en arrière-plan.
        /// </remarks>
        [Async]
        Task<List<AnimationEvent>> GetBlendSpaceEventDataAsync(AnimationBlendSpace blendSpace);

        // Blendspaces spécifiques
        AnimationClipInstance EvaluateAdaptiveBlendSpace(AdaptiveBlendSpace blendSpace, AnimationClipInstance instance);
        AnimationClipInstance EvaluateTerrainBlendSpace(TerrainAdaptiveBlendSpace blendSpace, Vector3 worldPos);
        AnimationClipInstance EvaluateEmotionalBlendSpace(EmotionalBlendSpace blendSpace, EmotionalState state);
        AnimationClipInstance EvaluateCombatBlendSpace(CombatBlendSpace blendSpace, CombatState state);
        AnimationClipInstance EvaluateStealthBlendSpace(StealthBlendSpace blendSpace, StealthState state);
        AnimationClipInstance EvaluateInjuryBlendSpace(InjuryBlendSpace blendSpace, InjuryState state);
        AnimationClipInstance EvaluateRecoveryBlendSpace(RecoveryBlendSpace blendSpace, RecoveryState state);
        AnimationClipInstance EvaluateClimbingBlendSpace(ClimbingBlendSpace blendSpace, ClimbingState state);
        AnimationClipInstance EvaluateSwimmingBlendSpace(SwimmingBlendSpace blendSpace, SwimmingState state);
        AnimationClipInstance EvaluateVehicleBlendSpace(VehicleBlendSpace blendSpace, VehicleState state);
        AnimationClipInstance EvaluateWeaponBlendSpace(WeaponBlendSpace blendSpace, WeaponState state);
        AnimationClipInstance EvaluateToolBlendSpace(ToolBlendSpace blendSpace, ToolState state);
        AnimationClipInstance EvaluateEnvironmentalBlendSpace(EnvironmentalBlendSpace blendSpace, EnvironmentalState state);
        AnimationClipInstance EvaluateCameraBlendSpace(CameraBlendSpace blendSpace, CameraState state);
        AnimationClipInstance EvaluateAIControlledBlendSpace(AIControlledBlendSpace blendSpace, AIState state);
        AnimationClipInstance EvaluateAudioSynchronizedBlendSpace(AudioSynchronizedBlendSpace blendSpace, AudioState state);
        AnimationClipInstance EvaluatePhysicsBasedBlendSpace(PhysicsBasedBlendSpace blendSpace, PhysicsState state);
        AnimationClipInstance EvaluateGameplayStateBlendSpace(GameplayStateBlendSpace blendSpace, GameplayState state);
        AnimationClipInstance EvaluateMultiCharacterBlendSpace(MultiCharacterBlendSpace blendSpace, List<CharacterState> characterStates);
        AnimationClipInstance EvaluateThreadedBlendSpace(ThreadedBlendSpace blendSpace, int threadId);

        // Transitions adaptatives
        void SetupAdaptiveTransition(AnimationClipInstance from, AnimationClipInstance to, AnimationTransitionRule rule);
        AnimationClipInstance CrossFade(AnimationClipInstance from, AnimationClipInstance to, float duration);
        AnimationClipInstance SmartCrossFade(AnimationClipInstance from, AnimationClipInstance to, SmartCrossFadeSettings settings);

        // 🧱 4. ROOT MOTION & SYNCHRONISATION
        RootMotionDelta ExtractRootMotion(AnimationClipInstance instance, float deltaTime);
        void ApplyRootMotionOverride(AnimationClipInstance instance, Vector3 overridePosition, Vector3 overrideRotation);
        void CorrectRootMotionForTerrain(AnimationClipInstance instance, Vector3 worldPosition);

        // Corrections spécifiques
        RootMotionDelta ApplyFrictionCorrection(RootMotionDelta delta, float friction);
        RootMotionDelta ApplySlopeCorrection(RootMotionDelta delta, Vector3 normal);
        RootMotionDelta ApplyCollisionCorrection(RootMotionDelta delta, CollisionData collision);
        RootMotionDelta ApplyFatigueCorrection(RootMotionDelta delta, float fatigue);
        RootMotionDelta ApplySurfaceCorrection(RootMotionDelta delta, SurfaceType surface);
        RootMotionDelta ApplyWeatherCorrection(RootMotionDelta delta, WeatherType weather);
        RootMotionDelta ApplyTerrainCorrection(RootMotionDelta delta, TerrainType terrain);
        RootMotionDelta ApplyCameraCorrection(RootMotionDelta delta, CameraState camera);
        RootMotionDelta ApplyAICorrection(RootMotionDelta delta, AIState aiState);
        RootMotionDelta ApplyGameplayCorrection(RootMotionDelta delta, GameplayState gameState);
        RootMotionDelta ApplyNetworkCorrection(RootMotionDelta delta, NetworkState networkState);
        RootMotionDelta ApplyLODCorrection(RootMotionDelta delta, AnimationLODSettings lodSettings);
        RootMotionDelta ApplyGPUCorrection(RootMotionDelta delta, GPUSkinningState gpuState);
        RootMotionDelta ApplyThreadCorrection(RootMotionDelta delta, AnimationThreadContext threadContext);
        RootMotionDelta ApplyCPUBudgetCorrection(RootMotionDelta delta, FrameBudgetSettings budget);
        RootMotionDelta ApplyPhysicsCorrection(RootMotionDelta delta, PhysicsState physicsState);
        RootMotionDelta ApplyBlendingCorrection(RootMotionDelta delta, AnimationBlendWeights weights);
        RootMotionDelta ApplyMotionMatchingCorrection(RootMotionDelta delta, MotionMatchingQuery query);

        // Extraction et gestion avancée du Root Motion
        void ApplyRootMotion(AnimationClipInstance instance, ref Vector3 position, ref Vector3 rotation);
        RootMotionDelta BlendRootMotion(List<RootMotionDelta> deltas);
        RootMotionDelta OverrideRootMotion(AnimationClipInstance instance, RootMotionDelta overrideDelta);
        RootMotionDelta CorrectRootMotion(AnimationClipInstance instance, RootMotionDelta rawDelta);
        bool ValidateRootMotion(RootMotionDelta rootMotion);
        RootMotionDelta GetRootMotionDelta(AnimationClipInstance instance);
        Vector3 GetRootMotionVelocity(AnimationClipInstance instance);
        Vector3 GetRootMotionAcceleration(AnimationClipInstance instance);
        Vector3 GetRootMotionDirection(AnimationClipInstance instance);
        float GetRootMotionFriction(AnimationClipInstance instance);
        float GetRootMotionSlope(AnimationClipInstance instance);
        SurfaceType GetRootMotionSurfaceType(AnimationClipInstance instance);
        CollisionData GetRootMotionCollisionStatus(AnimationClipInstance instance);
        RootMotionCorrectionData GetRootMotionCorrectionData(AnimationClipInstance instance);
        AnimationProfilerData GetRootMotionProfilerData(AnimationClipInstance instance);
        AnimationDebugData GetRootMotionDebugData(AnimationClipInstance instance);
        AnimationCacheStatus GetRootMotionCacheStatus(AnimationClipInstance instance);
        int GetRootMotionThreadAffinity(AnimationClipInstance instance);
        float GetRootMotionBudgetUsage(AnimationClipInstance instance);

        // Extraction et gestion avancée du Root Motion (async)
        /// <summary>
        /// Extrait le Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <param name="deltaTime">Temps delta</param>
        /// <returns>Tâche d'extraction</returns>
        /// <remarks>
        /// Extrait le Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<RootMotionDelta> ExtractRootMotionAsync(AnimationClipInstance instance, float deltaTime);
        
        /// <summary>
        /// Applique le Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <param name="position">Position</param>
        /// <param name="rotation">Rotation</param>
        /// <returns>Tâche d'application</returns>
        /// <remarks>
        /// Applique le Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task ApplyRootMotionAsync(AnimationClipInstance instance, ref Vector3 position, ref Vector3 rotation);
        
        /// <summary>
        /// Fusionne plusieurs Root Motions en mode asynchrone
        /// </summary>
        /// <param name="deltas">Liste des deltas</param>
        /// <returns>Tâche de fusion</returns>
        /// <remarks>
        /// Fusionne plusieurs Root Motions sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<RootMotionDelta> BlendRootMotionAsync(List<RootMotionDelta> deltas);
        
        /// <summary>
        /// Remplace le Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <param name="overrideDelta">Delta de remplacement</param>
        /// <returns>Tâche de remplacement</returns>
        /// <remarks>
        /// Remplace le Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<RootMotionDelta> OverrideRootMotionAsync(AnimationClipInstance instance, RootMotionDelta overrideDelta);
        
        /// <summary>
        /// Corrige le Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <param name="rawDelta">Delta brut</param>
        /// <returns>Tâche de correction</returns>
        /// <remarks>
        /// Corrige le Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<RootMotionDelta> CorrectRootMotionAsync(AnimationClipInstance instance, RootMotionDelta rawDelta);
        
        /// <summary>
        /// Valide le Root Motion en mode asynchrone
        /// </summary>
        /// <param name="rootMotion">Root Motion à valider</param>
        /// <returns>Tâche de validation</returns>
        /// <remarks>
        /// Valide le Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<bool> ValidateRootMotionAsync(RootMotionDelta rootMotion);
        
        /// <summary>
        /// Obtient le delta de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère le delta de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<RootMotionDelta> GetRootMotionDeltaAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient la vitesse de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère la vitesse de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<Vector3> GetRootMotionVelocityAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient l'accélération de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère l'accélération de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<Vector3> GetRootMotionAccelerationAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient la direction de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère la direction de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<Vector3> GetRootMotionDirectionAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient la friction de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère la friction de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<float> GetRootMotionFrictionAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient la pente de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère la pente de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<float> GetRootMotionSlopeAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient le type de surface de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère le type de surface de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<SurfaceType> GetRootMotionSurfaceTypeAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient l'état de collision de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère l'état de collision de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<CollisionData> GetRootMotionCollisionStatusAsync(AnimationClipInstance instance);
        
        /// <summary>
        /// Obtient les données de correction de Root Motion en mode asynchrone
        /// </summary>
        /// <param name="instance">Instance d'animation</param>
        /// <returns>Tâche de récupération</returns>
        /// <remarks>
        /// Récupère les données de correction de Root Motion sans bloquer le thread principal.
        /// </remarks>
        [Async]
        Task<RootMotionCorrectionData> GetRootMotionCorrectionDataAsync(AnimationClipInstance instance);

        // Sub-stepping
        List<RootMotionDelta> ExtractSubStepRootMotion(AnimationClipInstance instance, float deltaTime, int subSteps);

        // 🦿 5. IK & PROCÉDURAL
        void SolveFootPlacement(IKFootPlacementRequest request);
        void SolveHandGrip(IKHandGripRequest request);
        void SolveLookAt(IKLookAtRequest request);
        void SolveAimOffset(IKAimOffsetRequest request);

        // Solutions IK étendues
        void SolveFingerCurl(IKFingerCurlRequest request);
        void SolveShoulderCorrection(IKShoulderCorrectionRequest request);
        void SolveHipAlignment(IKHipAlignmentRequest request);
        void SolveStairPlacement(IKStairPlacementRequest request);
        void SolveSlopedSurfacePlacement(IKSlopedSurfaceRequest request);
        void SolveInteractionPlacement(IKInteractionRequest request);
        void SolveCombatPlacement(IKCombatRequest request);
        void SolveWeaponAlignment(IKWeaponAlignmentRequest request);
        void SolveToolPlacement(IKToolPlacementRequest request);
        void SolveVehiclePlacement(IKVehiclePlacementRequest request);
        void SolveLadderPlacement(IKLadderPlacementRequest request);
        void SolveRopePlacement(IKRopePlacementRequest request);
        void SolveGrapplePlacement(IKGrapplePlacementRequest request);
        void SolveProceduralAnimation(IKProceduralRequest request);
        void SolveDynamicPose(IKDynamicPoseRequest request);

        // Gestion avancée de l'IK
        AnimationIKResult EvaluateIK(AnimationIKRequest request);
        void ApplyIK(AnimationClipInstance instance, AnimationIKSolution solution);
        AnimationIKSolution BlendIK(List<AnimationIKSolution> solutions);
        AnimationIKSolution OverrideIK(AnimationIKRequest request, AnimationIKSolution overrideSolution);
        bool ValidateIK(AnimationIKRequest request);
        Vector3 GetIKTargetPosition(AnimationIKRequest request);
        Quaternion GetIKTargetRotation(AnimationIKRequest request);
        AnimationIKSolverType GetIKSolverType(AnimationIKRequest request);
        AnimationIKSolverPerformanceData GetIKSolverPerformanceData(AnimationIKRequest request);
        AnimationIKSolverErrorReport GetIKSolverErrorReport(AnimationIKRequest request);
        int GetIKSolverThreadAffinity(AnimationIKRequest request);
        float GetIKSolverBudgetUsage(AnimationIKRequest request);
        AnimationCacheStatus GetIKSolverCacheStatus(AnimationIKRequest request);
        AnimationDebugData GetIKSolverDebugData(AnimationIKRequest request);
        AnimationProfilerData GetIKSolverProfilerData(AnimationIKRequest request);
        long GetIKSolverMemoryUsage(AnimationIKRequest request);
        AnimationRetargetingData GetIKSolverRetargetingData(AnimationIKRequest request);
        MotionMatchingDatabase GetIKSolverMotionMatchingData(AnimationIKRequest request);
        AnimationAudioSyncData GetIKSolverAudioSyncData(AnimationIKRequest request);
        AnimationRenderSyncData GetIKSolverRenderSyncData(AnimationIKRequest request);

        // Résolution par priorité
        void SolveIKWithPriority(List<IKRequest> requests, IKPriorityOrder order);

        // 🧩 6. STATE MACHINES & GRAPHS
        AnimationState CreateState(string name, AnimationClip clip);
        AnimationGraph CreateGraph(string name);
        void AddStateToGraph(AnimationGraph graph, AnimationState state);
        void SetTransition(AnimationGraph graph, AnimationState from, AnimationState to, AnimationTransition transition);
        void EvaluateStateMachine(AnimationGraph graph, AnimationStateContext context);

        // Machines d'états spécialisées
        AnimationGraph CreateLocomotionStateMachine(LocomotionSettings settings);
        AnimationGraph CreateCombatStateMachine(CombatSettings settings);
        AnimationGraph CreateStealthStateMachine(StealthSettings settings);
        AnimationGraph CreateSwimmingStateMachine(SwimmingSettings settings);
        AnimationGraph CreateClimbingStateMachine(ClimbingSettings settings);
        AnimationGraph CreateVehicleStateMachine(VehicleSettings settings);
        AnimationGraph CreateEmotionalStateMachine(EmotionalSettings settings);
        AnimationGraph CreateInjuryStateMachine(InjurySettings settings);
        AnimationGraph CreateDeathStateMachine(DeathSettings settings);
        AnimationGraph CreateRecoveryStateMachine(RecoverySettings settings);
        AnimationGraph CreateChargeStateMachine(ChargeSettings settings);
        AnimationGraph CreateFireStateMachine(FireSettings settings);
        AnimationGraph CreateReloadStateMachine(ReloadSettings settings);
        AnimationGraph CreateParryStateMachine(ParrySettings settings);
        AnimationGraph CreateDodgeStateMachine(DodgeSettings settings);
        AnimationGraph CreateSlideStateMachine(SlideSettings settings);
        AnimationGraph CreateDashStateMachine(DashSettings settings);
        AnimationGraph CreateFallStateMachine(FallSettings settings);
        AnimationGraph CreateLandStateMachine(LandSettings settings);
        AnimationGraph CreateJumpStateMachine(JumpSettings settings);
        AnimationGraph CreateDoubleJumpStateMachine(DoubleJumpSettings settings);
        AnimationGraph CreateWallRunStateMachine(WallRunSettings settings);
        AnimationGraph CreateWallSlideStateMachine(WallSlideSettings settings);
        AnimationGraph CreateWallJumpStateMachine(WallJumpSettings settings);
        AnimationGraph CreateCameraStateMachine(CameraSettings settings);

        // Gestion avancée des State Machines
        AnimationGraph CreateStateMachine(string name);
        void AddState(AnimationGraph graph, AnimationState state);
        void RemoveState(AnimationGraph graph, AnimationState state);
        void AddTransition(AnimationGraph graph, AnimationState from, AnimationState to, AnimationTransition transition);
        void RemoveTransition(AnimationGraph graph, AnimationState from, AnimationState to);
        AnimationState GetCurrentState(AnimationGraph graph);
        AnimationState GetPreviousState(AnimationGraph graph);
        AnimationState GetNextState(AnimationGraph graph);
        AnimationTransition GetCurrentTransition(AnimationGraph graph);
        AnimationTransition GetPendingTransition(AnimationGraph graph);
        AnimationGraphStateHistory GetStateHistory(AnimationGraph graph);
        AnimationGraphTransitionHistory GetTransitionHistory(AnimationGraph graph);
        AnimationGraphPerformanceData GetStateMachinePerformanceData(AnimationGraph graph);
        AnimationGraphErrorReport GetStateMachineErrorReport(AnimationGraph graph);
        AnimationGraphDebugData GetStateMachineDebugData(AnimationGraph graph);
        AnimationGraphProfilerData GetStateMachineProfilerData(AnimationGraph graph);
        AnimationGraphCacheStatus GetStateMachineCacheStatus(AnimationGraph graph);
        int GetStateMachineThreadAffinity(AnimationGraph graph);
        float GetStateMachineBudgetUsage(AnimationGraph graph);
        List<AnimationEvent> GetStateMachineEventData(AnimationGraph graph);
        AnimationGraphMemoryUsage GetStateMachineMemoryUsage(AnimationGraph graph);
        AnimationGraphLODSettings GetStateMachineLODSettings(AnimationGraph graph);
        AnimationGraphRetargetingData GetStateMachineRetargetingData(AnimationGraph graph);
        MotionMatchingDatabase GetStateMachineMotionMatchingData(AnimationGraph graph);
        AnimationIKSettings GetStateMachineIKData(AnimationGraph graph);
        AnimationAudioSyncData GetStateMachineAudioSyncData(AnimationGraph graph);
        AnimationRenderSyncData GetStateMachineRenderSyncData(AnimationGraph graph);
        AnimationPhysicsSyncData GetStateMachinePhysicsSyncData(AnimationGraph graph);
        AnimationAIData GetStateMachineAIData(AnimationGraph graph);

        // 🎭 7. LAYERS & CURVES
        AnimationLayer CreateLayer(string name, AnimationLayerType type);
        void AddLayer(AnimationClipInstance instance, AnimationLayer layer);
        void SetLayerWeight(AnimationLayer layer, float weight);
        void BlendLayers(AnimationClipInstance instance, List<AnimationLayer> layers);

        // Couches spécialisées
        AnimationLayer CreateLocomotionLayer(LocomotionLayerSettings settings);
        AnimationLayer CreateCombatLayer(CombatLayerSettings settings);
        AnimationLayer CreateTorsoLayer(TorsoLayerSettings settings);
        AnimationLayer CreateHeadLayer(HeadLayerSettings settings);

        // Courbes globales
        float EvaluateCurve(AnimationCurve curve, float time);
        void RegisterCurveParameter(string name, float value);
        float GetCurveParameter(string name);

        // 🔧 8. SÉCURITÉ & ROBUSTESSE
        bool ValidateSkeleton(AnimationSkeleton skeleton);
        bool ValidateBlendSpace(BlendSpace2D blendSpace);
        bool ValidateTransition(AnimationTransition transition);
        bool ValidateRootMotion(RootMotionDelta rootMotion);
        bool ValidateIKSolution(IKRequest request);
        bool ValidateCurve(AnimationCurve curve);
        bool ValidateLOD(AnimationLODSettings settings);
        bool ValidateJob(AnimationJob job);
        void SanitizeNaNValues(ref Vector3 vector);
        void SanitizeNaNValues(ref Quaternion quaternion);
        void ClampValues(ref float value, float min, float max);

        // 🧠 9. IA & COMPORTEMENT
        void SynchronizeWithAI(AIState aiState, AnimationClipInstance instance);
        void AdaptToEmotionalState(EmotionalState emotionalState, AnimationClipInstance instance);
        void ApplyBehaviorTag(BehaviorTag tag, AnimationClipInstance instance);
        void SynchronizeCrowdAnimations(List<AnimationClipInstance> crowd);
        void BlendGroupFormations(List<AnimationClipInstance> group);

        // 🔊 10. AUDIO & FEEDBACK
        void SynchronizeFootstepsWithAnimation(AnimationClipInstance instance, AudioEngine audioEngine);
        void SynchronizeEffortSoundsWithAnimation(AnimationClipInstance instance, AudioEngine audioEngine);
        void SynchronizeImpactSoundsWithAnimation(AnimationClipInstance instance, AudioEngine audioEngine);
        void SynchronizeBreathingSoundsWithAnimation(AnimationClipInstance instance, AudioEngine audioEngine);
        void SynchronizeSurfaceSoundsWithAnimation(AnimationClipInstance instance, AudioEngine audioEngine);

        // 🔩 11. EXTENSIBILITÉ
        void RegisterExtension<T>() where T : IAnimationExtension;
        T GetExtension<T>() where T : IAnimationExtension;
    }

    #region ⚙️ 1. Structures d'Architecture

    public enum AnimationSubsystemType
    {
        IK, RootMotion, Blendspaces, StateMachine, Layers, Curves, MotionMatching, Retargeting, LOD, Debug
    }

    public interface IAnimationSubsystem
    {
        void Initialize();
        void Update(float deltaTime);
        void Shutdown();
        AnimationSubsystemType GetType();
    }

    public struct AnimationSubsystemStatus
    {
        public AnimationSubsystemType Type;
        public bool IsInitialized;
        public bool IsRunning;
        public bool HasErrors;
        public DateTime LastUpdate;
    }

    public struct AnimationSubsystemDependency
    {
        public AnimationSubsystemType Dependent;
        public AnimationSubsystemType DependsOn;
        public DependencyType Type;
    }

    public enum DependencyType
    {
        Required, Optional, ConflictsWith
    }

    public struct AnimationSubsystemPerformanceData
    {
        public AnimationSubsystemType Type;
        public float CPUTimeMs;
        public float MemoryUsageKB;
        public int FrameUpdates;
        public int ActiveInstances;
    }

    public struct AnimationSubsystemErrorReport
    {
        public AnimationSubsystemType Type;
        public List<AnimationErrorEntry> Errors;
        public int ErrorCount;
        public DateTime ReportTime;
    }

    public class AnimationEngineContext
    {
        public AnimationProfiler Profiler { get; set; }
        public AnimationJobSystem JobSystem { get; set; }
        public IRenderEngine RenderEngine { get; set; }
        public AnimationEventBus EventBus { get; set; }
        public AnimationResourceManager ResourceManager { get; set; }
        public AnimationErrorLogger ErrorLogger { get; set; }
        public AnimationCacheManager CacheManager { get; set; }
        public AnimationScheduler Scheduler { get; set; }
    }

    public class AnimationScheduler
    {
        public Dictionary<AnimationPriority, Queue<AnimationUpdateTask>> TaskQueues { get; set; }
        public int MaxTasksPerFrame { get; set; }
        public float TimeBudgetPerFrame { get; set; }
        public bool IsPaused { get; set; }
    }

    public enum AnimationPriority
    {
        Critical, High, Medium, Low, Background
    }

    public class AnimationUpdateTask
    {
        public Func<Task> WorkFunction { get; set; }
        public AnimationPriority Priority { get; set; }
        public DateTime ScheduledTime { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class FrameBudgetAllocator
    {
        public FrameBudgetSettings Settings { get; set; }
        public float UsedTime { get; set; }
        public float RemainingTime { get; set; }
        public bool IsOverBudget { get; set; }
    }

    public class FrameBudgetSettings
    {
        public float MaxAnimationTimeMs { get; set; }
        public float MaxIKTimeMs { get; set; }
        public float MaxBlendingTimeMs { get; set; }
        public float MaxSkinningTimeMs { get; set; }
    }

    public class AnimationJobSystem
    {
        public List<AnimationThreadContext> WorkerThreads { get; set; }
        public Queue<AnimationJob> JobQueue { get; set; }
        public int MaxConcurrentJobs { get; set; }
        public AnimationJobHandle ScheduleJob(AnimationJob job);
        public void WaitAll(params AnimationJobHandle[] handles);
    }

    public class AnimationJobHandle
    {
        public int Id { get; set; }
        public bool IsComplete { get; set; }
        public float ExecutionTime { get; set; }
    }

    public class AnimationCacheManager
    {
        public Dictionary<string, object> Cache { get; set; }
        public TimeSpan ExpirationTime { get; set; }
        public int MaxCacheSize { get; set; }
        public void ClearExpiredEntries();
        public void ClearAll();
    }

    public class AnimationEventBus
    {
        public Dictionary<string, List<Action<AnimationEventArgs>>> EventListeners { get; set; }
        public void PublishEvent(string eventName, AnimationEventArgs args);
        public void Subscribe(string eventName, Action<AnimationEventArgs> callback);
        public void Unsubscribe(string eventName, Action<AnimationEventArgs> callback);
    }

    public class AnimationEventArgs : EventArgs
    {
        public string EventName { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AnimationResourceManager
    {
        public Dictionary<string, WeakReference> LoadedResources { get; set; }
        public int MaxResourceCount { get; set; }
        public long MaxMemoryUsage { get; set; }
        public void GarbageCollectUnused();
    }

    public interface IAnimationResourceLoader
    {
        Task<object> LoadResourceAsync(string path);
        void UnloadResource(object resource);
        bool CanHandle(string extension);
    }

    public class AnimationProfiler
    {
        public Dictionary<string, AnimationProfilerData> ProfileData { get; set; }
        public bool IsProfiling { get; set; }
        public string CurrentSession { get; set; }
        public void BeginSample(string name);
        public void EndSample(string name);
        public AnimationProfilerData GetSampleData(string name);
    }

    public class AnimationDebugOverlay
    {
        public Dictionary<DebugVisualizationType, bool> Visualizations { get; set; }
        public Color OverlayColor { get; set; }
        public float OverlayOpacity { get; set; }
        public bool ShowPerformanceMetrics { get; set; }
    }

    public enum DebugVisualizationType
    {
        Skeleton, IKTargets, RootMotion, BlendWeights, Curves, Transitions, Layers, States, Events, Performance
    }

    public class AnimationErrorLogger
    {
        public List<AnimationErrorEntry> ErrorLog { get; set; }
        public int MaxLogEntries { get; set; }
        public AnimationErrorLevel LogLevel { get; set; }
    }

    public enum AnimationErrorLevel
    {
        Info, Warning, Error, Critical
    }

    public class AnimationErrorEntry
    {
        public DateTime Timestamp { get; set; }
        public AnimationErrorLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public string StackTrace { get; set; }
    }

    public class AnimationStateRegistry
    {
        public Dictionary<int, AnimationState> RegisteredStates { get; set; }
        public int NextId { get; set; }
    }

    public class AnimationStateReference
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class AnimationGraphCompiler
    {
        public Dictionary<string, CompiledAnimationGraph> CompiledGraphs { get; set; }
        public CompiledAnimationGraph Compile(AnimationGraph graph);
        public void Optimize(CompiledAnimationGraph compiledGraph);
    }

    public class CompiledAnimationGraph
    {
        public string Name { get; set; }
        public byte[] Bytecode { get; set; }
        public Dictionary<string, int> NodeOffsets { get; set; }
        public List<CompiledTransition> Transitions { get; set; }
    }

    public class CompiledTransition
    {
        public int FromNode { get; set; }
        public int ToNode { get; set; }
        public float Duration { get; set; }
        public List<CompiledCondition> Conditions { get; set; }
    }

    public class CompiledCondition
    {
        public string ParameterName { get; set; }
        public float Threshold { get; set; }
        public ComparisonOperator Operator { get; set; }
    }

    public class AnimationDataCompressor
    {
        public byte[] Compress(AnimationClip clip, CompressionSettings settings);
        public AnimationClip Decompress(byte[] data, CompressionSettings settings);
    }

    public class CompressionSettings
    {
        public CompressionMethod Method { get; set; }
        public float Quality { get; set; }
        public bool PreserveKeyframes { get; set; }
        public bool ReducePrecision { get; set; }
    }

    public enum CompressionMethod
    {
        KeyframeReduction, Quantization, VectorQuantization, NeuralNetwork
    }

    public class AnimationStreamingSystem
    {
        public Dictionary<string, StreamHandle> ActiveStreams { get; set; }
        public int MaxConcurrentStreams { get; set; }
        public int StreamBufferSize { get; set; }
    }

    public class StreamHandle
    {
        public string Path { get; set; }
        public StreamStatus Status { get; set; }
        public float Progress { get; set; }
        public AnimationClip Result { get; set; }
    }

    public enum StreamStatus
    {
        Queued, Loading, Complete, Failed, Cancelled
    }

    public class AnimationRetargetingManager
    {
        public Dictionary<string, AnimationRetargetingData> RetargetingPresets { get; set; }
        public AnimationClip Retarget(AnimationClip source, AnimationRetargetingData config);
    }

    public class AnimationMotionMatchingSystem
    {
        public Dictionary<string, MotionMatchingDatabase> Databases { get; set; }
        public AnimationClipInstance FindBestMatch(MotionMatchingQuery query);
        public void UpdateDatabase(string dbName, List<PoseSnapshot> newSamples);
    }

    public class AnimationPoseCorrectionSystem
    {
        public void ApplyCorrection(AnimationClipInstance instance, AnimationPoseCorrectionData correction);
        public bool ValidatePose(AnimationClipInstance instance);
    }

    public class AnimationLODManager
    {
        public Dictionary<string, AnimationLODSettings> LODPresets { get; set; }
        public void UpdateLODForInstance(AnimationClipInstance instance, Vector3 viewerPosition);
    }

    public class AnimationCurveManager
    {
        public Dictionary<string, AnimationCurve> GlobalCurves { get; set; }
        public void RegisterCurve(string name, AnimationCurve curve);
        public AnimationCurve GetCurve(string name);
    }

    public class AnimationLayerManager
    {
        public Dictionary<string, AnimationLayer> RegisteredLayers { get; set; }
        public AnimationLayer CreateLayer(string name, AnimationLayerType type);
    }

    public class AnimationSyncManager
    {
        public Dictionary<string, AnimationSyncGroup> SyncGroups { get; set; }
        public void SynchronizeGroup(AnimationSyncGroup group);
    }

    public class AnimationGPUHook
    {
        public List<Action<GPUSkinningEventArgs>> GPUSkinningCallbacks { get; set; }
        public void OnGPUSkinningBegin(GPUSkinningEventArgs args);
        public void OnGPUSkinningEnd(GPUSkinningEventArgs args);
    }

    public class GPUSkinningEventArgs : EventArgs
    {
        public SkinnedMesh Mesh { get; set; }
        public List<Matrix4x4> BoneMatrices { get; set; }
        public float ProcessingTime { get; set; }
        public bool Success { get; set; }
    }

    #endregion

    #region ⚙️ 1. Nouvelles Structures d'Architecture

    public struct SubsystemIntegrityReport
    {
        public bool IsIntact { get; set; }
        public List<string> Issues { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public struct DependencyGraph
    {
        public Dictionary<AnimationSubsystemType, List<AnimationSubsystemType>> Dependencies { get; set; }
        public List<AnimationSubsystemType> LoadOrder { get; set; }
    }

    public struct ThreadAffinityMap
    {
        public Dictionary<AnimationSubsystemType, int> ThreadAssignments { get; set; }
    }

    public struct ThreadLoadReport
    {
        public Dictionary<int, float> LoadPercentages { get; set; }
        public Dictionary<int, int> ActiveJobs { get; set; }
    }

    public struct CacheUsageReport
    {
        public Dictionary<AnimationSubsystemType, float> CacheUsagePercent { get; set; }
        public long TotalCacheSize { get; set; }
        public long UsedCacheSize { get; set; }
    }

    public struct AnimationSubsystemTelemetryData
    {
        public AnimationSubsystemType Type { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public struct VersionInfo
    {
        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public DateTime BuildDate { get; set; }
    }

    public struct BuildMetadata
    {
        public string Platform { get; set; }
        public string Configuration { get; set; }
        public string CompilerVersion { get; set; }
        public List<string> Features { get; set; }
    }

    public struct ErrorSeverityReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public Dictionary<AnimationErrorLevel, int> ErrorCounts { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct RecoveryStatusReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public bool IsRecovered { get; set; }
        public DateTime LastRecoveryTime { get; set; }
        public int RecoveryAttempts { get; set; }
    }

    public struct ThreadSafetyReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public bool IsThreadSafe { get; set; }
        public List<string> UnsafeOperations { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct LockStatusReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public Dictionary<string, bool> LockStatuses { get; set; }
        public List<string> ActiveLocks { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct PerformanceTrendReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public List<PerformanceSample> Samples { get; set; }
        public TrendDirection Direction { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct PerformanceSample
    {
        public float CPUTimeMs { get; set; }
        public float MemoryUsageKB { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum TrendDirection
    {
        Improving, Declining, Stable
    }

    public struct CPUHeatmapReport
    {
        public Dictionary<AnimationSubsystemType, float> CPULoad { get; set; }
        public float MaxLoad { get; set; }
        public float MinLoad { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct TimelineReport
    {
        public List<TimelineEvent> Events { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public struct TimelineEvent
    {
        public string Name { get; set; }
        public AnimationSubsystemType SubsystemType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public float DurationMs { get; set; }
    }

    public struct EventHistoryReport
    {
        public List<HistoricalEvent> Events { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public struct HistoricalEvent
    {
        public string Name { get; set; }
        public AnimationSubsystemType SubsystemType { get; set; }
        public DateTime Timestamp { get; set; }
        public object Data { get; set; }
    }

    public struct DependencyConflictReport
    {
        public List<DependencyConflict> Conflicts { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct DependencyConflict
    {
        public AnimationSubsystemType SubsystemA { get; set; }
        public AnimationSubsystemType SubsystemB { get; set; }
        public ConflictType Type { get; set; }
        public string Description { get; set; }
    }

    public enum ConflictType
    {
        CircularDependency, VersionMismatch, ResourceConflict
    }

    public struct ResourceUsageReport
    {
        public Dictionary<string, long> FileSizes { get; set; }
        public Dictionary<string, DateTime> LastAccessTimes { get; set; }
        public long TotalResourceUsage { get; set; }
    }

    public struct StreamingStatusReport
    {
        public Dictionary<string, StreamStatus> FileStatuses { get; set; }
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int FailedFiles { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct JobLatencyReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public float AverageLatencyMs { get; set; }
        public float MaxLatencyMs { get; set; }
        public float MinLatencyMs { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct JobThroughputReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public int JobsPerSecond { get; set; }
        public int TotalJobsProcessed { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct JobFailureReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public float FailureRate { get; set; }
        public int TotalFailures { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct JobRetryReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public int AverageRetries { get; set; }
        public int MaxRetries { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct JobPriorityReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public Dictionary<AnimationPriority, int> PriorityCounts { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct JobExecutionTimeReport
    {
        public AnimationSubsystemType SubsystemType { get; set; }
        public float AverageExecutionTimeMs { get; set; }
        public float MaxExecutionTimeMs { get; set; }
        public float MinExecutionTimeMs { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct JobDependencyGraph
    {
        public Dictionary<int, List<int>> Dependencies { get; set; }
        public List<int> ExecutionOrder { get; set; }
    }

    #endregion

    #region 🧩 2. Nouvelles Structures de Gestion des Squelettes & Clips

    public struct StreamingError
    {
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public struct AnimationClipStreamingProfilerData
    {
        public string ClipName { get; set; }
        public float LoadTimeMs { get; set; }
        public float DecompressTimeMs { get; set; }
        public float CacheTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public struct AnimationClipStreamingTelemetryData
    {
        public string ClipName { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion

    #region Interfaces d'extensions

    public interface IAnimationExtension
    {
        void Initialize();
        void Update(float deltaTime);
        void Shutdown();
    }

    #endregion

    #region Attributs

    /// <summary>
    /// Indique que la méthode est thread-safe
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ThreadSafeAttribute : Attribute
    {
    }

    /// <summary>
    /// Indique que la méthode est asynchrone
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AsyncAttribute : Attribute
    {
    }

    /// <summary>
    /// Indique que la méthode est critique pour les performances
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CriticalPathAttribute : Attribute
    {
    }

    #endregion
}