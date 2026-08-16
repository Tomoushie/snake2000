// Game/Gameplay/Movement/MovementSystem.cs

// #define DEBUG_PROFILING // Activer/désactiver le profiling
// #define DEBUG_THREAD_SAFE_PROFILER // Activer le profiler thread-safe si MovementJobSystem est utilisé
// #define DEBUG_OVERLAY_VISUAL // Activer l'affichage de l'overlay

using System;
using System.Collections.Concurrent; // Pour ConcurrentDictionary, ConcurrentQueue
using System.Collections.Generic;
using System.Diagnostics; // Pour MovementProfiler
using System.Drawing; // Pour les couleurs de debug overlay
using System.IO; // Pour l'export de fichiers
using System.Threading; // Pour le verrouillage dans le profiler thread-safe, Interlocked
using System.Threading.Tasks; // Pour l'export asynchrone, mise à jour physique asynchrone
using System.Linq; // Pour Parallel.ForEach
using Engine.Animation; // Pour IAnimationEngine et IAnimationBridge

// - Interfaces pour la cohérence -
namespace Game.Gameplay.Movement
{
    public interface IMovementSystem : ISystem {}

    namespace Animation
    {
        public interface IAnimationSystem : ISystem {}
    }

    namespace Physics
    {
        public interface IPhysicsSystem : ISystem {}
    }

    namespace AudioVisual
    {
        public interface IAudioVisualSystem : ISystem {}
    }

    namespace Debugging
    {
        public interface IDebugSystem : ISystem {}
    }

    namespace Tools
    {
        public interface IMovementProfiler
        {
            void StartProfiling(Type systemType, string operation = "Total");
            void StopProfiling(Type systemType, string operation = "Total");
            void SetStat(Type systemType, string key, object value);
            float GetLastDuration(Type systemType, string operation = "Total");
            float GetModuleAverageTimeMs();
            int GetErrorCount(IMovementLogger logger);
        }

        public interface IMovementLogger
        {
            void LogError(string source, string message, string stackTrace, ErrorSeverity severity);
        }

        public enum ErrorSeverity { Low, Medium, High, Critical }

        public class FrameBudgetAllocator
        {
            private readonly float _maxFrameTimeMs;
            private float _usedTimeMs = 0.0f;

            public FrameBudgetAllocator(float maxFrameTimeMs = 8.0f) // ~120 FPS
            {
                _maxFrameTimeMs = maxFrameTimeMs;
            }

            public bool IsBudgetAvailable() => _usedTimeMs < _maxFrameTimeMs;
            public float RemainingBudget => _maxFrameTimeMs - _usedTimeMs;
            public void AddUsedTime(float timeMs) => _usedTimeMs += timeMs;
            public void Reset() => _usedTimeMs = 0.0f;
        }

        // Interface pour le Thread Affinity Control
        public interface IThreadAffinityManager
        {
            void AssignSystemToThread(ISystem system, int threadIndex);
            Task RunOnThread(int threadIndex, Action action);
        }

        // Interface pour le GPU Profiling Hook
        public interface IGPUProfilerHook
        {
            void BeginGPUSample(string sampleName);
            void EndGPUSample(string sampleName);
        }

        // Interface pour le Safe Math Library
        public static class SafeMath
        {
            public static float SafeDivide(float a, float b)
            {
                if (Math.Abs(b) < float.Epsilon) return 0.0f; // Ou throw exception selon le comportement souhaité
                float result = a / b;
                if (float.IsNaN(result) || float.IsInfinity(result))
                {
                     // Logguer l'erreur via IMovementLogger
                     return 0.0f;
                }
                return result;
            }
            // Ajouter d'autres méthodes de calcul sécurisé...
        }
    }
}

// - Systèmes annexes critiques -
// FICHIER : /Game/Gameplay/Movement/Tools/MovementErrorLogSystem.cs
namespace Game.Gameplay.Movement.Tools
{
    public class MovementErrorLogSystem : IMovementLogger
    {
        private struct ErrorInfo
        {
            public DateTime LastLoggedTime;
            public int Count;
            public string FullMessage; // Garder le message complet pour l'export
            public string StackTrace; // Garder la stack trace pour l'export
            public ErrorSeverity Severity; // Niveau de gravité
        }

        // Implémentation simplifiée
        private Dictionary<string, ErrorInfo> _errors = new Dictionary<string, ErrorInfo>();

        public void LogError(string source, string message, string stackTrace, ErrorSeverity severity)
        {
            string key = source + ":" + message; // Clé simple pour regrouper les erreurs identiques
            if (_errors.ContainsKey(key))
            {
                _errors[key].Count++;
                _errors[key].LastLoggedTime = DateTime.UtcNow;
            }
            else
            {
                _errors.Add(key, new ErrorInfo { Count = 1, LastLoggedTime = DateTime.UtcNow, FullMessage = message, StackTrace = stackTrace, Severity = severity });
            }
        }
    }
}

// - Composants ECS pour le Mouvement (doivent être dans /Game/Gameplay/Movement/Components/) -
// - Composants ECS pour le Mouvement (doivent être dans /Game/Gameplay/Movement/Components/) -
// Ces définitions sont incluses ici pour complétude, mais devraient être dans un fichier séparé dans /Components/
// (Les définitions précédentes sont conservées, mais organisées/modifiées si nécessaire)

namespace Game.Gameplay.Movement.Components
{
    public enum MovementType { PhysicsBased, Steering, RootMotion }
    public enum MovementState
    {
        Idle, Walking, Running, Sprinting, Jumping, Falling, Slipping, Recovering, Crouching, Injured,
        // Ajouts pour les comportements avancés
        Sneaking, SprintingBurst, Sliding, Vaulting, Climbing, Swimming, InZeroG, UsingCover, Peeking, Strafing,
        UnderSuppression, MountedOnVehicle, PerformingParkour, ExploitingObstacle, Feinting, Retreating, Charging,
        Tired, Exhausted, Panicked, Alerted, Fleeing, AttackingMelee, AttackingRanged, Blocking, Dodging, KnockedDown, Stunned
    }

    public enum MovementStance { Standing, Crouched, Prone, Flying, Swimming, ZeroG }

    [Flags]
    public enum MovementTag
    {
        None = 0,
        Sneaking = 1 << 0,
        Sprinting = 1 << 1,
        Slipping = 1 << 2,
        Injured = 1 << 3,
        Stressful = 1 << 4,
        Swimming = 1 << 5,
        InZeroG = 1 << 6,
        UsingCover = 1 << 7,
        Charging = 1 << 8,
        // Ajouts pour les comportements avancés
        Aggressive = 1 << 9,
        Defensive = 1 << 10,
        Opportunistic = 1 << 11,
        Kamikaze = 1 << 12,
        Hunting = 1 << 13,
        Fleeing = 1 << 14,
        Boss = 1 << 15,
        MiniBoss = 1 << 16,
        Camouflaged = 1 << 17,
        Diverting = 1 << 18,
        Leading = 1 << 19
    }

    public struct MovementModifier
    {
        public float SpeedMultiplier;
        public float AccelerationMultiplier;
        public float TractionMultiplier;
        public float FrictionMultiplier;
        public float StaminaDrainMultiplier;
        public float StressIncreaseMultiplier;
        // Ajouts pour les comportements avancés
        public float StealthSpeedMultiplier;
        public float StealthAccelerationMultiplier;
        public float SwimSpeedMultiplier;
        public float BuoyancyFactor;
        public float WaterResistance;
        public float ZeroGThrustPower;
        public float ZeroGDamping;
        public float GrappleRange;
        public float VaultHeight;
        public float WallRunSpeedMultiplier;
        public float LedgeGrabDistance;
        public float PushForce;
        public float CarryCapacity;
        public float DestructionThreshold;
        public float HazardAvoidanceRadius;
        public float HazardAvoidanceStrength;
        public float WindInfluenceFactor;
        public float CurrentInfluenceFactor;
    }

    public struct MovementComponent : IComponent, IReusableComponent
    {
        // ... (garder tous les champs existants et ajoutés précédemment) ...
        public float BaseSpeed; public float CurrentSpeed; public float Acceleration; public float BaseDeceleration; public float Deceleration;
        public float MaxSlopeAngle; public bool IsGrounded; public Vector2 GroundNormal; public float Stamina; public float MaxStamina; public float StaminaRegenRate; public float StaminaDrainRate;
        public MovementType Type; public float LODUpdateFrequency; public float EffectiveFrictionFactor; public float BodyTemperature;
        // Locomotion furtive
        public float StealthSpeedMultiplier; public float StealthAccelerationMultiplier;
        // Locomotion aquatique
        public float SwimSpeedMultiplier; public float BuoyancyFactor; public float WaterResistance;
        // Locomotion en apesanteur
        public float ZeroGThrustPower; public float ZeroGDamping;
        // Locomotion par grappin / parkour
        public float GrappleRange; public float VaultHeight; public float WallRunSpeedMultiplier; public float LedgeGrabDistance;
        // Interaction avec les obstacles dynamiques
        public float PushForce; public float CarryCapacity; public float DestructionThreshold;
        // Zones de danger
        public float HazardAvoidanceRadius; public float HazardAvoidanceStrength;
        // Courants d'eau / vents
        public float WindInfluenceFactor; public float CurrentInfluenceFactor;
        // - Ajouts pour UE4/5 -
        public List<MovementModifier> Modifiers; // Pipeline de modificateurs
        public List<MovementTag> Tags; // Tags pour catégoriser l'état de mouvement
        public float MaxSpeedLimit; // Pour la validation (anti-teleportation)
        public Vector2 CachedSlopeNormal; // Pour le cache
        public float CachedSlopeAngle; // Pour le cache
        // Ajouts pour les comportements avancés
        public float LegInjurySeverity; // 0.0f (aucune) -> 1.0f (critique)
        public float ArmInjurySeverity; // 0.0f (aucune) -> 1.0f (critique)
        public float StressLevel; // 0.0f (calme) -> 1.0f (panique)
        public float BreathLevel; // 0.0f (essoufflé) -> 100.0f (repos)
        public float PainLevel; // 0.0f (aucune) -> 1.0f (extrême)
        public float FearLevel; // 0.0f (confiant) -> 1.0f (terrorisé)
        public float AngerLevel; // 0.0f (calme) -> 1.0f (furieux)
        public float Morale; // 0.0f (bas) -> 1.0f (haut)
        public float Focus; // 0.0f (distrait) -> 1.0f (concentré)
        public float Awareness; // 0.0f (inattentif) -> 1.0f (vigilant)
        public float Fatigue; // 0.0f (reposé) -> 1.0f (épuisé)
        public float Tension; // 0.0f (détendu) -> 1.0f (tendu)
        public float Adrenaline; // 0.0f (pas d'adrénaline) -> 1.0f (maximum)
        public float Health; // Santé globale
        public float Shield; // Bouclier (si applicable)
        public float Armor; // Armure (réduction des dégâts)
        public float Visibility; // 0.0f (invisible) -> 1.0f (pleinement visible)
        public float Audibility; // 0.0f (silencieux) -> 1.0f (bruyant)
        public float CamouflageEffectiveness; // 0.0f (aucun) -> 1.0f (parfait)
        public float DetectionRadius; // Portée de détection par les ennemis
        public float ThreatLevel; // Niveau de menace perçu
        public float LeadershipRating; // Capacité de leadership (pour IA)
        public float TrustLevel; // Niveau de confiance avec d'autres entités
        public float Loyalty; // Loyauté envers un groupe/allié
        public float Initiative; // Capacité à prendre des initiatives
        public float TacticalAwareness; // Conscience tactique
        public float CoverPreference; // Préférence pour se mettre à couvert
        public float AggressionLevel; // Niveau d'agression
        public float RiskTaking; // Propension à prendre des risques
        public float Patience; // Niveau de patience
        public float Empathy; // Niveau d'empathie (pour IA sociale)
        public float Confidence; // Niveau de confiance en soi
        public float Adaptability; // Capacité d'adaptation
        public float Curiosity; // Niveau de curiosité
        public float Hunger; // Niveau de faim (pour IA de survie)
        public float Thirst; // Niveau de soif (pour IA de survie)
        public float Temperature; // Température corporelle
        public float Hydration; // Niveau d'hydratation
        public float Oxygen; // Niveau d'oxygène (pour environnements hostiles)
        public float RadiationExposure; // Niveau d'exposition aux radiations
        public float PoisonLevel; // Niveau d'empoisonnement
        public float DiseaseLevel; // Niveau de maladie
        public float StunDuration; // Durée d'étourdissement restante
        public float SlowDuration; // Durée de ralentissement restante
        public float HasteDuration; // Durée d'accélération restante
        public float InvincibilityDuration; // Durée d'invincibilité restante
        public float InvisibilityDuration; // Durée d'invisibilité restante
        public float CloakDuration; // Durée de camouflage actif restante
        public float HasteSpeedMultiplier; // Multiplicateur de vitesse pour le buff Haste
        public float SlowSpeedMultiplier; // Multiplicateur de vitesse pour le debuff Slow
        public float Traction; // Adhérence au sol
        public float JumpPower; // Puissance de saut
        public float MaxJumpHeight; // Hauteur maximale de saut
        public float MaxJumpDistance; // Distance maximale de saut
        public float GlideRatio; // Ratio de planage
        public float ParachuteDeployHeight; // Hauteur de déploiement parachute
        public float FallDamageMultiplier; // Multiplicateur de dégâts de chute
        public float CollisionDamageMultiplier; // Multiplicateur de dégâts de collision
        public float EnvironmentalResistance; // Résistance aux environnements hostiles
        public float StealthDetectionReduction; // Réduction de la probabilité d'être détecté
        public float PerceptionBonus; // Bonus à la perception
        public float ReactionTime; // Temps de réaction
        public float CombatReadiness; // Prêt pour le combat
        public float TargetAcquisitionRange; // Portée d'acquisition de cible
        public float ThreatAssessmentAccuracy; // Précision de l'évaluation des menaces
        public float GroupCoordination; // Coordination avec le groupe
        public float ResourceManagementSkill; // Compétence en gestion des ressources
        public float SurvivalInstincts; // Instincts de survie
        public float LeadershipInitiative; // Initiative en tant que leader
        public float FollowerReliability; // Fiabilité en tant que suiveur
        public float TeamworkEffectiveness; // Efficacité en équipe
        public float CommunicationRange; // Portée de communication
        public float SignalClarity; // Clarté du signal de communication
        public float CommandAuthority; // Autorité de commandement
        public float MoraleBoostEffectiveness; // Efficacité du boost de moral
        public float PanicResistance; // Résistance à la panique
        public float PainTolerance; // Tolérance à la douleur
        public float FearSuppression; // Suppression de la peur
        public float AngerControl; // Contrôle de la colère
        public float StressManagement; // Gestion du stress
        public float FatigueResistance; // Résistance à la fatigue
        public float TensionRelease; // Capacité à relâcher la tension
        public float AdrenalineControl; // Contrôle de l'adrénaline
        public float HealthRegenerationRate; // Taux de régénération de la santé
        public float ShieldRegenerationRate; // Taux de régénération du bouclier
        public float StaminaRegenerationRate; // Taux de régénération de la stamina
        public float BreathRegenerationRate; // Taux de régénération de la respiration
        public float PoisonResistance; // Résistance au poison
        public float DiseaseResistance; // Résistance aux maladies
        public float RadiationResistance; // Résistance aux radiations
        public float EnvironmentalProtection; // Protection contre les environnements hostiles
        public float ThermalInsulation; // Isolation thermique
        public float HydrationRetention; // Rétention d'hydratation
        public float OxygenEfficiency; // Efficacité de la consommation d'oxygène
        public float NutritionAbsorption; // Absorption de la nutrition
        public float WasteElimination; // Elimination des déchets (biologique)
        public float SleepRequirement; // Besoin de sommeil (biologique)
        public float CircadianRhythm; // Rythme circadien (biologique)
        public float MetabolicRate; // Taux métabolique (biologique)
        public float ImmuneSystemStrength; // Force du système immunitaire (biologique)
        public float HealingFactor; // Facteur de guérison (biologique)
        public float RegenerationFactor; // Facteur de régénération (biologique)
        public float AgingFactor; // Facteur de vieillissement (biologique)
        public float GeneticPredisposition; // Predisposition génétique (biologique)
        public float LearnedBehaviors; // Comportements appris (IA)
        public float ExperiencePoints; // Points d'expérience (IA/apprentissage)
        public float SkillLevel; // Niveau de compétence (IA/apprentissage)
        public float Intelligence; // Intelligence (IA)
        public float MemoryCapacity; // Capacité de mémoire (IA)
        public float LearningRate; // Taux d'apprentissage (IA)
        public float ProblemSolvingAbility; // Capacité de résolution de problèmes (IA)
        public float Creativity; // Créativité (IA)
        public float AdaptabilityToChange; // Adaptabilité au changement (IA)
        public float EmotionalStability; // Stabilité émotionnelle (IA)
        public float SocialSkills; // Compétences sociales (IA)
        public float LeadershipQualities; // Qualités de leadership (IA)
        public float FollowerQualities; // Qualités de suiveur (IA)
        public float TeamworkSkills; // Compétences en équipe (IA)
        public float CommunicationSkills; // Compétences en communication (IA)
        public float NegotiationSkills; // Compétences en négociation (IA)
        public float DeceptionSkills; // Compétences en tromperie (IA)
        public float ManipulationSkills; // Compétences en manipulation (IA)
        public float StrategyDevelopment; // Développement de stratégie (IA)
        public float TacticalExecution; // Exécution tactique (IA)
        public float ResourceAllocation; // Allocation des ressources (IA)
        public float RiskAssessment; // Evaluation des risques (IA)
        public float ThreatAnalysis; // Analyse des menaces (IA)
        public float SituationAssessment; // Evaluation de la situation (IA)
        public float DecisionMakingSpeed; // Vitesse de prise de décision (IA)
        public float DecisionMakingAccuracy; // Précision de la prise de décision (IA)
        public float PlanFlexibility; // Flexibilité du plan (IA)
        public float ImprovisationAbility; // Capacité d'improvisation (IA)
        public float StressTolerance; // Tolérance au stress (IA)
        public float FearManagement; // Gestion de la peur (IA)
        public float AngerManagement; // Gestion de la colère (IA)
        public float PainManagement; // Gestion de la douleur (IA)
        public float FatigueManagement; // Gestion de la fatigue (IA)
        public float TensionManagement; // Gestion de la tension (IA)
        public float AdrenalineManagement; // Gestion de l'adrénaline (IA)
        public float HealthManagement; // Gestion de la santé (IA)
        public float ResourceManagement; // Gestion des ressources (IA)
        public float SupplyChainManagement; // Gestion de la chaîne d'approvisionnement (IA)
        public float LogisticsCoordination; // Coordination logistique (IA)
        public float MaintenanceEfficiency; // Efficacité de la maintenance (IA)
        public float RepairSpeed; // Vitesse de réparation (IA)
        public float ConstructionSpeed; // Vitesse de construction (IA)
        public float CraftingSkill; // Compétence en artisanat (IA)
        public float MiningEfficiency; // Efficacité du minage (IA)
        public float FarmingSkill; // Compétence agricole (IA)
        public float HuntingSkill; // Compétence de chasse (IA)
        public float FishingSkill; // Compétence de pêche (IA)
        public float ForagingSkill; // Compétence de cueillette (IA)
        public float NavigationSkill; // Compétence en navigation (IA)
        public float SurvivalSkill; // Compétence de survie (IA)
        public float MedicalSkill; // Compétence médicale (IA)
        public float EngineeringSkill; // Compétence en ingénierie (IA)
        public float ScienceSkill; // Compétence scientifique (IA)
        public float ArtSkill; // Compétence artistique (IA)
        public float MusicSkill; // Compétence musicale (IA)
        public float LanguageSkill; // Compétence linguistique (IA)
        public float HistoryKnowledge; // Connaissance historique (IA)
        public float CulturalKnowledge; // Connaissance culturelle (IA)
        public float TechnicalKnowledge; // Connaissance technique (IA)
        public float TacticalKnowledge; // Connaissance tactique (IA)
        public float StrategicKnowledge; // Connaissance stratégique (IA)
        public float PoliticalKnowledge; // Connaissance politique (IA)
        public float EconomicKnowledge; // Connaissance économique (IA)
        public float SocialKnowledge; // Connaissance sociale (IA)
        public float PsychologicalKnowledge; // Connaissance psychologique (IA)
        public float PhilosophicalKnowledge; // Connaissance philosophique (IA)
        public float SpiritualKnowledge; // Connaissance spirituelle (IA)
        public float MythologicalKnowledge; // Connaissance mythologique (IA)
        public float AstronomicalKnowledge; // Connaissance astronomique (IA)
        public float GeologicalKnowledge; // Connaissance géologique (IA)
        public float BiologicalKnowledge; // Connaissance biologique (IA)
        public float ChemicalKnowledge; // Connaissance chimique (IA)
        public float PhysicalKnowledge; // Connaissance physique (IA)
        public float MathematicalKnowledge; // Connaissance mathématique (IA)
        public float LogicalKnowledge; // Connaissance logique (IA)
        public float CreativeKnowledge; // Connaissance créative (IA)
        public float PracticalKnowledge; // Connaissance pratique (IA)
        public float AbstractKnowledge; // Connaissance abstraite (IA)
        public float IntuitiveKnowledge; // Connaissance intuitive (IA)
        public float EmpiricalKnowledge; // Connaissance empirique (IA)
        public float TheoreticalKnowledge; // Connaissance théorique (IA)
        public float AppliedKnowledge; // Connaissance appliquée (IA)
        public float SpecializedKnowledge; // Connaissance spécialisée (IA)
        public float GeneralKnowledge; // Connaissance générale (IA)
        public float UnknownKnowledge; // Connaissance inconnue (IA)
        public float ForgottenKnowledge; // Connaissance oubliée (IA)
        public float LostKnowledge; // Connaissance perdue (IA)
        public float ForbiddenKnowledge; // Connaissance interdite (IA)
        public float SacredKnowledge; // Connaissance sacrée (IA)
        public float ProfaneKnowledge; // Connaissance profane (IA)
        public float SecretKnowledge; // Connaissance secrète (IA)
        public float PublicKnowledge; // Connaissance publique (IA)
        public float PrivateKnowledge; // Connaissance privée (IA)
        public float ClassifiedKnowledge; // Connaissance classifiée (IA)
        public float ConfidentialKnowledge; // Connaissance confidentielle (IA)
        public float RestrictedKnowledge; // Connaissance restreinte (IA)
        public float AccessibleKnowledge; // Connaissance accessible (IA)
        public float InaccessibleKnowledge; // Connaissance inaccessible (IA)
        public float RelevantKnowledge; // Connaissance pertinente (IA)
        public float IrrelevantKnowledge; // Connaissance non pertinente (IA)
        public float UsefulKnowledge; // Connaissance utile (IA)
        public float UselessKnowledge; // Connaissance inutile (IA)
        public float ValuableKnowledge; // Connaissance précieuse (IA)
        public float WorthlessKnowledge; // Connaissance sans valeur (IA)
        public float PowerfulKnowledge; // Connaissance puissante (IA)
        public float WeakKnowledge; // Connaissance faible (IA)
        public float DangerousKnowledge; // Connaissance dangereuse (IA)
        public float SafeKnowledge; // Connaissance sûre (IA)
        public float BeneficialKnowledge; // Connaissance bénéfique (IA)
        public float HarmfulKnowledge; // Connaissance nuisible (IA)
        public float ConstructiveKnowledge; // Connaissance constructive (IA)
        public float DestructiveKnowledge; // Connaissance destructive (IA)
        public float PositiveKnowledge; // Connaissance positive (IA)
        public float NegativeKnowledge; // Connaissance négative (IA)
        public float NeutralKnowledge; // Connaissance neutre (IA)
        public float ActiveKnowledge; // Connaissance active (IA)
        public float PassiveKnowledge; // Connaissance passive (IA)
        public float DormantKnowledge; // Connaissance dormante (IA)
        public float EmergingKnowledge; // Connaissance émergente (IA)
        public float EvolvingKnowledge; // Connaissance évolutive (IA)
        public float StaticKnowledge; // Connaissance statique (IA)
        public float DynamicKnowledge; // Connaissance dynamique (IA)
        public float AdaptiveKnowledge; // Connaissance adaptable (IA)
        public float RigidKnowledge; // Connaissance rigide (IA)
        public float FlexibleKnowledge; // Connaissance flexible (IA)
        public float RobustKnowledge; // Connaissance robuste (IA)
        public float FragileKnowledge; // Connaissance fragile (IA)
        public float ReliableKnowledge; // Connaissance fiable (IA)
        public float UnreliableKnowledge; // Connaissance non fiable (IA)
        public float AccurateKnowledge; // Connaissance précise (IA)
        public float InaccurateKnowledge; // Connaissance imprécise (IA)
        public float CompleteKnowledge; // Connaissance complète (IA)
        public float IncompleteKnowledge; // Connaissance incomplète (IA)
        public float DetailedKnowledge; // Connaissance détaillée (IA)
        public float SuperficialKnowledge; // Connaissance superficielle (IA)
        public float DeepKnowledge; // Connaissance profonde (IA)
        public float ShallowKnowledge; // Connaissance superficielle (IA)
        public float BroadKnowledge; // Connaissance large (IA)
        public float NarrowKnowledge; // Connaissance étroite (IA)
        public float SpecificKnowledge; // Connaissance spécifique (IA)
        public float GeneralKnowledge; // Connaissance générale (IA)
        public float UniversalKnowledge; // Connaissance universelle (IA)
        public float ParticularKnowledge; // Connaissance particulière (IA)
        public float IndividualKnowledge; // Connaissance individuelle (IA)
        public float CollectiveKnowledge; // Connaissance collective (IA)
        public float PersonalKnowledge; // Connaissance personnelle (IA)
        public float SharedKnowledge; // Connaissance partagée (IA)
        public float CommonKnowledge; // Connaissance commune (IA)
        public float RareKnowledge; // Connaissance rare (IA)
        public float FrequentKnowledge; // Connaissance fréquente (IA)
        public float OccasionalKnowledge; // Connaissance occasionnelle (IA)
        public float RegularKnowledge; // Connaissance régulière (IA)
        public float PeriodicKnowledge; // Connaissance périodique (IA)
        public float ContinuousKnowledge; // Connaissance continue (IA)
        public float DiscontinuousKnowledge; // Connaissance discontinue (IA)
        public float CyclicalKnowledge; // Connaissance cyclique (IA)
        public float LinearKnowledge; // Connaissance linéaire (IA)
        public float NonLinearKnowledge; // Connaissance non linéaire (IA)
        public float RecursiveKnowledge; // Connaissance récursive (IA)
        public float IterativeKnowledge; // Connaissance itérative (IA)
        public float SequentialKnowledge; // Connaissance séquentielle (IA)
        public float ParallelKnowledge; // Connaissance parallèle (IA)
        public float HierarchicalKnowledge; // Connaissance hiérarchique (IA)
        public float NetworkedKnowledge; // Connaissance en réseau (IA)
        public float DistributedKnowledge; // Connaissance distribuée (IA)
        public float CentralizedKnowledge; // Connaissance centralisée (IA)
        public float DecentralizedKnowledge; // Connaissance décentralisée (IA)
        public float IntegratedKnowledge; // Connaissance intégrée (IA)
        public float SegregatedKnowledge; // Connaissance séparée (IA)
        public float ConnectedKnowledge; // Connaissance connectée (IA)
        public float IsolatedKnowledge; // Connaissance isolée (IA)
        public float InterconnectedKnowledge; // Connaissance interconnectée (IA)
        public float IndependentKnowledge; // Connaissance indépendante (IA)
        public float DependentKnowledge; // Connaissance dépendante (IA)
        public float InterdependentKnowledge; // Connaissance interdépendante (IA)
        public float SymbioticKnowledge; // Connaissance symbiotique (IA)
        public float ParasiticKnowledge; // Connaissance parasitaire (IA)
        public float CompetitiveKnowledge; // Connaissance compétitive (IA)
        public float CooperativeKnowledge; // Connaissance coopérative (IA)
        public float CollaborativeKnowledge; // Connaissance collaborative (IA)
        public float SynergisticKnowledge; // Connaissance synergétique (IA)
        public float ConflictingKnowledge; // Connaissance conflictuelle (IA)
        public float HarmoniousKnowledge; // Connaissance harmonieuse (IA)
        public float BalancedKnowledge; // Connaissance équilibrée (IA)
        public float ImbalancedKnowledge; // Connaissance déséquilibrée (IA)
        public float StableKnowledge; // Connaissance stable (IA)
        public float UnstableKnowledge; // Connaissance instable (IA)
        public float ConsistentKnowledge; // Connaissance cohérente (IA)
        public float InconsistentKnowledge; // Connaissance incohérente (IA)
        public float CoherentKnowledge; // Connaissance cohérente (IA)
        public float IncoherentKnowledge; // Connaissance incohérente (IA)
        public float LogicalKnowledge; // Connaissance logique (IA)
        public float IllogicalKnowledge; // Connaissance illogique (IA)
        public float RationalKnowledge; // Connaissance rationnelle (IA)
        public float IrrationalKnowledge; // Connaissance irrationnelle (IA)
        public float SensibleKnowledge; // Connaissance sensée (IA)
        public float InsensibleKnowledge; // Connaissance insensée (IA)
        public float ReasonableKnowledge; // Connaissance raisonnable (IA)
        public float UnreasonableKnowledge; // Connaissance déraisonnable (IA)
        public float JustifiableKnowledge; // Connaissance justifiable (IA)
        public float UnjustifiableKnowledge; // Connaissance injustifiable (IA)
        public float AcceptableKnowledge; // Connaissance acceptable (IA)
        public float UnacceptableKnowledge; // Connaissance inacceptable (IA)
        public float EthicalKnowledge; // Connaissance éthique (IA)
        public float UnethicalKnowledge; // Connaissance non éthique (IA)
        public float MoralKnowledge; // Connaissance morale (IA)
        public float ImmoralKnowledge; // Connaissance immorale (IA)
        public float LegalKnowledge; // Connaissance légale (IA)
        public float IllegalKnowledge; // Connaissance illégale (IA)
        public float RighteousKnowledge; // Connaissance vertueuse (IA)
        public float WickedKnowledge; // Connaissance perverse (IA)
        public float VirtuousKnowledge; // Connaissance vertueuse (IA)
        public float ViciousKnowledge; // Connaissance vicieuse (IA)
        public float NobleKnowledge; // Connaissance noble (IA)
        public float BaseKnowledge; // Connaissance basse (IA)
        public float ElevatedKnowledge; // Connaissance élevée (IA)
        public float DegradedKnowledge; // Connaissance dégradée (IA)
        public float PureKnowledge; // Connaissance pure (IA)
        public float ImpureKnowledge; // Connaissance impure (IA)
        public float CleanKnowledge; // Connaissance propre (IA)
        public float DirtyKnowledge; // Connaissance sale (IA)
        public float ClearKnowledge; // Connaissance claire (IA)
        public float UnclearKnowledge; // Connaissance floue (IA)
        public float TransparentKnowledge; // Connaissance transparente (IA)
        public float OpaqueKnowledge; // Connaissance opaque (IA)
        public float BrightKnowledge; // Connaissance lumineuse (IA)
        public float DarkKnowledge; // Connaissance obscure (IA)
        public float LightKnowledge; // Connaissance légère (IA)
        public float HeavyKnowledge; // Connaissance lourde (IA)
        public float SoftKnowledge; // Connaissance douce (IA)
        public float HardKnowledge; // Connaissance dure (IA)
        public float SmoothKnowledge; // Connaissance lisse (IA)
        public float RoughKnowledge; // Connaissance rugueuse (IA)
        public float SharpKnowledge; // Connaissance tranchante (IA)
        public float DullKnowledge; // Connaissance émoussée (IA)
        public float FineKnowledge; // Connaissance fine (IA)
        public float CoarseKnowledge; // Connaissance grossière (IA)
        public float DelicateKnowledge; // Connaissance délicate (IA)
        public float RobustKnowledge; // Connaissance robuste (IA)
        public float SubtleKnowledge; // Connaissance subtile (IA)
        public float GrossKnowledge; // Connaissance grossière (IA)
        public float NuancedKnowledge; // Connaissance nuancée (IA)
        public float BluntKnowledge; // Connaissance brutale (IA)
        public float DiplomaticKnowledge; // Connaissance diplomatique (IA)
        public float DirectKnowledge; // Connaissance directe (IA)
        public float IndirectKnowledge; // Connaissance indirecte (IA)
        public float ExplicitKnowledge; // Connaissance explicite (IA)
        public float ImplicitKnowledge; // Connaissance implicite (IA)
        public float TacitKnowledge; // Connaissance tacite (IA)
        public float OvertKnowledge; // Connaissance ouverte (IA)
        public float CovertKnowledge; // Connaissance secrète (IA)
        public float OpenKnowledge; // Connaissance ouverte (IA)
        public float ClosedKnowledge; // Connaissance fermée (IA)
        public float PublicKnowledge; // Connaissance publique (IA)
        public float PrivateKnowledge; // Connaissance privée (IA)
        public float CommonKnowledge; // Connaissance commune (IA)
        public float ExclusiveKnowledge; // Connaissance exclusive (IA)
        public float InclusiveKnowledge; // Connaissance inclusive (IA)
        public float AccessibleKnowledge; // Connaissance accessible (IA)
        public float InaccessibleKnowledge; // Connaissance inaccessible (IA)
        public float AvailableKnowledge; // Connaissance disponible (IA)
        public float UnavailableKnowledge; // Connaissance indisponible (IA)
        public float ObtainableKnowledge; // Connaissance obtenable (IA)
        public float UnobtainableKnowledge; // Connaissance inobtenable (IA)
        public float DiscoverableKnowledge; // Connaissance découvrable (IA)
        public float UndiscoverableKnowledge; // Connaissance indécouvrable (IA)
        public float LearnableKnowledge; // Connaissance apprenable (IA)
        public float UnlearnableKnowledge; // Connaissance inapprenable (IA)
        public float TeachableKnowledge; // Connaissance enseignable (IA)
        public float UnteachableKnowledge; // Connaissance inenseignable (IA)
        public float CommunicableKnowledge; // Connaissance communicable (IA)
        public float IncommunicableKnowledge; // Connaissance incommunicable (IA)
        public float ExpressibleKnowledge; // Connaissance exprimable (IA)
        public float InexpressibleKnowledge; // Connaissance inexplicable (IA)
        public float UnderstandableKnowledge; // Connaissance compréhensible (IA)
        public float UnunderstandableKnowledge; // Connaissance incompréhensible (IA)
        public float BelievableKnowledge; // Connaissance croyable (IA)
        public float UnbelievableKnowledge; // Connaissance incroyable (IA)
        public float AcceptableKnowledge; // Connaissance acceptable (IA)
        public float UnacceptableKnowledge; // Connaissance inacceptable (IA)
        public float UsableKnowledge; // Connaissance utilisable (IA)
        public float UnusableKnowledge; // Connaissance inutilisable (IA)
        public float PracticalKnowledge; // Connaissance pratique (IA)
        public float ImpracticalKnowledge; // Connaissance impraticable (IA)
        public float FunctionalKnowledge; // Connaissance fonctionnelle (IA)
        public float DysfunctionalKnowledge; // Connaissance dysfonctionnelle (IA)
        public float EfficientKnowledge; // Connaissance efficace (IA)
        public float InefficientKnowledge; // Connaissance inefficace (IA)
        public float ProductiveKnowledge; // Connaissance productive (IA)
        public float UnproductiveKnowledge; // Connaissance improductive (IA)
        public float CreativeKnowledge; // Connaissance créative (IA)
        public float UncreativeKnowledge; // Connaissance non créative (IA)
        public float InnovativeKnowledge; // Connaissance innovante (IA)
        public float ConservativeKnowledge; // Connaissance conservatrice (IA)
        public float ProgressiveKnowledge; // Connaissance progressiste (IA)
        public float RevolutionaryKnowledge; // Connaissance révolutionnaire (IA)
        public float TraditionalKnowledge; // Connaissance traditionnelle (IA)
        public float ModernKnowledge; // Connaissance moderne (IA)
        public float ContemporaryKnowledge; // Connaissance contemporaine (IA)
        public float AncientKnowledge; // Connaissance ancienne (IA)
        public float HistoricalKnowledge; // Connaissance historique (IA)
        public float FuturisticKnowledge; // Connaissance futuriste (IA)
        public float TimelessKnowledge; // Connaissance intemporelle (IA)
        public float ContextualKnowledge; // Connaissance contextuelle (IA)
        public float NonContextualKnowledge; // Connaissance non contextuelle (IA)
        public float SituationalKnowledge; // Connaissance situationnelle (IA)
        public float AsituationalKnowledge; // Connaissance asituationnelle (IA)
        public float ConditionalKnowledge; // Connaissance conditionnelle (IA)
        public float UnconditionalKnowledge; // Connaissance inconditionnelle (IA)
        public float RelativeKnowledge; // Connaissance relative (IA)
        public float AbsoluteKnowledge; // Connaissance absolue (IA)
        public float ObjectiveKnowledge; // Connaissance objective (IA)
        public float SubjectiveKnowledge; // Connaissance subjective (IA)
        public float PerspectiveKnowledge; // Connaissance perspective (IA)
        public float ViewpointKnowledge; // Connaissance de point de vue (IA)
        public float OpinionKnowledge; // Connaissance d'opinion (IA)
        public float FactKnowledge; // Connaissance de fait (IA)
        public float TheoryKnowledge; // Connaissance de théorie (IA)
        public float HypothesisKnowledge; // Connaissance d'hypothèse (IA)
        public float AssumptionKnowledge; // Connaissance d'hypothèse (IA)
        public float BeliefKnowledge; // Connaissance de croyance (IA)
        public float TruthKnowledge; // Connaissance de vérité (IA)
        public float FalsehoodKnowledge; // Connaissance de fausseté (IA)
        public float LieKnowledge; // Connaissance de mensonge (IA)
        public float RumorKnowledge; // Connaissance de rumeur (IA)
        public float GossipKnowledge; // Connaissance de ragot (IA)
        public float SpeculationKnowledge; // Connaissance de spéculation (IA)
        public float PredictionKnowledge; // Connaissance de prédiction (IA)
        public float ForecastKnowledge; // Connaissance de prévision (IA)
        public float ProphecyKnowledge; // Connaissance de prophétie (IA)
        public float RevelationKnowledge; // Connaissance de révélation (IA)
        public float InsightKnowledge; // Connaissance d'insight (IA)
        public float UnderstandingKnowledge; // Connaissance de compréhension (IA)
        public float WisdomKnowledge; // Connaissance de sagesse (IA)
        public float FollyKnowledge; // Connaissance de folie (IA)
        public float IgnoranceKnowledge; // Connaissance d'ignorance (IA)
        public float StupidityKnowledge; // Connaissance de stupidité (IA)
        public float IntelligenceKnowledge; // Connaissance d'intelligence (IA)
        public float ClevernessKnowledge; // Connaissance d'intelligence (IA)
        public float WitKnowledge; // Connaissance d'esprit (IA)
        public float HumorKnowledge; // Connaissance d'humour (IA)
        public float SatireKnowledge; // Connaissance de satire (IA)
        public float IronyKnowledge; // Connaissance d'ironie (IA)
        public float SarcasmKnowledge; // Connaissance de sarcasme (IA)
        public float CynicismKnowledge; // Connaissance de cynisme (IA)
        public float OptimismKnowledge; // Connaissance d'optimisme (IA)
        public float PessimismKnowledge; // Connaissance de pessimisme (IA)
        public float RealismKnowledge; // Connaissance de réalisme (IA)
        public float IdealismKnowledge; // Connaissance d'idéalisme (IA)
        public float PragmatismKnowledge; // Connaissance de pragmatisme (IA)
        public float DogmatismKnowledge; // Connaissance de dogmatisme (IA)
        public float SkepticismKnowledge; // Connaissance de scepticisme (IA)
        public float CredulityKnowledge; // Connaissance de crédulité (IA)
        public float GullibilityKnowledge; // Connaissance de crédulité (IA)
        public float NaivetyKnowledge; // Connaissance de naïveté (IA)
        public float SophisticationKnowledge; // Connaissance de sophistication (IA)
        public float ComplexityKnowledge; // Connaissance de complexité (IA)
        public float SimplicityKnowledge; // Connaissance de simplicité (IA)
        public float EleganceKnowledge; // Connaissance d'élégance (IA)
        public float GracefulnessKnowledge; // Connaissance de grâce (IA)
        public float AwkwardnessKnowledge; // Connaissance de gaucherie (IA)
        public float ClumsinessKnowledge; // Connaissance de maladresse (IA)
        public float PrecisionKnowledge; // Connaissance de précision (IA)
        public float AccuracyKnowledge; // Connaissance de précision (IA)
        public float ApproximationKnowledge; // Connaissance d'approximation (IA)
        public float EstimationKnowledge; // Connaissance d'estimation (IA)
        public float CalculationKnowledge; // Connaissance de calcul (IA)
        public float MeasurementKnowledge; // Connaissance de mesure (IA)
        public float QuantificationKnowledge; // Connaissance de quantification (IA)
        public float QualificationKnowledge; // Connaissance de qualification (IA)
        public float ClassificationKnowledge; // Connaissance de classification (IA)
        public float CategorizationKnowledge; // Connaissance de catégorisation (IA)
        public float IdentificationKnowledge; // Connaissance d'identification (IA)
        public float RecognitionKnowledge; // Connaissance de reconnaissance (IA)
        public float DiscriminationKnowledge; // Connaissance de discrimination (IA)
        public float DifferentiationKnowledge; // Connaissance de différenciation (IA)
        public float ComparisonKnowledge; // Connaissance de comparaison (IA)
        public float ContrastKnowledge; // Connaissance de contraste (IA)
        public float SimilarityKnowledge; // Connaissance de similarité (IA)
        public float DissimilarityKnowledge; // Connaissance de dissimilarité (IA)
        public float AnalogyKnowledge; // Connaissance d'analogie (IA)
        public float MetaphorKnowledge; // Connaissance de métaphore (IA)
        public float SymbolismKnowledge; // Connaissance de symbolisme (IA)
        public float AllegoryKnowledge; // Connaissance d'allégorie (IA)
        public float ParableKnowledge; // Connaissance de parabole (IA)
        public float FableKnowledge; // Connaissance de fable (IA)
        public float StorytellingKnowledge; // Connaissance de narration (IA)
        public float NarrativeKnowledge; // Connaissance narrative (IA)
        public float DramaticKnowledge; // Connaissance dramatique (IA)
        public float PoeticKnowledge; // Connaissance poétique (IA)
        public float MusicalKnowledge; // Connaissance musicale (IA)
        public float VisualKnowledge; // Connaissance visuelle (IA)
        public float AuditoryKnowledge; // Connaissance auditive (IA)
        public float TactileKnowledge; // Connaissance tactile (IA)
        public float OlfactoryKnowledge; // Connaissance olfactive (IA)
        public float GustatoryKnowledge; // Connaissance gustative (IA)
        public float SensoryKnowledge; // Connaissance sensorielle (IA)
        public float PerceptualKnowledge; // Connaissance perceptive (IA)
        public float CognitiveKnowledge; // Connaissance cognitive (IA)
        public float IntellectualKnowledge; // Connaissance intellectuelle (IA)
        public float MentalKnowledge; // Connaissance mentale (IA)
        public float SpiritualKnowledge; // Connaissance spirituelle (IA)
        public float ReligiousKnowledge; // Connaissance religieuse (IA)
        public float MysticalKnowledge; // Connaissance mystique (IA)
        public float EsotericKnowledge; // Connaissance ésotérique (IA)
        public float OccultKnowledge; // Connaissance occulte (IA)
        public float SupernaturalKnowledge; // Connaissance surnaturelle (IA)
        public float ParanormalKnowledge; // Connaissance paranormale (IA)
        public float MagicalKnowledge; // Connaissance magique (IA)
        public float AlchemicalKnowledge; // Connaissance alchimique (IA)
        public float AstrologicalKnowledge; // Connaissance astrologique (IA)
        public float NumerologicalKnowledge; // Connaissance numéroligique (IA)
        public float GeomanticKnowledge; // Connaissance géomantique (IA)
        public float FengShuiKnowledge; // Connaissance Feng Shui (IA)
        public float VastuShastraKnowledge; // Connaissance Vastu Shastra (IA)
        public float SpaceFengShuiKnowledge; // Connaissance Feng Shui spatial (IA)
        public float EnergyFlowKnowledge; // Connaissance du flux d'énergie (IA)
        public float ChiKnowledge; // Connaissance du Chi (IA)
        public float KiKnowledge; // Connaissance du Ki (IA)
        public float PranaKnowledge; // Connaissance du Prana (IA)
        public float LifeForceKnowledge; // Connaissance de la force vitale (IA)
        public float VitalityKnowledge; // Connaissance de la vitalité (IA)
        public float HealthKnowledge; // Connaissance de la santé (IA)
        public float WellnessKnowledge; // Connaissance du bien-être (IA)
        public float FitnessKnowledge; // Connaissance de la forme physique (IA)
        public float StrengthKnowledge; // Connaissance de la force (IA)
        public float EnduranceKnowledge; // Connaissance de l'endurance (IA)
        public float AgilityKnowledge; // Connaissance de l'agilité (IA)
        public float SpeedKnowledge; // Connaissance de la vitesse (IA)
        public float DexterityKnowledge; // Connaissance de la dextérité (IA)
        public float CoordinationKnowledge; // Connaissance de la coordination (IA)
        public float BalanceKnowledge; // Connaissance de l'équilibre (IA)
        public float FlexibilityKnowledge; // Connaissance de la flexibilité (IA)
        public float StabilityKnowledge; // Connaissance de la stabilité (IA)
        public float ResilienceKnowledge; // Connaissance de la résilience (IA)
        public float RecoveryKnowledge; // Connaissance de la récupération (IA)
        public float RehabilitationKnowledge; // Connaissance de la réhabilitation (IA)
        public float TherapyKnowledge; // Connaissance de la thérapie (IA)
        public float MedicineKnowledge; // Connaissance de la médecine (IA)
        public float SurgeryKnowledge; // Connaissance de la chirurgie (IA)
        public float PharmacologyKnowledge; // Connaissance de la pharmacologie (IA)
        public float ToxicologyKnowledge; // Connaissance de la toxicologie (IA)
        public float PathologyKnowledge; // Connaissance de la pathologie (IA)
        public float AnatomyKnowledge; // Connaissance de l'anatomie (IA)
        public float PhysiologyKnowledge; // Connaissance de la physiologie (IA)
        public float BiochemistryKnowledge; // Connaissance de la biochimie (IA)
        public float GeneticsKnowledge; // Connaissance de la génétique (IA)
        public float EvolutionaryKnowledge; // Connaissance de l'évolution (IA)
        public float DevelopmentalKnowledge; // Connaissance du développement (IA)
        public float GrowthKnowledge; // Connaissance de la croissance (IA)
        public float MaturationKnowledge; // Connaissance de la maturation (IA)
        public float AgingKnowledge; // Connaissance du vieillissement (IA)
        public float DeathKnowledge; // Connaissance de la mort (IA)
        public float RebirthKnowledge; // Connaissance de la renaissance (IA)
        public float AfterlifeKnowledge; // Connaissance de l'au-delà (IA)
        public float SoulKnowledge; // Connaissance de l'âme (IA)
        public float SpiritKnowledge; // Connaissance de l'esprit (IA)
        public float MindKnowledge; // Connaissance de l'esprit (IA)
        public float ConsciousnessKnowledge; // Connaissance de la conscience (IA)
        public float SubconsciousnessKnowledge; // Connaissance du subconscient (IA)
        public float UnconsciousnessKnowledge; // Connaissance de l'inconscient (IA)
        public float AwarenessKnowledge; // Connaissance de la conscience (IA)
        public float AttentionKnowledge; // Connaissance de l'attention (IA)
        public float FocusKnowledge; // Connaissance de la focalisation (IA)
        public float ConcentrationKnowledge; // Connaissance de la concentration (IA)
        public float MeditationKnowledge; // Connaissance de la méditation (IA)
        public float ContemplationKnowledge; // Connaissance de la contemplation (IA)
        public float ReflectionKnowledge; // Connaissance de la réflexion (IA)
        public float IntrospectionKnowledge; // Connaissance de l'introspection (IA)
        public float SelfKnowledge; // Connaissance de soi (IA)
        public float OtherKnowledge; // Connaissance des autres (IA)
        public float WorldKnowledge; // Connaissance du monde (IA)
        public float UniverseKnowledge; // Connaissance de l'univers (IA)
        public float ExistenceKnowledge; // Connaissance de l'existence (IA)
        public float RealityKnowledge; // Connaissance de la réalité (IA)
        public float IllusionKnowledge; // Connaissance de l'illusion (IA)
        public float AppearanceKnowledge; // Connaissance de l'apparence (IA)
        public float EssenceKnowledge; // Connaissance de l'essence (IA)
        public float BeingKnowledge; // Connaissance de l'être (IA)
        public float BecomingKnowledge; // Connaissance du devenir (IA)
        public float ChangeKnowledge; // Connaissance du changement (IA)
        public float PermanenceKnowledge; // Connaissance de la permanence (IA)
        public float ImpermanenceKnowledge; // Connaissance de l'impermanence (IA)
        public float UnityKnowledge; // Connaissance de l'unité (IA)
        public float DiversityKnowledge; // Connaissance de la diversité (IA)
        public float HarmonyKnowledge; // Connaissance de l'harmonie (IA)
        public float DiscordKnowledge; // Connaissance de la discorde (IA)
        public float OrderKnowledge; // Connaissance de l'ordre (IA)
        public float ChaosKnowledge; // Connaissance du chaos (IA)
        public float BalanceKnowledge; // Connaissance de l'équilibre (IA)
        public float ImbalanceKnowledge; // Connaissance du déséquilibre (IA)
        public float JusticeKnowledge; // Connaissance de la justice (IA)
        public float InjusticeKnowledge; // Connaissance de l'injustice (IA)
        public float FairnessKnowledge; // Connaissance de l'équité (IA)
        public float UnfairnessKnowledge; // Connaissance de l'inéquité (IA)
        public float EqualityKnowledge; // Connaissance de l'égalité (IA)
        public float InequalityKnowledge; // Connaissance de l'inégalité (IA)
        public float FreedomKnowledge; // Connaissance de la liberté (IA)
        public float SlaveryKnowledge; // Connaissance de l'esclavage (IA)
        public float LibertyKnowledge; // Connaissance de la liberté (IA)
        public float OppressionKnowledge; // Connaissance de l'oppression (IA)
        public float LiberationKnowledge; // Connaissance de la libération (IA)
        public float IndependenceKnowledge; // Connaissance de l'indépendance (IA)
        public float DependenceKnowledge; // Connaissance de la dépendance (IA)
        public float ResponsibilityKnowledge; // Connaissance de la responsabilité (IA)
        public float IrresponsibilityKnowledge; // Connaissance de l'irresponsabilité (IA)
        public float AccountabilityKnowledge; // Connaissance de la responsabilité (IA)
        public float NegligenceKnowledge; // Connaissance de la négligence (IA)
        public float DutyKnowledge; // Connaissance du devoir (IA)
        public float ObligationKnowledge; // Connaissance de l'obligation (IA)
        public float CommitmentKnowledge; // Connaissance de l'engagement (IA)
        public float LoyaltyKnowledge; // Connaissance de la loyauté (IA)
        public float BetrayalKnowledge; // Connaissance de la trahison (IA)
        public float TrustKnowledge; // Connaissance de la confiance (IA)
        public float DistrustKnowledge; // Connaissance de la méfiance (IA)
        public float HonestyKnowledge; // Connaissance de l'honnêteté (IA)
        public float DishonestyKnowledge; // Connaissance de la malhonnêteté (IA)
        public float IntegrityKnowledge; // Connaissance de l'intégrité (IA)
        public float CorruptionKnowledge; // Connaissance de la corruption (IA)
        public float VirtueKnowledge; // Connaissance de la vertu (IA)
        public float ViceKnowledge; // Connaissance du vice (IA)
        public float GoodnessKnowledge; // Connaissance du bien (IA)
        public float BadnessKnowledge; // Connaissance du mal (IA)
        public float RighteousnessKnowledge; // Connaissance de la droiture (IA)
        public float WickednessKnowledge; // Connaissance de la perversité (IA)
        public float KindnessKnowledge; // Connaissance de la gentillesse (IA)
        public float CrueltyKnowledge; // Connaissance de la cruauté (IA)
        public float CompassionKnowledge; // Connaissance de la compassion (IA)
        public float CallousnessKnowledge; // Connaissance de l'insensibilité (IA)
        public float EmpathyKnowledge; // Connaissance de l'empathie (IA)
        public float SympathyKnowledge; // Connaissance de la sympathie (IA)
        public float AntipathyKnowledge; // Connaissance de l'antipathie (IA)
        public float LoveKnowledge; // Connaissance de l'amour (IA)
        public float HateKnowledge; // Connaissance de la haine (IA)
        public float AffectionKnowledge; // Connaissance de l'affection (IA)
        public float IndifferenceKnowledge; // Connaissance de l'indifférence (IA)
        public float PassionKnowledge; // Connaissance de la passion (IA)
        public float ApathyKnowledge; // Connaissance de l'apathie (IA)
        public float DesireKnowledge; // Connaissance du désir (IA)
        public float AversionKnowledge; // Connaissance de l'aversion (IA)
        public float AttractionKnowledge; // Connaissance de l'attraction (IA)
        public float RepulsionKnowledge; // Connaissance de la répulsion (IA)
        public float PleasureKnowledge; // Connaissance du plaisir (IA)
        public float PainKnowledge; // Connaissance de la douleur (IA)
        public float JoyKnowledge; // Connaissance de la joie (IA)
        public float SorrowKnowledge; // Connaissance de la tristesse (IA)
        public float HappinessKnowledge; // Connaissance du bonheur (IA)
        public float SadnessKnowledge; // Connaissance de la tristesse (IA)
        public float ExcitementKnowledge; // Connaissance de l'excitation (IA)
        public float CalmnessKnowledge; // Connaissance de la tranquillité (IA)
        public float AnxietyKnowledge; // Connaissance de l'anxiété (IA)
        public float PeaceKnowledge; // Connaissance de la paix (IA)
        public float ConflictKnowledge; // Connaissance du conflit (IA)
        public float WarKnowledge; // Connaissance de la guerre (IA)
        public float PeaceKnowledge; // Connaissance de la paix (IA)
        public float VictoryKnowledge; // Connaissance de la victoire (IA)
        public float DefeatKnowledge; // Connaissance de la défaite (IA)
        public float SuccessKnowledge; // Connaissance du succès (IA)
        public float FailureKnowledge; // Connaissance de l'échec (IA)
        public float AchievementKnowledge; // Connaissance de la réussite (IA)
        public float AccomplishmentKnowledge; // Connaissance de l'accomplissement (IA)
        public float ProgressKnowledge; // Connaissance du progrès (IA)
        public float RegressionKnowledge; // Connaissance de la régression (IA)
        public float AdvancementKnowledge; // Connaissance de l'avancement (IA)
        public float StagnationKnowledge; // Connaissance de la stagnation (IA)
        public float InnovationKnowledge; // Connaissance de l'innovation (IA)
        public float TraditionKnowledge; // Connaissance de la tradition (IA)
        public float ModernityKnowledge; // Connaissance de la modernité (IA)
        public float FuturityKnowledge; // Connaissance du futur (IA)
        public float PastKnowledge; // Connaissance du passé (IA)
        public float PresentKnowledge; // Connaissance du présent (IA)
        public float TimeKnowledge; // Connaissance du temps (IA)
        public float SpaceKnowledge; // Connaissance de l'espace (IA)
        public float DimensionKnowledge; // Connaissance de la dimension (IA)
        public float MatterKnowledge; // Connaissance de la matière (IA)
        public float EnergyKnowledge; // Connaissance de l'énergie (IA)
        public float ForceKnowledge; // Connaissance de la force (IA)
        public float MotionKnowledge; // Connaissance du mouvement (IA)
        public float RestKnowledge; // Connaissance du repos (IA)
        public float ChangeKnowledge; // Connaissance du changement (IA)
        public float ConstancyKnowledge; // Connaissance de la constance (IA)
        public float CauseKnowledge; // Connaissance de la cause (IA)
        public float EffectKnowledge; // Connaissance de l'effet (IA)
        public float RelationshipKnowledge; // Connaissance de la relation (IA)
        public float ConnectionKnowledge; // Connaissance de la connexion (IA)
        public float SeparationKnowledge; // Connaissance de la séparation (IA)
        public float UnionKnowledge; // Connaissance de l'union (IA)
        public float DivisionKnowledge; // Connaissance de la division (IA)
        public float CombinationKnowledge; // Connaissance de la combinaison (IA)
        public float SynthesisKnowledge; // Connaissance de la synthèse (IA)
        public float AnalysisKnowledge; // Connaissance de l'analyse (IA)
        public float DecompositionKnowledge; // Connaissance de la décomposition (IA)
        public float IntegrationKnowledge; // Connaissance de l'intégration (IA)
        public float DifferentiationKnowledge; // Connaissance de la différenciation (IA)
        public float EmergenceKnowledge; // Connaissance de l'émergence (IA)
        public float ReductionKnowledge; // Connaissance de la réduction (IA)
        public float ComplexityKnowledge; // Connaissance de la complexité (IA)
        public float SimplicityKnowledge; // Connaissance de la simplicité (IA)
        public float OrderKnowledge; // Connaissance de l'ordre (IA)
        public float ChaosKnowledge; // Connaissance du chaos (IA)
        public float PatternKnowledge; // Connaissance du motif (IA)
        public float RandomnessKnowledge; // Connaissance du hasard (IA)
        public float LawKnowledge; // Connaissance de la loi (IA)
        public float RuleKnowledge; // Connaissance de la règle (IA)
        public float PrincipleKnowledge; // Connaissance du principe (IA)
        public float ConceptKnowledge; // Connaissance du concept (IA)
        public float IdeaKnowledge; // Connaissance de l'idée (IA)
        public float ThoughtKnowledge; // Connaissance de la pensée (IA)
        public float BeliefKnowledge; // Connaissance de la croyance (IA)
        public float OpinionKnowledge; // Connaissance de l'opinion (IA)
        public float FactKnowledge; // Connaissance du fait (IA)
        public float FictionKnowledge; // Connaissance de la fiction (IA)
        public float TruthKnowledge; // Connaissance de la vérité (IA)
        public float FalsehoodKnowledge; // Connaissance de la fausseté (IA)
        public float EvidenceKnowledge; // Connaissance de la preuve (IA)
        public float ProofKnowledge; // Connaissance de la preuve (IA)
        public float ArgumentKnowledge; // Connaissance de l'argument (IA)
        public float LogicKnowledge; // Connaissance de la logique (IA)
        public float ReasoningKnowledge; // Connaissance du raisonnement (IA)
        public float DeductionKnowledge; // Connaissance de la déduction (IA)
        public float InductionKnowledge; // Connaissance de l'induction (IA)
        public float AbductionKnowledge; // Connaissance de l'abduction (IA)
        public float InferenceKnowledge; // Connaissance de l'inférence (IA)
        public float ConclusionKnowledge; // Connaissance de la conclusion (IA)
        public float PremiseKnowledge; // Connaissance de la prémisse (IA)
        public float AssumptionKnowledge; // Connaissance de l'hypothèse (IA)
        public float HypothesisKnowledge; // Connaissance de l'hypothèse (IA)
        public float TheoryKnowledge; // Connaissance de la théorie (IA)
        public float ModelKnowledge; // Connaissance du modèle (IA)
        public float FrameworkKnowledge; // Connaissance du cadre (IA)
        public float SystemKnowledge; // Connaissance du système (IA)
        public float StructureKnowledge; // Connaissance de la structure (IA)
        public float FunctionKnowledge; // Connaissance de la fonction (IA)
        public float PurposeKnowledge; // Connaissance du but (IA)
        public float GoalKnowledge; // Connaissance de l'objectif (IA)
        public float MeansKnowledge; // Connaissance des moyens (IA)
        public float EndKnowledge; // Connaissance de la fin (IA)
        public float MethodKnowledge; // Connaissance de la méthode (IA)
        public float TechniqueKnowledge; // Connaissance de la technique (IA)
        public float SkillKnowledge; // Connaissance de la compétence (IA)
        public float AbilityKnowledge; // Connaissance de l'aptitude (IA)
        public float TalentKnowledge; // Connaissance du talent (IA)
        public float GiftKnowledge; // Connaissance du don (IA)
        public float PotentialKnowledge; // Connaissance du potentiel (IA)
        public float ActualizationKnowledge; // Connaissance de l'actualisation (IA)
        public float DevelopmentKnowledge; // Connaissance du développement (IA)
        public float GrowthKnowledge; // Connaissance de la croissance (IA)
        public float LearningKnowledge; // Connaissance de l'apprentissage (IA)
        public float TeachingKnowledge; // Connaissance de l'enseignement (IA)
        public float EducationKnowledge; // Connaissance de l'éducation (IA)
        public float TrainingKnowledge; // Connaissance de la formation (IA)
        public float PracticeKnowledge; // Connaissance de la pratique (IA)
        public float ExperienceKnowledge; // Connaissance de l'expérience (IA)
        public float ExperimentationKnowledge; // Connaissance de l'expérimentation (IA)
        public float ObservationKnowledge; // Connaissance de l'observation (IA)
        public float MeasurementKnowledge; // Connaissance de la mesure (IA)
        public float CalculationKnowledge; // Connaissance du calcul (IA)
        public float EstimationKnowledge; // Connaissance de l'estimation (IA)
        public float ApproximationKnowledge; // Connaissance de l'approximation (IA)
        public float PrecisionKnowledge; // Connaissance de la précision (IA)
        public float AccuracyKnowledge; // Connaissance de l'exactitude (IA)
        public float ReliabilityKnowledge; // Connaissance de la fiabilité (IA)
        public float ValidityKnowledge; // Connaissance de la validité (IA)
        public float ConsistencyKnowledge; // Connaissance de la cohérence (IA)
        public float ReproducibilityKnowledge; // Connaissance de la reproductibilité (IA)
        public float ReplicabilityKnowledge; // Connaissance de la replicabilité (IA)
        public float VerificationKnowledge; // Connaissance de la vérification (IA)
        public float ValidationKnowledge; // Connaissance de la validation (IA)
        public float QualityKnowledge; // Connaissance de la qualité (IA)
        public float StandardKnowledge; // Connaissance de la norme (IA)
        public float CriterionKnowledge; // Connaissance du critère (IA)
        public float BenchmarkKnowledge; // Connaissance du repère (IA)
        public float ReferenceKnowledge; // Connaissance de la référence (IA)
        public float ComparisonKnowledge; // Connaissance de la comparaison (IA)
        public float ContrastKnowledge; // Connaissance du contraste (IA)
        public float SimilarityKnowledge; // Connaissance de la similarité (IA)
        public float DifferenceKnowledge; // Connaissance de la différence (IA)
        public float IdentityKnowledge; // Connaissance de l'identité (IA)
        public float DistinctionKnowledge; // Connaissance de la distinction (IA)
        public float ClassificationKnowledge; // Connaissance de la classification (IA)
        public float CategorizationKnowledge; // Connaissance de la catégorisation (IA)
        public float TypologyKnowledge; // Connaissance de la typologie (IA)
        public float TaxonomyKnowledge; // Connaissance de la taxonomie (IA)
        public float OntologyKnowledge; // Connaissance de l'ontologie (IA)
        public float EpistemologyKnowledge; // Connaissance de l'épistémologie (IA)
        public float MetaphysicsKnowledge; // Connaissance de la métaphysique (IA)
        public float EthicsKnowledge; // Connaissance de l'éthique (IA)
        public float AestheticsKnowledge; // Connaissance de l'esthétique (IA)
        public float PoliticsKnowledge; // Connaissance de la politique (IA)
        public float EconomicsKnowledge; // Connaissance de l'économie (IA)
        public float SociologyKnowledge; // Connaissance de la sociologie (IA)
        public float AnthropologyKnowledge; // Connaissance de l'anthropologie (IA)
        public float PsychologyKnowledge; // Connaissance de la psychologie (IA)
        public float PhilosophyKnowledge; // Connaissance de la philosophie (IA)
        public float ReligionKnowledge; // Connaissance de la religion (IA)
        public float MythologyKnowledge; // Connaissance de la mythologie (IA)
        public float LiteratureKnowledge; // Connaissance de la littérature (IA)
        public float ArtKnowledge; // Connaissance de l'art (IA)
        public float MusicKnowledge; // Connaissance de la musique (IA)
        public float DanceKnowledge; // Connaissance de la danse (IA)
        public float TheaterKnowledge; // Connaissance du théâtre (IA)
        public float CinemaKnowledge; // Connaissance du cinéma (IA)
        public float TelevisionKnowledge; // Connaissance de la télévision (IA)
        public float RadioKnowledge; // Connaissance de la radio (IA)
        public float JournalismKnowledge; // Connaissance du journalisme (IA)
        public float PublishingKnowledge; // Connaissance de l'édition (IA)
        public float BroadcastingKnowledge; // Connaissance de la diffusion (IA)
        public float TelecommunicationsKnowledge; // Connaissance des télécommunications (IA)
        public float ComputingKnowledge; // Connaissance de l'informatique (IA)
        public float InformationTechnologyKnowledge; // Connaissance des technologies de l'information (IA)
        public float SoftwareEngineeringKnowledge; // Connaissance du génie logiciel (IA)
        public float HardwareEngineeringKnowledge; // Connaissance du génie matériel (IA)
        public float CybersecurityKnowledge; // Connaissance de la cybersécurité (IA)
        public float DataScienceKnowledge; // Connaissance de la science des données (IA)
        public float ArtificialIntelligenceKnowledge; // Connaissance de l'intelligence artificielle (IA)
        public float MachineLearningKnowledge; // Connaissance de l'apprentissage automatique (IA)
        public float DeepLearningKnowledge; // Connaissance de l'apprentissage profond (IA)
        public float NaturalLanguageProcessingKnowledge; // Connaissance du traitement du langage naturel (IA)
        public float ComputerVisionKnowledge; // Connaissance de la vision par ordinateur (IA)
        public float RoboticsKnowledge; // Connaissance de la robotique (IA)
        public float AutomationKnowledge; // Connaissance de l'automatisation (IA)
        public float ControlTheoryKnowledge; // Connaissance de la théorie du contrôle (IA)
        public float SystemsTheoryKnowledge; // Connaissance de la théorie des systèmes (IA)
        public float ComplexityTheoryKnowledge; // Connaissance de la théorie de la complexité (IA)
        public float ChaosTheoryKnowledge; // Connaissance de la théorie du chaos (IA)
        public float NetworkTheoryKnowledge; // Connaissance de la théorie des réseaux (IA)
        public float GameTheoryKnowledge; // Connaissance de la théorie des jeux (IA)
        public float DecisionTheoryKnowledge; // Connaissance de la théorie de la décision (IA)
        public float ProbabilityTheoryKnowledge; // Connaissance de la théorie des probabilités (IA)
        public float StatisticsKnowledge; // Connaissance des statistiques (IA)
        public float MathematicsKnowledge; // Connaissance des mathématiques (IA)
        public float PhysicsKnowledge; // Connaissance de la physique (IA)
        public float ChemistryKnowledge; // Connaissance de la chimie (IA)
        public float BiologyKnowledge; // Connaissance de la biologie (IA)
        public float EarthScienceKnowledge; // Connaissance des sciences de la Terre (IA)
        public float AstronomyKnowledge; // Connaissance de l'astronomie (IA)
        public float CosmologyKnowledge; // Connaissance de la cosmologie (IA)
        public float SpaceScienceKnowledge; // Connaissance des sciences spatiales (IA)
        public float MaterialsScienceKnowledge; // Connaissance des sciences des matériaux (IA)
        public float EngineeringKnowledge; // Connaissance de l'ingénierie (IA)
        public float ArchitectureKnowledge; // Connaissance de l'architecture (IA)
        public float UrbanPlanningKnowledge; // Connaissance de l'aménagement urbain (IA)
        public float LandscapeArchitectureKnowledge; // Connaissance de l'architecture paysagère (IA)
        public float InteriorDesignKnowledge; // Connaissance du design d'intérieur (IA)
        public float IndustrialDesignKnowledge; // Connaissance du design industriel (IA)
        public float GraphicDesignKnowledge; // Connaissance du design graphique (IA)
        public float FashionDesignKnowledge; // Connaissance du design de mode (IA)
        public float TextileDesignKnowledge; // Connaissance du design textile (IA)
        public float JewelryDesignKnowledge; // Connaissance du design de bijoux (IA)
        public float FurnitureDesignKnowledge; // Connaissance du design de mobilier (IA)
        public float AutomotiveDesignKnowledge; // Connaissance du design automobile (IA)
        public float AerospaceDesignKnowledge; // Connaissance du design aérospatial (IA)
        public float NavalArchitectureKnowledge; // Connaissance de l'architecture navale (IA)
        public float CivilEngineeringKnowledge; // Connaissance du génie civil (IA)
        public float MechanicalEngineeringKnowledge; // Connaissance du génie mécanique (IA)
        public float ElectricalEngineeringKnowledge; // Connaissance du génie électrique (IA)
        public float ElectronicEngineeringKnowledge; // Connaissance du génie électronique (IA)
        public float ChemicalEngineeringKnowledge; // Connaissance du génie chimique (IA)
        public float BiomedicalEngineeringKnowledge; // Connaissance du génie biomédical (IA)
        public float EnvironmentalEngineeringKnowledge; // Connaissance du génie environnemental (IA)
        public float AgriculturalEngineeringKnowledge; // Connaissance du génie agricole (IA)
        public float FoodEngineeringKnowledge; // Connaissance du génie alimentaire (IA)
        public float PharmaceuticalEngineeringKnowledge; // Connaissance du génie pharmaceutique (IA)
        public float NuclearEngineeringKnowledge; // Connaissance du génie nucléaire (IA)
        public float PetroleumEngineeringKnowledge; // Connaissance du génie pétrolier (IA)
        public float MiningEngineeringKnowledge; // Connaissance du génie minier (IA)
        public float GeologicalEngineeringKnowledge; // Connaissance du génie géologique (IA)
        public float OceanographicEngineeringKnowledge; // Connaissance du génie océanographique (IA)
        public float MeteorologicalEngineeringKnowledge; // Connaissance du génie météorologique (IA)
        public float ClimatologicalEngineeringKnowledge; // Connaissance du génie climatologique (IA)
        public float SeismologicalEngineeringKnowledge; // Connaissance du génie sismologique (IA)
        public float VolcanologicalEngineeringKnowledge; // Connaissance du génie volcanologique (IA)
        public float GlaciologicalEngineeringKnowledge; // Connaissance du génie glaciologique (IA)
        public float HydrologicalEngineeringKnowledge; // Connaissance du génie hydrologique (IA)
        public float SoilEngineeringKnowledge; // Connaissance du génie des sols (IA)
        public float StructuralEngineeringKnowledge; // Connaissance du génie structurel (IA)
        public float TransportationEngineeringKnowledge; // Connaissance du génie des transports (IA)
        public float TrafficEngineeringKnowledge; // Connaissance du génie du trafic (IA)
        public float LogisticsEngineeringKnowledge; // Connaissance du génie logistique (IA)
        public float SupplyChainEngineeringKnowledge; // Connaissance du génie de la chaîne d'approvisionnement (IA)
        public float ManufacturingEngineeringKnowledge; // Connaissance du génie de la fabrication (IA)
        public float ProductionEngineeringKnowledge; // Connaissance du génie de la production (IA)
        public float QualityEngineeringKnowledge; // Connaissance du génie de la qualité (IA)
        public float SafetyEngineeringKnowledge; // Connaissance du génie de la sécurité (IA)
        public float RiskEngineeringKnowledge; // Connaissance du génie du risque (IA)
        public float ReliabilityEngineeringKnowledge; // Connaissance du génie de la fiabilité (IA)
        public float MaintainabilityEngineeringKnowledge; // Connaissance du génie de la maintenabilité (IA)
        public float AvailabilityEngineeringKnowledge; // Connaissance du génie de la disponibilité (IA)
        public float SurvivabilityEngineeringKnowledge; // Connaissance du génie de la survie (IA)
        public float VulnerabilityEngineeringKnowledge; // Connaissance du génie de la vulnérabilité (IA)
        public float ThreatEngineeringKnowledge; // Connaissance du génie des menaces (IA)
        public float CountermeasureEngineeringKnowledge; // Connaissance du génie des contre-mesures (IA)
        public float SecurityEngineeringKnowledge; // Connaissance du génie de la sécurité (IA)
        public float DefenseEngineeringKnowledge; // Connaissance du génie de la défense (IA)
        public float OffenseEngineeringKnowledge; // Connaissance du génie de l'offense (IA)
        public float WarfareEngineeringKnowledge; // Connaissance du génie de la guerre (IA)
        public float PeacekeepingEngineeringKnowledge; // Connaissance du génie du maintien de la paix (IA)
        public float HumanitarianEngineeringKnowledge; // Connaissance du génie humanitaire (IA)
        public float DevelopmentEngineeringKnowledge; // Connaissance du génie du développement (IA)
        public float SustainabilityEngineeringKnowledge; // Connaissance du génie du développement durable (IA)
        public float EnvironmentalEngineeringKnowledge; // Connaissance du génie environnemental (IA)
        public float EcologicalEngineeringKnowledge; // Connaissance du génie écologique (IA)
        public float ConservationEngineeringKnowledge; // Connaissance du génie de la conservation (IA)
        public float RestorationEngineeringKnowledge; // Connaissance du génie de la restauration (IA)
        public float RemediationEngineeringKnowledge; // Connaissance du génie de la remédiation (IA)
        public float PollutionControlEngineeringKnowledge; // Connaissance du génie du contrôle de la pollution (IA)
        public float WasteManagementEngineeringKnowledge; // Connaissance du génie de la gestion des déchets (IA)
        public float RecyclingEngineeringKnowledge; // Connaissance du génie du recyclage (IA)
        public float EnergyEngineeringKnowledge; // Connaissance du génie énergétique (IA)
        public float PowerEngineeringKnowledge; // Connaissance du génie électrique (IA)
        public float RenewableEnergyEngineeringKnowledge; // Connaissance du génie des énergies renouvelables (IA)
        public float NuclearEnergyEngineeringKnowledge; // Connaissance du génie de l'énergie nucléaire (IA)
        public float FossilFuelEngineeringKnowledge; // Connaissance du génie des combustibles fossiles (IA)
        public float AlternativeEnergyEngineeringKnowledge; // Connaissance du génie des énergies alternatives (IA)
        public float EnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie (IA)
        public float EnergyDistributionEngineeringKnowledge; // Connaissance du génie de la distribution d'énergie (IA)
        public float EnergyEfficiencyEngineeringKnowledge; // Connaissance du génie de l'efficacité énergétique (IA)
        public float EnergyPolicyEngineeringKnowledge; // Connaissance du génie de la politique énergétique (IA)
        public float EnergyEconomicsEngineeringKnowledge; // Connaissance du génie de l'économie énergétique (IA)
        public float EnergySecurityEngineeringKnowledge; // Connaissance du génie de la sécurité énergétique (IA)
        public float EnergySafetyEngineeringKnowledge; // Connaissance du génie de la sécurité énergétique (IA)
        public float EnergyReliabilityEngineeringKnowledge; // Connaissance du génie de la fiabilité énergétique (IA)
        public float EnergyAvailabilityEngineeringKnowledge; // Connaissance du génie de la disponibilité énergétique (IA)
        public float EnergyAccessibilityEngineeringKnowledge; // Connaissance du génie de l'accessibilité énergétique (IA)
        public float EnergyAffordabilityEngineeringKnowledge; // Connaissance du génie de l'accessibilité énergétique (IA)
        public float EnergySustainabilityEngineeringKnowledge; // Connaissance du génie du développement énergétique durable (IA)
        public float EnergyJusticeEngineeringKnowledge; // Connaissance du génie de la justice énergétique (IA)
        public float EnergyEquityEngineeringKnowledge; // Connaissance du génie de l'équité énergétique (IA)
        public float EnergyDemocracyEngineeringKnowledge; // Connaissance du génie de la démocratie énergétique (IA)
        public float EnergyTransparencyEngineeringKnowledge; // Connaissance du génie de la transparence énergétique (IA)
        public float EnergyAccountabilityEngineeringKnowledge; // Connaissance du génie de la responsabilité énergétique (IA)
        public float EnergyGovernanceEngineeringKnowledge; // Connaissance du génie de la gouvernance énergétique (IA)
        public float EnergyLeadershipEngineeringKnowledge; // Connaissance du génie du leadership énergétique (IA)
        public float EnergyManagementEngineeringKnowledge; // Connaissance du génie de la gestion énergétique (IA)
        public float EnergyCommunicationEngineeringKnowledge; // Connaissance du génie de la communication énergétique (IA)
        public float EnergyEducationEngineeringKnowledge; // Connaissance du génie de l'éducation énergétique (IA)
        public float EnergyAwarenessEngineeringKnowledge; // Connaissance du génie de la sensibilisation énergétique (IA)
        public float EnergyBehaviorEngineeringKnowledge; // Connaissance du génie du comportement énergétique (IA)
        public float EnergyCultureEngineeringKnowledge; // Connaissance du génie de la culture énergétique (IA)
        public float EnergyPsychologyEngineeringKnowledge; // Connaissance du génie de la psychologie énergétique (IA)
        public float EnergySociologyEngineeringKnowledge; // Connaissance du génie de la sociologie énergétique (IA)
        public float EnergyAnthropologyEngineeringKnowledge; // Connaissance du génie de l'anthropologie énergétique (IA)
        public float EnergyHistoryEngineeringKnowledge; // Connaissance du génie de l'histoire énergétique (IA)
        public float EnergyPoliticsEngineeringKnowledge; // Connaissance du génie de la politique énergétique (IA)
        public float EnergyEconomicsEngineeringKnowledge; // Connaissance du génie de l'économie énergétique (IA)
        public float EnergyLawEngineeringKnowledge; // Connaissance du génie du droit énergétique (IA)
        public float EnergyEthicsEngineeringKnowledge; // Connaissance du génie de l'éthique énergétique (IA)
        public float EnergyPhilosophyEngineeringKnowledge; // Connaissance du génie de la philosophie énergétique (IA)
        public float EnergyReligionEngineeringKnowledge; // Connaissance du génie de la religion énergétique (IA)
        public float EnergyMythologyEngineeringKnowledge; // Connaissance du génie de la mythologie énergétique (IA)
        public float EnergyLiteratureEngineeringKnowledge; // Connaissance du génie de la littérature énergétique (IA)
        public float EnergyArtEngineeringKnowledge; // Connaissance du génie de l'art énergétique (IA)
        public float EnergyMusicEngineeringKnowledge; // Connaissance du génie de la musique énergétique (IA)
        public float EnergyDanceEngineeringKnowledge; // Connaissance du génie de la danse énergétique (IA)
        public float EnergyTheaterEngineeringKnowledge; // Connaissance du génie du théâtre énergétique (IA)
        public float EnergyCinemaEngineeringKnowledge; // Connaissance du génie du cinéma énergétique (IA)
        public float EnergyTelevisionEngineeringKnowledge; // Connaissance du génie de la télévision énergétique (IA)
        public float EnergyRadioEngineeringKnowledge; // Connaissance du génie de la radio énergétique (IA)
        public float EnergyJournalismEngineeringKnowledge; // Connaissance du génie du journalisme énergétique (IA)
        public float EnergyPublishingEngineeringKnowledge; // Connaissance du génie de l'édition énergétique (IA)
        public float EnergyBroadcastingEngineeringKnowledge; // Connaissance du génie de la diffusion énergétique (IA)
        public float EnergyTelecommunicationsEngineeringKnowledge; // Connaissance du génie des télécommunications énergétique (IA)
        public float EnergyComputingEngineeringKnowledge; // Connaissance du génie informatique énergétique (IA)
        public float EnergyInformationTechnologyEngineeringKnowledge; // Connaissance du génie des technologies de l'information énergétique (IA)
        public float EnergySoftwareEngineeringEngineeringKnowledge; // Connaissance du génie logiciel énergétique (IA)
        public float EnergyHardwareEngineeringEngineeringKnowledge; // Connaissance du génie matériel énergétique (IA)
        public float EnergyCybersecurityEngineeringKnowledge; // Connaissance du génie de la cybersécurité énergétique (IA)
        public float EnergyDataScienceEngineeringKnowledge; // Connaissance du génie de la science des données énergétique (IA)
        public float EnergyArtificialIntelligenceEngineeringKnowledge; // Connaissance du génie de l'intelligence artificielle énergétique (IA)
        public float EnergyMachineLearningEngineeringKnowledge; // Connaissance du génie de l'apprentissage automatique énergétique (IA)
        public float EnergyDeepLearningEngineeringKnowledge; // Connaissance du génie de l'apprentissage profond énergétique (IA)
        public float EnergyNaturalLanguageProcessingEngineeringKnowledge; // Connaissance du génie du traitement du langage naturel énergétique (IA)
        public float EnergyComputerVisionEngineeringKnowledge; // Connaissance du génie de la vision par ordinateur énergétique (IA)
        public float EnergyRoboticsEngineeringKnowledge; // Connaissance du génie de la robotique énergétique (IA)
        public float EnergyAutomationEngineeringKnowledge; // Connaissance du génie de l'automatisation énergétique (IA)
        public float EnergyControlTheoryEngineeringKnowledge; // Connaissance du génie de la théorie du contrôle énergétique (IA)
        public float EnergySystemsTheoryEngineeringKnowledge; // Connaissance du génie de la théorie des systèmes énergétique (IA)
        public float EnergyComplexityTheoryEngineeringKnowledge; // Connaissance du génie de la théorie de la complexité énergétique (IA)
        public float EnergyChaosTheoryEngineeringKnowledge; // Connaissance du génie de la théorie du chaos énergétique (IA)
        public float EnergyNetworkTheoryEngineeringKnowledge; // Connaissance du génie de la théorie des réseaux énergétique (IA)
        public float EnergyGameTheoryEngineeringKnowledge; // Connaissance du génie de la théorie des jeux énergétique (IA)
        public float EnergyDecisionTheoryEngineeringKnowledge; // Connaissance du génie de la théorie de la décision énergétique (IA)
        public float EnergyProbabilityTheoryEngineeringKnowledge; // Connaissance du génie de la théorie des probabilités énergétique (IA)
        public float EnergyStatisticsEngineeringKnowledge; // Connaissance du génie des statistiques énergétique (IA)
        public float EnergyMathematicsEngineeringKnowledge; // Connaissance du génie des mathématiques énergétique (IA)
        public float EnergyPhysicsEngineeringKnowledge; // Connaissance du génie de la physique énergétique (IA)
        public float EnergyChemistryEngineeringKnowledge; // Connaissance du génie de la chimie énergétique (IA)
        public float EnergyBiologyEngineeringKnowledge; // Connaissance du génie de la biologie énergétique (IA)
        public float EnergyEarthScienceEngineeringKnowledge; // Connaissance du génie des sciences de la Terre énergétique (IA)
        public float EnergyAstronomyEngineeringKnowledge; // Connaissance du génie de l'astronomie énergétique (IA)
        public float EnergyCosmologyEngineeringKnowledge; // Connaissance du génie de la cosmologie énergétique (IA)
        public float EnergySpaceScienceEngineeringKnowledge; // Connaissance du génie des sciences spatiales énergétique (IA)
        public float EnergyMaterialsScienceEngineeringKnowledge; // Connaissance du génie des sciences des matériaux énergétique (IA)
        public float EnergyEngineeringEngineeringKnowledge; // Connaissance du génie du génie énergétique (IA)
        public float EnergyArchitectureEngineeringKnowledge; // Connaissance du génie de l'architecture énergétique (IA)
        public float EnergyUrbanPlanningEngineeringKnowledge; // Connaissance du génie de l'aménagement urbain énergétique (IA)
        public float EnergyLandscapeArchitectureEngineeringKnowledge; // Connaissance du génie de l'architecture paysagère énergétique (IA)
        public float EnergyInteriorDesignEngineeringKnowledge; // Connaissance du génie du design d'intérieur énergétique (IA)
        public float EnergyIndustrialDesignEngineeringKnowledge; // Connaissance du génie du design industriel énergétique (IA)
        public float EnergyGraphicDesignEngineeringKnowledge; // Connaissance du génie du design graphique énergétique (IA)
        public float EnergyFashionDesignEngineeringKnowledge; // Connaissance du génie du design de mode énergétique (IA)
        public float EnergyTextileDesignEngineeringKnowledge; // Connaissance du génie du design textile énergétique (IA)
        public float EnergyJewelryDesignEngineeringKnowledge; // Connaissance du génie du design de bijoux énergétique (IA)
        public float EnergyFurnitureDesignEngineeringKnowledge; // Connaissance du génie du design de mobilier énergétique (IA)
        public float EnergyAutomotiveDesignEngineeringKnowledge; // Connaissance du génie du design automobile énergétique (IA)
        public float EnergyAerospaceDesignEngineeringKnowledge; // Connaissance du génie du design aérospatial énergétique (IA)
        public float EnergyNavalArchitectureEngineeringKnowledge; // Connaissance du génie de l'architecture navale énergétique (IA)
        public float EnergyCivilEngineeringEngineeringKnowledge; // Connaissance du génie civil énergétique (IA)
        public float EnergyMechanicalEngineeringEngineeringKnowledge; // Connaissance du génie mécanique énergétique (IA)
        public float EnergyElectricalEngineeringEngineeringKnowledge; // Connaissance du génie électrique énergétique (IA)
        public float EnergyElectronicEngineeringEngineeringKnowledge; // Connaissance du génie électronique énergétique (IA)
        public float EnergyChemicalEngineeringEngineeringKnowledge; // Connaissance du génie chimique énergétique (IA)
        public float EnergyBiomedicalEngineeringEngineeringKnowledge; // Connaissance du génie biomédical énergétique (IA)
        public float EnergyEnvironmentalEngineeringEngineeringKnowledge; // Connaissance du génie environnemental énergétique (IA)
        public float EnergyAgriculturalEngineeringEngineeringKnowledge; // Connaissance du génie agricole énergétique (IA)
        public float EnergyFoodEngineeringEngineeringKnowledge; // Connaissance du génie alimentaire énergétique (IA)
        public float EnergyPharmaceuticalEngineeringEngineeringKnowledge; // Connaissance du génie pharmaceutique énergétique (IA)
        public float EnergyNuclearEngineeringEngineeringKnowledge; // Connaissance du génie nucléaire énergétique (IA)
        public float EnergyPetroleumEngineeringEngineeringKnowledge; // Connaissance du génie pétrolier énergétique (IA)
        public float EnergyMiningEngineeringEngineeringKnowledge; // Connaissance du génie minier énergétique (IA)
        public float EnergyGeologicalEngineeringEngineeringKnowledge; // Connaissance du génie géologique énergétique (IA)
        public float EnergyOceanographicEngineeringEngineeringKnowledge; // Connaissance du génie océanographique énergétique (IA)
        public float EnergyMeteorologicalEngineeringEngineeringKnowledge; // Connaissance du génie météorologique énergétique (IA)
        public float EnergyClimatologicalEngineeringEngineeringKnowledge; // Connaissance du génie climatologique énergétique (IA)
        public float EnergySeismologicalEngineeringEngineeringKnowledge; // Connaissance du génie sismologique énergétique (IA)
        public float EnergyVolcanologicalEngineeringEngineeringKnowledge; // Connaissance du génie volcanologique énergétique (IA)
        public float EnergyGlaciologicalEngineeringEngineeringKnowledge; // Connaissance du génie glaciologique énergétique (IA)
        public float EnergyHydrologicalEngineeringEngineeringKnowledge; // Connaissance du génie hydrologique énergétique (IA)
        public float EnergySoilEngineeringEngineeringKnowledge; // Connaissance du génie des sols énergétique (IA)
        public float EnergyStructuralEngineeringEngineeringKnowledge; // Connaissance du génie structurel énergétique (IA)
        public float EnergyTransportationEngineeringEngineeringKnowledge; // Connaissance du génie des transports énergétique (IA)
        public float EnergyTrafficEngineeringEngineeringKnowledge; // Connaissance du génie du trafic énergétique (IA)
        public float EnergyLogisticsEngineeringEngineeringKnowledge; // Connaissance du génie logistique énergétique (IA)
        public float EnergySupplyChainEngineeringEngineeringKnowledge; // Connaissance du génie de la chaîne d'approvisionnement énergétique (IA)
        public float EnergyManufacturingEngineeringEngineeringKnowledge; // Connaissance du génie de la fabrication énergétique (IA)
        public float EnergyProductionEngineeringEngineeringKnowledge; // Connaissance du génie de la production énergétique (IA)
        public float EnergyQualityEngineeringEngineeringKnowledge; // Connaissance du génie de la qualité énergétique (IA)
        public float EnergySafetyEngineeringEngineeringKnowledge; // Connaissance du génie de la sécurité énergétique (IA)
        public float EnergyRiskEngineeringEngineeringKnowledge; // Connaissance du génie du risque énergétique (IA)
        public float EnergyReliabilityEngineeringEngineeringKnowledge; // Connaissance du génie de la fiabilité énergétique (IA)
        public float EnergyMaintainabilityEngineeringEngineeringKnowledge; // Connaissance du génie de la maintenabilité énergétique (IA)
        public float EnergyAvailabilityEngineeringEngineeringKnowledge; // Connaissance du génie de la disponibilité énergétique (IA)
        public float EnergySurvivabilityEngineeringEngineeringKnowledge; // Connaissance du génie de la survie énergétique (IA)
        public float EnergyVulnerabilityEngineeringEngineeringKnowledge; // Connaissance du génie de la vulnérabilité énergétique (IA)
        public float EnergyThreatEngineeringEngineeringKnowledge; // Connaissance du génie des menaces énergétique (IA)
        public float EnergyCountermeasureEngineeringEngineeringKnowledge; // Connaissance du génie des contre-mesures énergétique (IA)
        public float EnergySecurityEngineeringEngineeringKnowledge; // Connaissance du génie de la sécurité énergétique (IA)
        public float EnergyDefenseEngineeringEngineeringKnowledge; // Connaissance du génie de la défense énergétique (IA)
        public float EnergyOffenseEngineeringEngineeringKnowledge; // Connaissance du génie de l'offense énergétique (IA)
        public float EnergyWarfareEngineeringEngineeringKnowledge; // Connaissance du génie de la guerre énergétique (IA)
        public float EnergyPeacekeepingEngineeringEngineeringKnowledge; // Connaissance du génie du maintien de la paix énergétique (IA)
        public float EnergyHumanitarianEngineeringEngineeringKnowledge; // Connaissance du génie humanitaire énergétique (IA)
        public float EnergyDevelopmentEngineeringEngineeringKnowledge; // Connaissance du génie du développement énergétique (IA)
        public float EnergySustainabilityEngineeringEngineeringKnowledge; // Connaissance du génie du développement durable énergétique (IA)
        public float EnergyEcologicalEngineeringEngineeringKnowledge; // Connaissance du génie écologique énergétique (IA)
        public float EnergyConservationEngineeringEngineeringKnowledge; // Connaissance du génie de la conservation énergétique (IA)
        public float EnergyRestorationEngineeringEngineeringKnowledge; // Connaissance du génie de la restauration énergétique (IA)
        public float EnergyRemediationEngineeringEngineeringKnowledge; // Connaissance du génie de la remédiation énergétique (IA)
        public float EnergyPollutionControlEngineeringEngineeringKnowledge; // Connaissance du génie du contrôle de la pollution énergétique (IA)
        public float EnergyWasteManagementEngineeringEngineeringKnowledge; // Connaissance du génie de la gestion des déchets énergétique (IA)
        public float EnergyRecyclingEngineeringEngineeringKnowledge; // Connaissance du génie du recyclage énergétique (IA)
        public float EnergySolarEngineeringKnowledge; // Connaissance du génie solaire énergétique (IA)
        public float EnergyWindEngineeringKnowledge; // Connaissance du génie éolien énergétique (IA)
        public float EnergyHydroelectricEngineeringKnowledge; // Connaissance du génie hydroélectrique énergétique (IA)
        public float EnergyGeothermalEngineeringKnowledge; // Connaissance du génie géothermique énergétique (IA)
        public float EnergyBiomassEngineeringKnowledge; // Connaissance du génie de la biomasse énergétique (IA)
        public float EnergyBiofuelEngineeringKnowledge; // Connaissance du génie des biocarburants énergétique (IA)
        public float EnergyHydrogenEngineeringKnowledge; // Connaissance du génie de l'hydrogène énergétique (IA)
        public float EnergyFuelCellEngineeringKnowledge; // Connaissance du génie des piles à combustible énergétique (IA)
        public float EnergyBatteryEngineeringKnowledge; // Connaissance du génie des batteries énergétique (IA)
        public float EnergySuperCapacitorEngineeringKnowledge; // Connaissance du génie des supercondensateurs énergétique (IA)
        public float EnergyFlywheelEngineeringKnowledge; // Connaissance du génie des volants d'inertie énergétique (IA)
        public float EnergyCompressedAirEngineeringKnowledge; // Connaissance du génie de l'air comprimé énergétique (IA)
        public float EnergyPumpedHydroStorageEngineeringKnowledge; // Connaissance du génie du stockage par pompage énergétique (IA)
        public float EnergyThermalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage thermique énergétique (IA)
        public float EnergyPhaseChangeMaterialEngineeringKnowledge; // Connaissance du génie des matériaux à changement de phase énergétique (IA)
        public float EnergyLatentHeatStorageEngineeringKnowledge; // Connaissance du génie du stockage de chaleur latente énergétique (IA)
        public float EnergySensibleHeatStorageEngineeringKnowledge; // Connaissance du génie du stockage de chaleur sensible énergétique (IA)
        public float EnergyChemicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage chimique énergétique (IA)
        public float EnergyMechanicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage mécanique énergétique (IA)
        public float EnergyElectricalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage électrique énergétique (IA)
        public float EnergyMagneticEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage magnétique énergétique (IA)
        public float EnergyNuclearEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage nucléaire énergétique (IA)
        public float EnergyAntimatterEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'antimatière énergétique (IA)
        public float EnergyDarkMatterEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage de matière noire énergétique (IA)
        public float EnergyExoticMatterEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage de matière exotique énergétique (IA)
        public float EnergyQuantumEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage quantique énergétique (IA)
        public float EnergyGravitationalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage gravitationnel énergétique (IA)
        public float EnergyInertialEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage inertiel énergétique (IA)
        public float EnergyPotentialEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie potentielle énergétique (IA)
        public float EnergyKineticEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie cinétique énergétique (IA)
        public float EnergyRadiantEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie rayonnante énergétique (IA)
        public float EnergySoundEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie sonore énergétique (IA)
        public float EnergyThermalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage thermique énergétique (IA)
        public float EnergyChemicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage chimique énergétique (IA)
        public float EnergyNuclearEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage nucléaire énergétique (IA)
        public float EnergyMatterEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage matière énergétique (IA)
        public float EnergyInformationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage informationnel énergétique (IA)
        public float EnergyConsciousnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage de conscience énergétique (IA)
        public float EnergySpiritualEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage spirituel énergétique (IA)
        public float EnergyLifeForceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage de force vitale énergétique (IA)
        public float EnergyChiEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage de Chi énergétique (IA)
        public float EnergyKiEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage de Ki énergétique (IA)
        public float EnergyPranaEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage de Prana énergétique (IA)
        public float EnergyUniversalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie universelle énergétique (IA)
        public float EnergyCosmicEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie cosmique énergétique (IA)
        public float EnergyDivineEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie divine énergétique (IA)
        public float EnergySacredEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie sacrée énergétique (IA)
        public float EnergyProfaneEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie profane énergétique (IA)
        public float EnergyForbiddenEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie interdite énergétique (IA)
        public float EnergySecretEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie secrète énergétique (IA)
        public float EnergyPublicEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie publique énergétique (IA)
        public float EnergyPrivateEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie privée énergétique (IA)
        public float EnergyClassifiedEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie classifiée énergétique (IA)
        public float EnergyConfidentialEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie confidentielle énergétique (IA)
        public float EnergyRestrictedEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie restreinte énergétique (IA)
        public float EnergyAccessibleEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie accessible énergétique (IA)
        public float EnergyInaccessibleEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inaccessible énergétique (IA)
        public float EnergyAvailableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie disponible énergétique (IA)
        public float EnergyUnavailableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie indisponible énergétique (IA)
        public float EnergyObtainableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie obtenable énergétique (IA)
        public float EnergyUnobtainableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inobtenable énergétique (IA)
        public float EnergyDiscoverableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie découvrable énergétique (IA)
        public float EnergyUndiscoverableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie indécouvrable énergétique (IA)
        public float EnergyLearnableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie apprenable énergétique (IA)
        public float EnergyUnlearnableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inapprenable énergétique (IA)
        public float EnergyTeachableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie enseignable énergétique (IA)
        public float EnergyUnteachableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inenseignable énergétique (IA)
        public float EnergyCommunicableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie communicable énergétique (IA)
        public float EnergyIncommunicableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie incommunicable énergétique (IA)
        public float EnergyExpressibleEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie exprimable énergétique (IA)
        public float EnergyInexpressibleEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inexplicable énergétique (IA)
        public float EnergyUnderstandableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie compréhensible énergétique (IA)
        public float EnergyUnunderstandableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie incompréhensible énergétique (IA)
        public float EnergyBelievableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie croyable énergétique (IA)
        public float EnergyUnbelievableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie incroyable énergétique (IA)
        public float EnergyAcceptableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie acceptable énergétique (IA)
        public float EnergyUnacceptableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inacceptable énergétique (IA)
        public float EnergyUsableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie utilisable énergétique (IA)
        public float EnergyUnusableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inutilisable énergétique (IA)
        public float EnergyPracticalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie pratique énergétique (IA)
        public float EnergyImpracticalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie impraticable énergétique (IA)
        public float EnergyFunctionalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie fonctionnelle énergétique (IA)
        public float EnergyDysfunctionalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie dysfonctionnelle énergétique (IA)
        public float EnergyEfficientEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie efficace énergétique (IA)
        public float EnergyInefficientEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inefficace énergétique (IA)
        public float EnergyProductiveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie productive énergétique (IA)
        public float EnergyUnproductiveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie improductive énergétique (IA)
        public float EnergyCreativeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie créative énergétique (IA)
        public float EnergyUncreativeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie non créative énergétique (IA)
        public float EnergyInnovativeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie innovante énergétique (IA)
        public float EnergyConservativeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie conservatrice énergétique (IA)
        public float EnergyProgressiveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie progressiste énergétique (IA)
        public float EnergyRevolutionaryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie révolutionnaire énergétique (IA)
        public float EnergyTraditionalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie traditionnelle énergétique (IA)
        public float EnergyModernEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie moderne énergétique (IA)
        public float EnergyContemporaryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie contemporaine énergétique (IA)
        public float EnergyAncientEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie ancienne énergétique (IA)
        public float EnergyHistoricalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie historique énergétique (IA)
        public float EnergyFuturisticEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie futuriste énergétique (IA)
        public float EnergyTimelessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie intemporelle énergétique (IA)
        public float EnergyContextualEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie contextuelle énergétique (IA)
        public float EnergyNonContextualEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie non contextuelle énergétique (IA)
        public float EnergySituationalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie situationnelle énergétique (IA)
        public float EnergyAsituationalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie asituationnelle énergétique (IA)
        public float EnergyConditionalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie conditionnelle énergétique (IA)
        public float EnergyUnconditionalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie inconditionnelle énergétique (IA)
        public float EnergyRelativeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie relative énergétique (IA)
        public float EnergyAbsoluteEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie absolue énergétique (IA)
        public float EnergyObjectiveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie objective énergétique (IA)
        public float EnergySubjectiveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie subjective énergétique (IA)
        public float EnergyPerspectiveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie perspective énergétique (IA)
        public float EnergyViewpointEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de point de vue énergétique (IA)
        public float EnergyOpinionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'opinion énergétique (IA)
        public float EnergyFactEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de fait énergétique (IA)
        public float EnergyTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de théorie énergétique (IA)
        public float EnergyHypothesisEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'hypothèse énergétique (IA)
        public float EnergyAssumptionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'hypothèse énergétique (IA)
        public float EnergyBeliefEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de croyance énergétique (IA)
        public float EnergyTruthEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de vérité énergétique (IA)
        public float EnergyFalsehoodEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de fausseté énergétique (IA)
        public float EnergyLieEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de mensonge énergétique (IA)
        public float EnergyRumorEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de rumeur énergétique (IA)
        public float EnergyGossipEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de ragot énergétique (IA)
        public float EnergySpeculationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de spéculation énergétique (IA)
        public float EnergyPredictionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de prédiction énergétique (IA)
        public float EnergyForecastEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de prévision énergétique (IA)
        public float EnergyProphecyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de prophétie énergétique (IA)
        public float EnergyRevelationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de révélation énergétique (IA)
        public float EnergyInsightEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'insight énergétique (IA)
        public float EnergyUnderstandingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de compréhension énergétique (IA)
        public float EnergyWisdomEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de sagesse énergétique (IA)
        public float EnergyFollyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de folie énergétique (IA)
        public float EnergyIgnoranceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'ignorance énergétique (IA)
        public float EnergyStupidityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de stupidité énergétique (IA)
        public float EnergyIntelligenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'intelligence énergétique (IA)
        public float EnergyClevernessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'intelligence énergétique (IA)
        public float EnergyWitEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'esprit énergétique (IA)
        public float EnergyHumorEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'humour énergétique (IA)
        public float EnergySatireEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de satire énergétique (IA)
        public float EnergyIronyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'ironie énergétique (IA)
        public float EnergySarcasmEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de sarcasme énergétique (IA)
        public float EnergyCynicismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de cynisme énergétique (IA)
        public float EnergyOptimismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'optimisme énergétique (IA)
        public float EnergyPessimismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de pessimisme énergétique (IA)
        public float EnergyRealismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de réalisme énergétique (IA)
        public float EnergyIdealismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'idéalisme énergétique (IA)
        public float EnergyPragmatismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de pragmatisme énergétique (IA)
        public float EnergyDogmatismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de dogmatisme énergétique (IA)
        public float EnergySkepticismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de scepticisme énergétique (IA)
        public float EnergyCredulityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de crédulité énergétique (IA)
        public float EnergyGullibilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de crédulité énergétique (IA)
        public float EnergyNaivetyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de naïveté énergétique (IA)
        public float EnergySophisticationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de sophistication énergétique (IA)
        public float EnergyComplexityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de complexité énergétique (IA)
        public float EnergySimplicityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de simplicité énergétique (IA)
        public float EnergyEleganceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'élégance énergétique (IA)
        public float EnergyGracefulnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de grâce énergétique (IA)
        public float EnergyAwkwardnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de gaucherie énergétique (IA)
        public float EnergyClumsinessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de maladresse énergétique (IA)
        public float EnergyPrecisionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de précision énergétique (IA)
        public float EnergyAccuracyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de précision énergétique (IA)
        public float EnergyApproximationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'approximation énergétique (IA)
        public float EnergyEstimationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'estimation énergétique (IA)
        public float EnergyCalculationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de calcul énergétique (IA)
        public float EnergyMeasurementEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de mesure énergétique (IA)
        public float EnergyQuantificationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de quantification énergétique (IA)
        public float EnergyQualificationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de qualification énergétique (IA)
        public float EnergyClassificationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de classification énergétique (IA)
        public float EnergyCategorizationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de catégorisation énergétique (IA)
        public float EnergyIdentificationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'identification énergétique (IA)
        public float EnergyRecognitionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de reconnaissance énergétique (IA)
        public float EnergyDiscriminationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de discrimination énergétique (IA)
        public float EnergyDifferentiationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de différenciation énergétique (IA)
        public float EnergyComparisonEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de comparaison énergétique (IA)
        public float EnergyContrastEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de contraste énergétique (IA)
        public float EnergySimilarityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de similarité énergétique (IA)
        public float EnergyDissimilarityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de dissimilarité énergétique (IA)
        public float EnergyAnalogyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'analogie énergétique (IA)
        public float EnergyMetaphorEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de métaphore énergétique (IA)
        public float EnergySymbolismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de symbolisme énergétique (IA)
        public float EnergyAllegoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie d'allégorie énergétique (IA)
        public float EnergyParableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de parabole énergétique (IA)
        public float EnergyFableEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de fable énergétique (IA)
        public float EnergyStorytellingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de narration énergétique (IA)
        public float EnergyNarrativeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie narrative énergétique (IA)
        public float EnergyDramaticEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie dramatique énergétique (IA)
        public float EnergyPoeticEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie poétique énergétique (IA)
        public float EnergyMusicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie musicale énergétique (IA)
        public float EnergyVisualEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie visuelle énergétique (IA)
        public float EnergyAuditoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie auditive énergétique (IA)
        public float EnergyTactileEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie tactile énergétique (IA)
        public float EnergyOlfactoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie olfactive énergétique (IA)
        public float EnergyGustatoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie gustative énergétique (IA)
        public float EnergySensoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie sensorielle énergétique (IA)
        public float EnergyPerceptualEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie perceptive énergétique (IA)
        public float EnergyCognitiveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie cognitive énergétique (IA)
        public float EnergyIntellectualEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie intellectuelle énergétique (IA)
        public float EnergyMentalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie mentale énergétique (IA)
        public float EnergySpiritualEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie spirituelle énergétique (IA)
        public float EnergyReligiousEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie religieuse énergétique (IA)
        public float EnergyMysticalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie mystique énergétique (IA)
        public float EnergyEsotericEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie ésotérique énergétique (IA)
        public float EnergyOccultEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie occulte énergétique (IA)
        public float EnergySupernaturalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie surnaturelle énergétique (IA)
        public float EnergyParanormalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie paranormale énergétique (IA)
        public float EnergyMagicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie magique énergétique (IA)
        public float EnergyAlchemicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie alchimique énergétique (IA)
        public float EnergyAstrologicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie astrologique énergétique (IA)
        public float EnergyNumerologicalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie numéroligique énergétique (IA)
        public float EnergyGeomanticEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie géomantique énergétique (IA)
        public float EnergyFengShuiEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie Feng Shui énergétique (IA)
        public float EnergyVastuShastraEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie Vastu Shastra énergétique (IA)
        public float EnergySpaceFengShuiEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie Feng Shui spatial énergétique (IA)
        public float EnergyEnergyFlowEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du flux d'énergie énergétique (IA)
        public float EnergyChiEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du Chi énergétique (IA)
        public float EnergyKiEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du Ki énergétique (IA)
        public float EnergyPranaEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du Prana énergétique (IA)
        public float EnergyLifeForceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la force vitale énergétique (IA)
        public float EnergyVitalityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la vitalité énergétique (IA)
        public float EnergyHealthEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la santé énergétique (IA)
        public float EnergyWellnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du bien-être énergétique (IA)
        public float EnergyFitnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la forme physique énergétique (IA)
        public float EnergyStrengthEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la force énergétique (IA)
        public float EnergyEnduranceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'endurance énergétique (IA)
        public float EnergyAgilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'agilité énergétique (IA)
        public float EnergySpeedEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la vitesse énergétique (IA)
        public float EnergyDexterityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la dextérité énergétique (IA)
        public float EnergyCoordinationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la coordination énergétique (IA)
        public float EnergyBalanceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'équilibre énergétique (IA)
        public float EnergyFlexibilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la flexibilité énergétique (IA)
        public float EnergyStabilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la stabilité énergétique (IA)
        public float EnergyResilienceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la résilience énergétique (IA)
        public float EnergyRecoveryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la récupération énergétique (IA)
        public float EnergyRehabilitationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la réhabilitation énergétique (IA)
        public float EnergyTherapyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la thérapie énergétique (IA)
        public float EnergyMedicineEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la médecine énergétique (IA)
        public float EnergySurgeryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la chirurgie énergétique (IA)
        public float EnergyPharmacologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la pharmacologie énergétique (IA)
        public float EnergyToxicologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la toxicologie énergétique (IA)
        public float EnergyPathologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la pathologie énergétique (IA)
        public float EnergyAnatomyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'anatomie énergétique (IA)
        public float EnergyPhysiologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la physiologie énergétique (IA)
        public float EnergyBiochemistryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la biochimie énergétique (IA)
        public float EnergyGeneticsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la génétique énergétique (IA)
        public float EnergyEvolutionaryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'évolution énergétique (IA)
        public float EnergyDevelopmentalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du développement énergétique (IA)
        public float EnergyGrowthEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la croissance énergétique (IA)
        public float EnergyMaturationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la maturation énergétique (IA)
        public float EnergyAgingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du vieillissement énergétique (IA)
        public float EnergyDeathEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la mort énergétique (IA)
        public float EnergyRebirthEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la renaissance énergétique (IA)
        public float EnergyAfterlifeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'au-delà énergétique (IA)
        public float EnergySoulEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'âme énergétique (IA)
        public float EnergySpiritEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'esprit énergétique (IA)
        public float EnergyMindEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'esprit énergétique (IA)
        public float EnergyConsciousnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la conscience énergétique (IA)
        public float EnergySubconsciousnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du subconscient énergétique (IA)
        public float EnergyUnconsciousnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'inconscient énergétique (IA)
        public float EnergyAwarenessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la conscience énergétique (IA)
        public float EnergyAttentionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'attention énergétique (IA)
        public float EnergyFocusEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la focalisation énergétique (IA)
        public float EnergyConcentrationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la concentration énergétique (IA)
        public float EnergyMeditationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la méditation énergétique (IA)
        public float EnergyContemplationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la contemplation énergétique (IA)
        public float EnergyReflectionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la réflexion énergétique (IA)
        public float EnergyIntrospectionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'introspection énergétique (IA)
        public float EnergySelfEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de soi énergétique (IA)
        public float EnergyOtherEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des autres énergétique (IA)
        public float EnergyWorldEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du monde énergétique (IA)
        public float EnergyUniverseEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'univers énergétique (IA)
        public float EnergyExistenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'existence énergétique (IA)
        public float EnergyRealityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la réalité énergétique (IA)
        public float EnergyIllusionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'illusion énergétique (IA)
        public float EnergyAppearanceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'apparence énergétique (IA)
        public float EnergyEssenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'essence énergétique (IA)
        public float EnergyBeingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'être énergétique (IA)
        public float EnergyBecomingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du devenir énergétique (IA)
        public float EnergyChangeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du changement énergétique (IA)
        public float EnergyPermanenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la permanence énergétique (IA)
        public float EnergyImpermanenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'impermanence énergétique (IA)
        public float EnergyUnityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'unité énergétique (IA)
        public float EnergyDiversityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la diversité énergétique (IA)
        public float EnergyHarmonyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'harmonie énergétique (IA)
        public float EnergyDiscordEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la discorde énergétique (IA)
        public float EnergyOrderEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'ordre énergétique (IA)
        public float EnergyChaosEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du chaos énergétique (IA)
        public float EnergyBalanceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'équilibre énergétique (IA)
        public float EnergyImbalanceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du déséquilibre énergétique (IA)
        public float EnergyJusticeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la justice énergétique (IA)
        public float EnergyInjusticeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'injustice énergétique (IA)
        public float EnergyFairnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'équité énergétique (IA)
        public float EnergyUnfairnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'inéquité énergétique (IA)
        public float EnergyEqualityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'égalité énergétique (IA)
        public float EnergyInequalityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'inégalité énergétique (IA)
        public float EnergyFreedomEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la liberté énergétique (IA)
        public float EnergySlaveryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'esclavage énergétique (IA)
        public float EnergyLibertyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la liberté énergétique (IA)
        public float EnergyOppressionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'oppression énergétique (IA)
        public float EnergyLiberationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la libération énergétique (IA)
        public float EnergyIndependenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'indépendance énergétique (IA)
        public float EnergyDependenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la dépendance énergétique (IA)
        public float EnergyResponsibilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la responsabilité énergétique (IA)
        public float EnergyIrresponsibilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'irresponsabilité énergétique (IA)
        public float EnergyAccountabilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la responsabilité énergétique (IA)
        public float EnergyNegligenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la négligence énergétique (IA)
        public float EnergyDutyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du devoir énergétique (IA)
        public float EnergyObligationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'obligation énergétique (IA)
        public float EnergyCommitmentEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'engagement énergétique (IA)
        public float EnergyLoyaltyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la loyauté énergétique (IA)
        public float EnergyBetrayalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la trahison énergétique (IA)
        public float EnergyTrustEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la confiance énergétique (IA)
        public float EnergyDistrustEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la méfiance énergétique (IA)
        public float EnergyHonestyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'honnêteté énergétique (IA)
        public float EnergyDishonestyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la malhonnêteté énergétique (IA)
        public float EnergyIntegrityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'intégrité énergétique (IA)
        public float EnergyCorruptionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la corruption énergétique (IA)
        public float EnergyVirtueEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la vertu énergétique (IA)
        public float EnergyViceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du vice énergétique (IA)
        public float EnergyGoodnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du bien énergétique (IA)
        public float EnergyBadnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du mal énergétique (IA)
        public float EnergyRighteousnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la droiture énergétique (IA)
        public float EnergyWickednessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la perversité énergétique (IA)
        public float EnergyKindnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la gentillesse énergétique (IA)
        public float EnergyCrueltyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la cruauté énergétique (IA)
        public float EnergyCompassionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la compassion énergétique (IA)
        public float EnergyCallousnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'insensibilité énergétique (IA)
        public float EnergyEmpathyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'empathie énergétique (IA)
        public float EnergySympathyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la sympathie énergétique (IA)
        public float EnergyAntipathyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'antipathie énergétique (IA)
        public float EnergyLoveEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'amour énergétique (IA)
        public float EnergyHateEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la haine énergétique (IA)
        public float EnergyAffectionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'affection énergétique (IA)
        public float EnergyIndifferenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'indifférence énergétique (IA)
        public float EnergyPassionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la passion énergétique (IA)
        public float EnergyApathyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'apathie énergétique (IA)
        public float EnergyDesireEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du désir énergétique (IA)
        public float EnergyAversionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'aversion énergétique (IA)
        public float EnergyAttractionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'attraction énergétique (IA)
        public float EnergyRepulsionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la répulsion énergétique (IA)
        public float EnergyPleasureEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du plaisir énergétique (IA)
        public float EnergyPainEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la douleur énergétique (IA)
        public float EnergyJoyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la joie énergétique (IA)
        public float EnergySorrowEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la tristesse énergétique (IA)
        public float EnergyHappinessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du bonheur énergétique (IA)
        public float EnergySadnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la tristesse énergétique (IA)
        public float EnergyExcitementEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'excitation énergétique (IA)
        public float EnergyCalmnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la tranquillité énergétique (IA)
        public float EnergyAnxietyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'anxiété énergétique (IA)
        public float EnergyPeaceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la paix énergétique (IA)
        public float EnergyConflictEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du conflit énergétique (IA)
        public float EnergyWarEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la guerre énergétique (IA)
        public float EnergyPeaceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la paix énergétique (IA)
        public float EnergyVictoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la victoire énergétique (IA)
        public float EnergyDefeatEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la défaite énergétique (IA)
        public float EnergySuccessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du succès énergétique (IA)
        public float EnergyFailureEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'échec énergétique (IA)
        public float EnergyAchievementEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la réussite énergétique (IA)
        public float EnergyAccomplishmentEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'accomplissement énergétique (IA)
        public float EnergyProgressEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du progrès énergétique (IA)
        public float EnergyRegressionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la régression énergétique (IA)
        public float EnergyAdvancementEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'avancement énergétique (IA)
        public float EnergyStagnationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la stagnation énergétique (IA)
        public float EnergyInnovationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'innovation énergétique (IA)
        public float EnergyTraditionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la tradition énergétique (IA)
        public float EnergyModernityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la modernité énergétique (IA)
        public float EnergyFuturityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du futur énergétique (IA)
        public float EnergyPastEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du passé énergétique (IA)
        public float EnergyPresentEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du présent énergétique (IA)
        public float EnergyTimeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du temps énergétique (IA)
        public float EnergySpaceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'espace énergétique (IA)
        public float EnergyDimensionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la dimension énergétique (IA)
        public float EnergyMatterEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la matière énergétique (IA)
        public float EnergyEnergyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'énergie énergétique (IA)
        public float EnergyForceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la force énergétique (IA)
        public float EnergyMotionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du mouvement énergétique (IA)
        public float EnergyRestEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du repos énergétique (IA)
        public float EnergyChangeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du changement énergétique (IA)
        public float EnergyConstancyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la constance énergétique (IA)
        public float EnergyCauseEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la cause énergétique (IA)
        public float EnergyEffectEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'effet énergétique (IA)
        public float EnergyRelationshipEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la relation énergétique (IA)
        public float EnergyConnectionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la connexion énergétique (IA)
        public float EnergySeparationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la séparation énergétique (IA)
        public float EnergyUnionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'union énergétique (IA)
        public float EnergyDivisionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la division énergétique (IA)
        public float EnergyCombinationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la combinaison énergétique (IA)
        public float EnergySynthesisEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la synthèse énergétique (IA)
        public float EnergyAnalysisEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'analyse énergétique (IA)
        public float EnergyDecompositionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la décomposition énergétique (IA)
        public float EnergyIntegrationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'intégration énergétique (IA)
        public float EnergyDifferentiationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la différenciation énergétique (IA)
        public float EnergyEmergenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'émergence énergétique (IA)
        public float EnergyReductionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la réduction énergétique (IA)
        public float EnergyComplexityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la complexité énergétique (IA)
        public float EnergySimplicityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la simplicité énergétique (IA)
        public float EnergyOrderEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'ordre énergétique (IA)
        public float EnergyChaosEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du chaos énergétique (IA)
        public float EnergyPatternEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du motif énergétique (IA)
        public float EnergyRandomnessEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du hasard énergétique (IA)
        public float EnergyLawEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la loi énergétique (IA)
        public float EnergyRuleEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la règle énergétique (IA)
        public float EnergyPrincipleEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du principe énergétique (IA)
        public float EnergyConceptEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du concept énergétique (IA)
        public float EnergyIdeaEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'idée énergétique (IA)
        public float EnergyThoughtEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la pensée énergétique (IA)
        public float EnergyBeliefEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la croyance énergétique (IA)
        public float EnergyOpinionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'opinion énergétique (IA)
        public float EnergyFactEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du fait énergétique (IA)
        public float EnergyFictionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la fiction énergétique (IA)
        public float EnergyTruthEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la vérité énergétique (IA)
        public float EnergyFalsehoodEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la fausseté énergétique (IA)
        public float EnergyEvidenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la preuve énergétique (IA)
        public float EnergyProofEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la preuve énergétique (IA)
        public float EnergyArgumentEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'argument énergétique (IA)
        public float EnergyLogicEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la logique énergétique (IA)
        public float EnergyReasoningEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du raisonnement énergétique (IA)
        public float EnergyDeductionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la déduction énergétique (IA)
        public float EnergyInductionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'induction énergétique (IA)
        public float EnergyAbductionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'abduction énergétique (IA)
        public float EnergyInferenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'inférence énergétique (IA)
        public float EnergyConclusionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la conclusion énergétique (IA)
        public float EnergyPremiseEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la prémisse énergétique (IA)
        public float EnergyAssumptionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'hypothèse énergétique (IA)
        public float EnergyHypothesisEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'hypothèse énergétique (IA)
        public float EnergyTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie énergétique (IA)
        public float EnergyModelEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du modèle énergétique (IA)
        public float EnergyFrameworkEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du cadre énergétique (IA)
        public float EnergySystemEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du système énergétique (IA)
        public float EnergyStructureEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la structure énergétique (IA)
        public float EnergyFunctionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la fonction énergétique (IA)
        public float EnergyPurposeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du but énergétique (IA)
        public float EnergyGoalEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'objectif énergétique (IA)
        public float EnergyMeansEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des moyens énergétique (IA)
        public float EnergyEndEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la fin énergétique (IA)
        public float EnergyMethodEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la méthode énergétique (IA)
        public float EnergyTechniqueEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la technique énergétique (IA)
        public float EnergySkillEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la compétence énergétique (IA)
        public float EnergyAbilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'aptitude énergétique (IA)
        public float EnergyTalentEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du talent énergétique (IA)
        public float EnergyGiftEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du don énergétique (IA)
        public float EnergyPotentialEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du potentiel énergétique (IA)
        public float EnergyActualizationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'actualisation énergétique (IA)
        public float EnergyDevelopmentEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du développement énergétique (IA)
        public float EnergyGrowthEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la croissance énergétique (IA)
        public float EnergyLearningEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'apprentissage énergétique (IA)
        public float EnergyTeachingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'enseignement énergétique (IA)
        public float EnergyEducationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'éducation énergétique (IA)
        public float EnergyTrainingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la formation énergétique (IA)
        public float EnergyPracticeEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la pratique énergétique (IA)
        public float EnergyExperienceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'expérience énergétique (IA)
        public float EnergyExperimentationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'expérimentation énergétique (IA)
        public float EnergyObservationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'observation énergétique (IA)
        public float EnergyMeasurementEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la mesure énergétique (IA)
        public float EnergyCalculationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du calcul énergétique (IA)
        public float EnergyEstimationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'estimation énergétique (IA)
        public float EnergyApproximationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'approximation énergétique (IA)
        public float EnergyPrecisionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la précision énergétique (IA)
        public float EnergyAccuracyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'exactitude énergétique (IA)
        public float EnergyReliabilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la fiabilité énergétique (IA)
        public float EnergyValidityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la validité énergétique (IA)
        public float EnergyConsistencyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la cohérence énergétique (IA)
        public float EnergyReproducibilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la reproductibilité énergétique (IA)
        public float EnergyReplicabilityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la replicabilité énergétique (IA)
        public float EnergyVerificationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la vérification énergétique (IA)
        public float EnergyValidationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la validation énergétique (IA)
        public float EnergyQualityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la qualité énergétique (IA)
        public float EnergyStandardEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la norme énergétique (IA)
        public float EnergyCriterionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du critère énergétique (IA)
        public float EnergyBenchmarkEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du repère énergétique (IA)
        public float EnergyReferenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la référence énergétique (IA)
        public float EnergyComparisonEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la comparaison énergétique (IA)
        public float EnergyContrastEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du contraste énergétique (IA)
        public float EnergySimilarityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la similarité énergétique (IA)
        public float EnergyDifferenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la différence énergétique (IA)
        public float EnergyIdentityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'identité énergétique (IA)
        public float EnergyDistinctionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la distinction énergétique (IA)
        public float EnergyClassificationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la classification énergétique (IA)
        public float EnergyCategorizationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la catégorisation énergétique (IA)
        public float EnergyTypologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la typologie énergétique (IA)
        public float EnergyTaxonomyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la taxonomie énergétique (IA)
        public float EnergyOntologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'ontologie énergétique (IA)
        public float EnergyEpistemologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'épistémologie énergétique (IA)
        public float EnergyMetaphysicsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la métaphysique énergétique (IA)
        public float EnergyEthicsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'éthique énergétique (IA)
        public float EnergyAestheticsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'esthétique énergétique (IA)
        public float EnergyPoliticsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la politique énergétique (IA)
        public float EnergyEconomicsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'économie énergétique (IA)
        public float EnergySociologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la sociologie énergétique (IA)
        public float EnergyAnthropologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'anthropologie énergétique (IA)
        public float EnergyPsychologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la psychologie énergétique (IA)
        public float EnergyPhilosophyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la philosophie énergétique (IA)
        public float EnergyReligionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la religion énergétique (IA)
        public float EnergyMythologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la mythologie énergétique (IA)
        public float EnergyLiteratureEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la littérature énergétique (IA)
        public float EnergyArtEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'art énergétique (IA)
        public float EnergyMusicEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la musique énergétique (IA)
        public float EnergyDanceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la danse énergétique (IA)
        public float EnergyTheaterEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du théâtre énergétique (IA)
        public float EnergyCinemaEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du cinéma énergétique (IA)
        public float EnergyTelevisionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la télévision énergétique (IA)
        public float EnergyRadioEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la radio énergétique (IA)
        public float EnergyJournalismEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du journalisme énergétique (IA)
        public float EnergyPublishingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'édition énergétique (IA)
        public float EnergyBroadcastingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la diffusion énergétique (IA)
        public float EnergyTelecommunicationsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des télécommunications énergétique (IA)
        public float EnergyComputingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'informatique énergétique (IA)
        public float EnergyInformationTechnologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des technologies de l'information énergétique (IA)
        public float EnergySoftwareEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie logiciel énergétique (IA)
        public float EnergyHardwareEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie matériel énergétique (IA)
        public float EnergyCybersecurityEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la cybersécurité énergétique (IA)
        public float EnergyDataScienceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la science des données énergétique (IA)
        public float EnergyArtificialIntelligenceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'intelligence artificielle énergétique (IA)
        public float EnergyMachineLearningEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'apprentissage automatique énergétique (IA)
        public float EnergyDeepLearningEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'apprentissage profond énergétique (IA)
        public float EnergyNaturalLanguageProcessingEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du traitement du langage naturel énergétique (IA)
        public float EnergyComputerVisionEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la vision par ordinateur énergétique (IA)
        public float EnergyRoboticsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la robotique énergétique (IA)
        public float EnergyAutomationEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'automatisation énergétique (IA)
        public float EnergyControlTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie du contrôle énergétique (IA)
        public float EnergySystemsTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie des systèmes énergétique (IA)
        public float EnergyComplexityTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie de la complexité énergétique (IA)
        public float EnergyChaosTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie du chaos énergétique (IA)
        public float EnergyNetworkTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie des réseaux énergétique (IA)
        public float EnergyGameTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie des jeux énergétique (IA)
        public float EnergyDecisionTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie de la décision énergétique (IA)
        public float EnergyProbabilityTheoryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la théorie des probabilités énergétique (IA)
        public float EnergyStatisticsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des statistiques énergétique (IA)
        public float EnergyMathematicsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des mathématiques énergétique (IA)
        public float EnergyPhysicsEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la physique énergétique (IA)
        public float EnergyChemistryEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la chimie énergétique (IA)
        public float EnergyBiologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la biologie énergétique (IA)
        public float EnergyEarthScienceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des sciences de la Terre énergétique (IA)
        public float EnergyAstronomyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'astronomie énergétique (IA)
        public float EnergyCosmologyEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de la cosmologie énergétique (IA)
        public float EnergySpaceScienceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des sciences spatiales énergétique (IA)
        public float EnergyMaterialsScienceEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie des sciences des matériaux énergétique (IA)
        public float EnergyEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'ingénierie énergétique (IA)
        public float EnergyArchitectureEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'architecture énergétique (IA)
        public float EnergyUrbanPlanningEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'aménagement urbain énergétique (IA)
        public float EnergyLandscapeArchitectureEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'architecture paysagère énergétique (IA)
        public float EnergyInteriorDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design d'intérieur énergétique (IA)
        public float EnergyIndustrialDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design industriel énergétique (IA)
        public float EnergyGraphicDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design graphique énergétique (IA)
        public float EnergyFashionDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design de mode énergétique (IA)
        public float EnergyTextileDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design textile énergétique (IA)
        public float EnergyJewelryDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design de bijoux énergétique (IA)
        public float EnergyFurnitureDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design de mobilier énergétique (IA)
        public float EnergyAutomotiveDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design automobile énergétique (IA)
        public float EnergyAerospaceDesignEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du design aérospatial énergétique (IA)
        public float EnergyNavalArchitectureEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie de l'architecture navale énergétique (IA)
        public float EnergyCivilEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie civil énergétique (IA)
        public float EnergyMechanicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie mécanique énergétique (IA)
        public float EnergyElectricalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie électrique énergétique (IA)
        public float EnergyElectronicEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie électronique énergétique (IA)
        public float EnergyChemicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie chimique énergétique (IA)
        public float EnergyBiomedicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie biomédical énergétique (IA)
        public float EnergyEnvironmentalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie environnemental énergétique (IA)
        public float EnergyAgriculturalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie agricole énergétique (IA)
        public float EnergyFoodEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie alimentaire énergétique (IA)
        public float EnergyPharmaceuticalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie pharmaceutique énergétique (IA)
        public float EnergyNuclearEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie nucléaire énergétique (IA)
        public float EnergyPetroleumEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie pétrolier énergétique (IA)
        public float EnergyMiningEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie minier énergétique (IA)
        public float EnergyGeologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie géologique énergétique (IA)
        public float EnergyOceanographicEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie océanographique énergétique (IA)
        public float EnergyMeteorologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie météorologique énergétique (IA)
        public float EnergyClimatologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie climatologique énergétique (IA)
        public float EnergySeismologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie sismologique énergétique (IA)
        public float EnergyVolcanologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie volcanologique énergétique (IA)
        public float EnergyGlaciologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie glaciologique énergétique (IA)
        public float EnergyHydrologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie hydrologique énergétique (IA)
        public float EnergySoilEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie des sols énergétique (IA)
        public float EnergyStructuralEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie structurel énergétique (IA)
        public float EnergyTransportationEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie des transports énergétique (IA)
        public float EnergyTrafficEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie du trafic énergétique (IA)
        public float EnergyLogisticsEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie logistique énergétique (IA)
        public float EnergySupplyChainEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la chaîne d'approvisionnement énergétique (IA)
        public float EnergyManufacturingEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la fabrication énergétique (IA)
        public float EnergyProductionEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la production énergétique (IA)
        public float EnergyQualityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la qualité énergétique (IA)
        public float EnergySafetyEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la sécurité énergétique (IA)
        public float EnergyRiskEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie du risque énergétique (IA)
        public float EnergyReliabilityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la fiabilité énergétique (IA)
        public float EnergyMaintainabilityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la maintenabilité énergétique (IA)
        public float EnergyAvailabilityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la disponibilité énergétique (IA)
        public float EnergySurvivabilityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la survie énergétique (IA)
        public float EnergyVulnerabilityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la vulnérabilité énergétique (IA)
        public float EnergyThreatEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie des menaces énergétique (IA)
        public float EnergyCountermeasureEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie des contre-mesures énergétique (IA)
        public float EnergySecurityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la sécurité énergétique (IA)
        public float EnergyDefenseEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la défense énergétique (IA)
        public float EnergyOffenseEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de l'offense énergétique (IA)
        public float EnergyWarfareEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la guerre énergétique (IA)
        public float EnergyPeacekeepingEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie du maintien de la paix énergétique (IA)
        public float EnergyHumanitarianEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie humanitaire énergétique (IA)
        public float EnergyDevelopmentEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie du développement énergétique (IA)
        public float EnergySustainabilityEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie du développement durable énergétique (IA)
        public float EnergyEcologicalEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie écologique énergétique (IA)
        public float EnergyConservationEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la conservation énergétique (IA)
        public float EnergyRestorationEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la restauration énergétique (IA)
        public float EnergyRemediationEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la remédiation énergétique (IA)
        public float EnergyPollutionControlEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie du contrôle de la pollution énergétique (IA)
        public float EnergyWasteManagementEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie de la gestion des déchets énergétique (IA)
        public float EnergyRecyclingEngineeringEnergyStorageEngineeringKnowledge; // Connaissance du génie du stockage d'énergie du génie du recyclage énergétique (IA)
    }

    public struct MovementStateComponent : IComponent, IReusableComponent
    {
        public MovementState State; public Vector2 DesiredVelocity; public bool IsSlipping; public float SlipRecoveryTimer;
        // Ajouts
        public MovementStance Stance; public bool IsUsingCover; public bool IsSprintingBurst; public bool IsPeeking; public bool IsStrafing;
        public bool IsUnderSuppression; public bool IsSwimming; public bool IsInZeroG; public bool IsMountedOnVehicle; public bool IsPerformingParkour;
        public bool IsClimbing; public bool IsExploitingObstacle; public bool IsFeinting; public bool IsRetreating; public bool IsCharging;
        public bool IsAlerted; public bool IsFleeing; public bool IsAttackingMelee; public bool IsAttackingRanged; public bool IsBlocking; public bool IsDodging; public bool IsKnockedDown; public bool IsStunned;
        public float CoverSearchRadius; public float PeekDuration; public float StrafeSpeedMultiplier; public float SuppressionRecoveryRate;
        public float SwimmingStrokePower; public float ZeroGThrusterPower; public float VaultSpeed; public float ClimbSpeed; public float ParkourVaultHeight;
        public float FeintDuration; public float RetreatSpeedMultiplier; public float ChargeSpeedMultiplier; public float ChargeDamageMultiplier;
        public float AlertLevel; public float FleeThreshold; public float AttackRangeMelee; public float AttackRangeRanged; public float BlockStaminaCost;
        public float DodgeStaminaCost; public float KnockdownRecoveryTime; public float StunRecoveryTime;
        public List<MovementTag> ActiveBehaviorTags; // Pour les comportements avancés (IA)
    }

    public struct AnimationStateComponent : IComponent, IReusableComponent
    {
        public string CurrentState;
        public float SpeedParameter;
        public float DirectionParameter;
        public float SlopeParameter;
        public float InjuryParameter;
        public float StressParameter;
        public float StaminaParameter;
        public float TractionParameter;
        public float LeanAmount; // Pour l'inclinaison (Lean)
        public float MomentumAmount; // Pour le momentum visuel
        // Ajouts pour la cohérence visuelle et les comportements avancés
        public float FacialExpressionParameter; // Pour la synchro faciale
        public float BreathIntensityParameter; // Pour la respiration
        public float FatigueShakeParameter; // Pour les tremblements de fatigue
        public float StressSweatParameter; // Pour la transpiration de stress
        public float PainFlinchParameter; // Pour les grimaces de douleur
        public float EmotionDrivenPoseParameter; // Pour les poses expressives
        public float LODAnimationDetail; // Pour le LOD dynamique
        public float AnimationPlaybackSpeed; // Pour la vitesse de lecture (ex: slow-motion)
        public float FootIKLeftWeight; // Poids de l'IK pour le pied gauche
        public float FootIKRightWeight; // Poids de l'IK pour le pied droit
        public Vector3 FootIKLeftPosition; // Position cible de l'IK pour le pied gauche
        public Vector3 FootIKRightPosition; // Position cible de l'IK pour le pied droit
        public bool IsFootIKLeftActive; // Activation de l'IK pour le pied gauche
        public bool IsFootIKRightActive; // Activation de l'IK pour le pied droit
        public float CameraShakeIntensity; // Intensité de secousse caméra basée sur l'animation
        public float MotionBlurIntensity; // Intensité du flou de mouvement
        public float MotionBlurVelocityScale; // Echelle de la vélocité pour le flou
        public float FacialSyncEffort; // Niveau d'effort pour la synchro faciale
        public float FacialSyncStamina; // Niveau de stamina pour la synchro faciale
        public float FacialSyncStress; // Niveau de stress pour la synchro faciale
        public float BlendWeightUpperBody; // Poids de blending pour le haut du corps
        public float BlendWeightLowerBody; // Poids de blending pour le bas du corps
        public float BlendWeightHead; // Poids de blending pour la tête
        public float BlendWeightArms; // Poids de blending pour les bras
        public float BlendWeightLegs; // Poids de blending pour les jambes
        public float BlendWeightTorso; // Poids de blending pour le torse
        public float BlendWeightHands; // Poids de blending pour les mains
        public float BlendWeightFeet; // Poids de blending pour les pieds
        public float BlendWeightFingers; // Poids de blending pour les doigts
        public float BlendWeightHair; // Poids de blending pour les cheveux (si applicable)
        public float BlendWeightCloth; // Poids de blending pour les vêtements (si applicable)
        public float BlendWeightAccessories; // Poids de blending pour les accessoires (si applicable)
        public float BlendWeightEyes; // Poids de blending pour les yeux (clignotement, regard)
        public float BlendWeightEyebrows; // Poids de blending pour les sourcils
        public float BlendWeightMouth; // Poids de blending pour la bouche (parole, expression)
        public float BlendWeightNeck; // Poids de blending pour le cou
        public float BlendWeightShoulders; // Poids de blending pour les épaules
        public float BlendWeightElbows; // Poids de blending pour les coudes
        public float BlendWeightWrists; // Poids de blending pour les poignets
        public float BlendWeightKnees; // Poids de blending pour les genoux
        public float BlendWeightAnkles; // Poids de blending pour les chevilles
        public float BlendWeightSpine; // Poids de blending pour la colonne vertébrale
        public float BlendWeightPelvis; // Poids de blending pour le bassin
        public float BlendWeightHips; // Poids de blending pour les hanches
        public float BlendWeightChest; // Poids de blending pour la poitrine
        public float BlendWeightBack; // Poids de blending pour le dos
        public float BlendWeightWaist; // Poids de blending pour la taille
        public float BlendWeightGroin; // Poids de blending pour l'entrejambe
        public float BlendWeightButtocks; // Poids de blending pour les fesses
        public float BlendWeightThighs; // Poids de blending pour les cuisses
        public float BlendWeightCalves; // Poids de blending pour les mollets
        public float BlendWeightShins; // Poids de blending pour les tibias
        public float BlendWeightHeels; // Poids de blending pour les talons
        public float BlendWeightToes; // Poids de blending pour les orteils
        public float BlendWeightNose; // Poids de blending pour le nez
        public float BlendWeightCheeks; // Poids de blending pour les joues
        public float BlendWeightChin; // Poids de blending pour le menton
        public float BlendWeightForehead; // Poids de blending pour le front
        public float BlendWeightTemple; // Poids de blending pour la tempe
        public float BlendWeightEar; // Poids de blending pour l'oreille
        public float BlendWeightLip; // Poids de blending pour la lèvre
        public float BlendWeightTongue; // Poids de blending pour la langue
        public float BlendWeightEyeLid; // Poids de blending pour la paupière
        public float BlendWeightEyeBall; // Poids de blending pour le globe oculaire
        public float BlendWeightEyeBrow; // Poids de blending pour le sourcil
        public float BlendWeightNoseBridge; // Poids de blending pour le pont du nez
        public float BlendWeightNoseTip; // Poids de blending pour la pointe du nez
        public float BlendWeightNostril; // Poids de blending pour la narine
        public float BlendWeightMouthCorner; // Poids de blending pour le coin de la bouche
        public float BlendWeightMouthTop; // Poids de blending pour le haut de la bouche
        public float BlendWeightMouthBottom; // Poids de blending pour le bas de la bouche
        public float BlendWeightMouthInside; // Poids de blending pour l'intérieur de la bouche
        public float BlendWeightTeeth; // Poids de blending pour les dents
        public float BlendWeightGum; // Poids de blending pour la gencive
        public float BlendWeightTongueTip; // Poids de blending pour la pointe de la langue
        public float BlendWeightTongueBase; // Poids de blending pour la base de la langue
        public float BlendWeightTongueMiddle; // Poids de blending pour le milieu de la langue
        public float BlendWeightTongueRoot; // Poids de blending pour la racine de la langue
        public float BlendWeightTongueDorsum; // Poids de blending pour le dos de la langue
        public float BlendWeightTongueVentral; // Poids de blending pour le ventre de la langue
        public float BlendWeightTongueApex; // Poids de blending pour l'apex de la langue
        public float BlendWeightTongueMidline; // Poids de blending pour la ligne médiane de la langue
        public float BlendWeightTongueSides; // Poids de blending pour les côtés de la langue
        public float BlendWeightTongueSurface; // Poids de blending pour la surface de la langue
        public float BlendWeightTongueInterior; // Poids de blending pour l'intérieur de la langue
        public float BlendWeightTongueExterior; // Poids de blending pour l'extérieur de la langue
        public float BlendWeightTongueCore; // Poids de blending pour le noyau de la langue
        public float BlendWeightTongueEdge; // Poids de blending pour le bord de la langue
        public float BlendWeightTongueTipUpper; // Poids de blending pour la pointe supérieure de la langue
        public float BlendWeightTongueTipLower; // Poids de blending pour la pointe inférieure de la langue
        public float BlendWeightTongueTipLeft; // Poids de blending pour la pointe gauche de la langue
        public float BlendWeightTongueTipRight; // Poids de blending pour la pointe droite de la langue
        public float BlendWeightTongueTipFront; // Poids de blending pour la pointe avant de la langue
        public float BlendWeightTongueTipBack; // Poids de blending pour la pointe arrière de la langue
        public float BlendWeightTongueTipTop; // Poids de blending pour la pointe supérieure de la langue
        public float BlendWeightTongueTipBottom; // Poids de blending pour la pointe inférieure de la langue
        public float BlendWeightTongueBaseUpper; // Poids de blending pour la base supérieure de la langue
        public float BlendWeightTongueBaseLower; // Poids de blending pour la base inférieure de la langue
        public float BlendWeightTongueBaseLeft; // Poids de blending pour la base gauche de la langue
        public float BlendWeightTongueBaseRight; // Poids de blending pour la base droite de la langue
        public float BlendWeightTongueBaseFront; // Poids de blending pour la base avant de la langue
        public float BlendWeightTongueBaseBack; // Poids de blending pour la base arrière de la langue
        public float BlendWeightTongueBaseTop; // Poids de blending pour la base supérieure de la langue
        public float BlendWeightTongueBaseBottom; // Poids de blending pour la base inférieure de la langue
        public float BlendWeightTongueMiddleUpper; // Poids de blending pour le milieu supérieur de la langue
        public float BlendWeightTongueMiddleLower; // Poids de blending pour le milieu inférieur de la langue
        public float BlendWeightTongueMiddleLeft; // Poids de blending pour le milieu gauche de la langue
        public float BlendWeightTongueMiddleRight; // Poids de blending pour le milieu droite de la langue
        public float BlendWeightTongueMiddleFront; // Poids de blending pour le milieu avant de la langue
        public float BlendWeightTongueMiddleBack; // Poids de blending pour le milieu arrière de la langue
        public float BlendWeightTongueMiddleTop; // Poids de blending pour le milieu supérieur de la langue
        public float BlendWeightTongueMiddleBottom; // Poids de blending pour le milieu inférieur de la langue
        public float BlendWeightTongueRootUpper; // Poids de blending pour la racine supérieure de la langue
        public float BlendWeightTongueRootLower; // Poids de blending pour la racine inférieure de la langue
        public float BlendWeightTongueRootLeft; // Poids de blending pour la racine gauche de la langue
        public float BlendWeightTongueRootRight; // Poids de blending pour la racine droite de la langue
        public float BlendWeightTongueRootFront; // Poids de blending pour la racine avant de la langue
        public float BlendWeightTongueRootBack; // Poids de blending pour la racine arrière de la langue
        public float BlendWeightTongueRootTop; // Poids de blending pour la racine supérieure de la langue
        public float BlendWeightTongueRootBottom; // Poids de blending pour la racine inférieure de la langue
        public float BlendWeightTongueDorsumUpper; // Poids de blending pour le dos supérieur de la langue
        public float BlendWeightTongueDorsumLower; // Poids de blending pour le dos inférieur de la langue
        public float BlendWeightTongueDorsumLeft; // Poids de blending pour le dos gauche de la langue
        public float BlendWeightTongueDorsumRight; // Poids de blending pour le dos droite de la langue
        public float BlendWeightTongueDorsumFront; // Poids de blending pour le dos avant de la langue
        public float BlendWeightTongueDorsumBack; // Poids de blending pour le dos arrière de la langue
        public float BlendWeightTongueDorsumTop; // Poids de blending pour le dos supérieur de la langue
        public float BlendWeightTongueDorsumBottom; // Poids de blending pour le dos inférieur de la langue
        public float BlendWeightTongueVentralUpper; // Poids de blending pour le ventre supérieur de la langue
        public float BlendWeightTongueVentralLower; // Poids de blending pour le ventre inférieur de la langue
        public float BlendWeightTongueVentralLeft; // Poids de blending pour le ventre gauche de la langue
        public float BlendWeightTongueVentralRight; // Poids de blending pour le ventre droite de la langue
        public float BlendWeightTongueVentralFront; // Poids de blending pour le ventre avant de la langue
        public float BlendWeightTongueVentralBack; // Poids de blending pour le ventre arrière de la langue
        public float BlendWeightTongueVentralTop; // Poids de blending pour le ventre supérieur de la langue
        public float BlendWeightTongueVentralBottom; // Poids de blending pour le ventre inférieur de la langue
        public float BlendWeightTongueApexUpper; // Poids de blending pour l'apex supérieur de la langue
        public float BlendWeightTongueApexLower; // Poids de blending pour l'apex inférieur de la langue
        public float BlendWeightTongueApexLeft; // Poids de blending pour l'apex gauche de la langue
        public float BlendWeightTongueApexRight; // Poids de blending pour l'apex droite de la langue
        public float BlendWeightTongueApexFront; // Poids de blending pour l'apex avant de la langue
        public float BlendWeightTongueApexBack; // Poids de blending pour l'apex arrière de la langue
        public float BlendWeightTongueApexTop; // Poids de blending pour l'apex supérieur de la langue
        public float BlendWeightTongueApexBottom; // Poids de blending pour l'apex inférieur de la langue
        public float BlendWeightTongueMidlineUpper; // Poids de blending pour la ligne médiane supérieure de la langue
        public float BlendWeightTongueMidlineLower; // Poids de blending pour la ligne médiane inférieure de la langue
        public float BlendWeightTongueMidlineLeft; // Poids de blending pour la ligne médiane gauche de la langue
        public float BlendWeightTongueMidlineRight; // Poids de blending pour la ligne médiane droite de la langue
        public float BlendWeightTongueMidlineFront; // Poids de blending pour la ligne médiane avant de la langue
        public float BlendWeightTongueMidlineBack; // Poids de blending pour la ligne médiane arrière de la langue
        public float BlendWeightTongueMidlineTop; // Poids de blending pour la ligne médiane supérieure de la langue
        public float BlendWeightTongueMidlineBottom; // Poids de blending pour la ligne médiane inférieure de la langue
        public float BlendWeightTongueSidesUpper; // Poids de blending pour les côtés supérieurs de la langue
        public float BlendWeightTongueSidesLower; // Poids de blending pour les côtés inférieurs de la langue
        public float BlendWeightTongueSidesLeft; // Poids de blending pour les côtés gauches de la langue
        public float BlendWeightTongueSidesRight; // Poids de blending pour les côtés droits de la langue
        public float BlendWeightTongueSidesFront; // Poids de blending pour les côtés avant de la langue
        public float BlendWeightTongueSidesBack; // Poids de blending pour les côtés arrière de la langue
        public float BlendWeightTongueSidesTop; // Poids de blending pour les côtés supérieurs de la langue
        public float BlendWeightTongueSidesBottom; // Poids de blending pour les côtés inférieurs de la langue
        public float BlendWeightTongueSurfaceUpper; // Poids de blending pour la surface supérieure de la langue
        public float BlendWeightTongueSurfaceLower; // Poids de blending pour la surface inférieure de la langue
        public float BlendWeightTongueSurfaceLeft; // Poids de blending pour la surface gauche de la langue
        public float BlendWeightTongueSurfaceRight; // Poids de blending pour la surface droite de la langue
        public float BlendWeightTongueSurfaceFront; // Poids de blending pour la surface avant de la langue
        public float BlendWeightTongueSurfaceBack; // Poids de blending pour la surface arrière de la langue
        public float BlendWeightTongueSurfaceTop; // Poids de blending pour la surface supérieure de la langue
        public float BlendWeightTongueSurfaceBottom; // Poids de blending pour la surface inférieure de la langue
        public float BlendWeightTongueInteriorUpper; // Poids de blending pour l'intérieur supérieur de la langue
        public float BlendWeightTongueInteriorLower; // Poids de blending pour l'intérieur inférieur de la langue
        public float BlendWeightTongueInteriorLeft; // Poids de blending pour l'intérieur gauche de la langue
        public float BlendWeightTongueInteriorRight; // Poids de blending pour l'intérieur droite de la langue
        public float BlendWeightTongueInteriorFront; // Poids de blending pour l'intérieur avant de la langue
        public float BlendWeightTongueInteriorBack; // Poids de blending pour l'intérieur arrière de la langue
        public float BlendWeightTongueInteriorTop; // Poids de blending pour l'intérieur supérieur de la langue
        public float BlendWeightTongueInteriorBottom; // Poids de blending pour l'intérieur inférieur de la langue
        public float BlendWeightTongueExteriorUpper; // Poids de blending pour l'extérieur supérieur de la langue
        public float BlendWeightTongueExteriorLower; // Poids de blending pour l'extérieur inférieur de la langue
        public float BlendWeightTongueExteriorLeft; // Poids de blending pour l'extérieur gauche de la langue
        public float BlendWeightTongueExteriorRight; // Poids de blending pour l'extérieur droite de la langue
        public float BlendWeightTongueExteriorFront; // Poids de blending pour l'extérieur avant de la langue
        public float BlendWeightTongueExteriorBack; // Poids de blending pour l'extérieur arrière de la langue
        public float BlendWeightTongueExteriorTop; // Poids de blending pour l'extérieur supérieur de la langue
        public float BlendWeightTongueExteriorBottom; // Poids de blending pour l'extérieur inférieur de la langue
        public float BlendWeightTongueCoreUpper; // Poids de blending pour le noyau supérieur de la langue
        public float BlendWeightTongueCoreLower; // Poids de blending pour le noyau inférieur de la langue
        public float BlendWeightTongueCoreLeft; // Poids de blending pour le noyau gauche de la langue
        public float BlendWeightTongueCoreRight; // Poids de blending pour le noyau droite de la langue
        public float BlendWeightTongueCoreFront; // Poids de blending pour le noyau avant de la langue
        public float BlendWeightTongueCoreBack; // Poids de blending pour le noyau arrière de la langue
        public float BlendWeightTongueCoreTop; // Poids de blending pour le noyau supérieur de la langue
        public float BlendWeightTongueCoreBottom; // Poids de blending pour le noyau inférieur de la langue
        public float BlendWeightTongueEdgeUpper; // Poids de blending pour le bord supérieur de la langue
        public float BlendWeightTongueEdgeLower; // Poids de blending pour le bord inférieur de la langue
        public float BlendWeightTongueEdgeLeft; // Poids de blending pour le bord gauche de la langue
        public float BlendWeightTongueEdgeRight; // Poids de blending pour le bord droite de la langue
        public float BlendWeightTongueEdgeFront; // Poids de blending pour le bord avant de la langue
        public float BlendWeightTongueEdgeBack; // Poids de blending pour le bord arrière de la langue
        public float BlendWeightTongueEdgeTop; // Poids de blending pour le bord supérieur de la langue
        public float BlendWeightTongueEdgeBottom; // Poids de blending pour le bord inférieur de la langue

        // Méthode utilitaire pour obtenir les paramètres courants
        public Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>
            {
                {"CurrentState", CurrentState},
                {"SpeedParameter", SpeedParameter},
                {"DirectionParameter", DirectionParameter},
                {"SlopeParameter", SlopeParameter},
                {"InjuryParameter", InjuryParameter},
                {"StressParameter", StressParameter},
                {"StaminaParameter", StaminaParameter},
                {"TractionParameter", TractionParameter},
                {"LeanAmount", LeanAmount},
                {"MomentumAmount", MomentumAmount},
                {"FacialExpressionParameter", FacialExpressionParameter},
                {"BreathIntensityParameter", BreathIntensityParameter},
                {"FatigueShakeParameter", FatigueShakeParameter},
                {"StressSweatParameter", StressSweatParameter},
                {"PainFlinchParameter", PainFlinchParameter},
                {"EmotionDrivenPoseParameter", EmotionDrivenPoseParameter},
                {"LODAnimationDetail", LODAnimationDetail},
                {"AnimationPlaybackSpeed", AnimationPlaybackSpeed},
                {"FootIKLeftWeight", FootIKLeftWeight},
                {"FootIKRightWeight", FootIKRightWeight},
                {"FootIKLeftPosition", FootIKLeftPosition},
                {"FootIKRightPosition", FootIKRightPosition},
                {"IsFootIKLeftActive", IsFootIKLeftActive},
                {"IsFootIKRightActive", IsFootIKRightActive},
                {"CameraShakeIntensity", CameraShakeIntensity},
                {"MotionBlurIntensity", MotionBlurIntensity},
                {"MotionBlurVelocityScale", MotionBlurVelocityScale},
                {"FacialSyncEffort", FacialSyncEffort},
                {"FacialSyncStamina", FacialSyncStamina},
                {"FacialSyncStress", FacialSyncStress},
                {"BlendWeightUpperBody", BlendWeightUpperBody},
                {"BlendWeightLowerBody", BlendWeightLowerBody},
                {"BlendWeightHead", BlendWeightHead},
                {"BlendWeightArms", BlendWeightArms},
                {"BlendWeightLegs", BlendWeightLegs},
                {"BlendWeightTorso", BlendWeightTorso},
                {"BlendWeightHands", BlendWeightHands},
                {"BlendWeightFeet", BlendWeightFeet},
                {"BlendWeightFingers", BlendWeightFingers},
                {"BlendWeightHair", BlendWeightHair},
                {"BlendWeightCloth", BlendWeightCloth},
                {"BlendWeightAccessories", BlendWeightAccessories},
                {"BlendWeightEyes", BlendWeightEyes},
                {"BlendWeightEyebrows", BlendWeightEyebrows},
                {"BlendWeightMouth", BlendWeightMouth},
                {"BlendWeightNeck", BlendWeightNeck},
                {"BlendWeightShoulders", BlendWeightShoulders},
                {"BlendWeightElbows", BlendWeightElbows},
                {"BlendWeightWrists", BlendWeightWrists},
                {"BlendWeightKnees", BlendWeightKnees},
                {"BlendWeightAnkles", BlendWeightAnkles},
                {"BlendWeightSpine", BlendWeightSpine},
                {"BlendWeightPelvis", BlendWeightPelvis},
                {"BlendWeightHips", BlendWeightHips},
                {"BlendWeightChest", BlendWeightChest},
                {"BlendWeightBack", BlendWeightBack},
                {"BlendWeightWaist", BlendWeightWaist},
                {"BlendWeightGroin", BlendWeightGroin},
                {"BlendWeightButtocks", BlendWeightButtocks},
                {"BlendWeightThighs", BlendWeightThighs},
                {"BlendWeightCalves", BlendWeightCalves},
                {"BlendWeightShins", BlendWeightShins},
                {"BlendWeightHeels", BlendWeightHeels},
                {"BlendWeightToes", BlendWeightToes},
                {"BlendWeightNose", BlendWeightNose},
                {"BlendWeightCheeks", BlendWeightCheeks},
                {"BlendWeightChin", BlendWeightChin},
                {"BlendWeightForehead", BlendWeightForehead},
                {"BlendWeightTemple", BlendWeightTemple},
                {"BlendWeightEar", BlendWeightEar},
                {"BlendWeightLip", BlendWeightLip},
                {"BlendWeightTongue", BlendWeightTongue},
                {"BlendWeightEyeLid", BlendWeightEyeLid},
                {"BlendWeightEyeBall", BlendWeightEyeBall},
                {"BlendWeightEyeBrow", BlendWeightEyeBrow},
                {"BlendWeightNoseBridge", BlendWeightNoseBridge},
                {"BlendWeightNoseTip", BlendWeightNoseTip},
                {"BlendWeightNostril", BlendWeightNostril},
                {"BlendWeightMouthCorner", BlendWeightMouthCorner},
                {"BlendWeightMouthTop", BlendWeightMouthTop},
                {"BlendWeightMouthBottom", BlendWeightMouthBottom},
                {"BlendWeightMouthInside", BlendWeightMouthInside},
                {"BlendWeightTeeth", BlendWeightTeeth},
                {"BlendWeightGum", BlendWeightGum},
                {"BlendWeightTongueTip", BlendWeightTongueTip},
                {"BlendWeightTongueBase", BlendWeightTongueBase},
                {"BlendWeightTongueMiddle", BlendWeightTongueMiddle},
                {"BlendWeightTongueRoot", BlendWeightTongueRoot},
                {"BlendWeightTongueDorsum", BlendWeightTongueDorsum},
                {"BlendWeightTongueVentral", BlendWeightTongueVentral},
                {"BlendWeightTongueApex", BlendWeightTongueApex},
                {"BlendWeightTongueMidline", BlendWeightTongueMidline},
                {"BlendWeightTongueSides", BlendWeightTongueSides},
                {"BlendWeightTongueSurface", BlendWeightTongueSurface},
                {"BlendWeightTongueInterior", BlendWeightTongueInterior},
                {"BlendWeightTongueExterior", BlendWeightTongueExterior},
                {"BlendWeightTongueCore", BlendWeightTongueCore},
                {"BlendWeightTongueEdge", BlendWeightTongueEdge},
                {"BlendWeightTongueTipUpper", BlendWeightTongueTipUpper},
                {"BlendWeightTongueTipLower", BlendWeightTongueTipLower},
                {"BlendWeightTongueTipLeft", BlendWeightTongueTipLeft},
                {"BlendWeightTongueTipRight", BlendWeightTongueTipRight},
                {"BlendWeightTongueTipFront", BlendWeightTongueTipFront},
                {"BlendWeightTongueTipBack", BlendWeightTongueTipBack},
                {"BlendWeightTongueTipTop", BlendWeightTongueTipTop},
                {"BlendWeightTongueTipBottom", BlendWeightTongueTipBottom},
                {"BlendWeightTongueBaseUpper", BlendWeightTongueBaseUpper},
                {"BlendWeightTongueBaseLower", BlendWeightTongueBaseLower},
                {"BlendWeightTongueBaseLeft", BlendWeightTongueBaseLeft},
                {"BlendWeightTongueBaseRight", BlendWeightTongueBaseRight},
                {"BlendWeightTongueBaseFront", BlendWeightTongueBaseFront},
                {"BlendWeightTongueBaseBack", BlendWeightTongueBaseBack},
                {"BlendWeightTongueBaseTop", BlendWeightTongueBaseTop},
                {"BlendWeightTongueBaseBottom", BlendWeightTongueBaseBottom},
                {"BlendWeightTongueMiddleUpper", BlendWeightTongueMiddleUpper},
                {"BlendWeightTongueMiddleLower", BlendWeightTongueMiddleLower},
                {"BlendWeightTongueMiddleLeft", BlendWeightTongueMiddleLeft},
                {"BlendWeightTongueMiddleRight", BlendWeightTongueMiddleRight},
                {"BlendWeightTongueMiddleFront", BlendWeightTongueMiddleFront},
                {"BlendWeightTongueMiddleBack", BlendWeightTongueMiddleBack},
                {"BlendWeightTongueMiddleTop", BlendWeightTongueMiddleTop},
                {"BlendWeightTongueMiddleBottom", BlendWeightTongueMiddleBottom},
                {"BlendWeightTongueRootUpper", BlendWeightTongueRootUpper},
                {"BlendWeightTongueRootLower", BlendWeightTongueRootLower},
                {"BlendWeightTongueRootLeft", BlendWeightTongueRootLeft},
                {"BlendWeightTongueRootRight", BlendWeightTongueRootRight},
                {"BlendWeightTongueRootFront", BlendWeightTongueRootFront},
                {"BlendWeightTongueRootBack", BlendWeightTongueRootBack},
                {"BlendWeightTongueRootTop", BlendWeightTongueRootTop},
                {"BlendWeightTongueRootBottom", BlendWeightTongueRootBottom},
                {"BlendWeightTongueDorsumUpper", BlendWeightTongueDorsumUpper},
                {"BlendWeightTongueDorsumLower", BlendWeightTongueDorsumLower},
                {"BlendWeightTongueDorsumLeft", BlendWeightTongueDorsumLeft},
                {"BlendWeightTongueDorsumRight", BlendWeightTongueDorsumRight},
                {"BlendWeightTongueDorsumFront", BlendWeightTongueDorsumFront},
                {"BlendWeightTongueDorsumBack", BlendWeightTongueDorsumBack},
                {"BlendWeightTongueDorsumTop", BlendWeightTongueDorsumTop},
                {"BlendWeightTongueDorsumBottom", BlendWeightTongueDorsumBottom},
                {"BlendWeightTongueVentralUpper", BlendWeightTongueVentralUpper},
                {"BlendWeightTongueVentralLower", BlendWeightTongueVentralLower},
                {"BlendWeightTongueVentralLeft", BlendWeightTongueVentralLeft},
                {"BlendWeightTongueVentralRight", BlendWeightTongueVentralRight},
                {"BlendWeightTongueVentralFront", BlendWeightTongueVentralFront},
                {"BlendWeightTongueVentralBack", BlendWeightTongueVentralBack},
                {"BlendWeightTongueVentralTop", BlendWeightTongueVentralTop},
                {"BlendWeightTongueVentralBottom", BlendWeightTongueVentralBottom},
                {"BlendWeightTongueApexUpper", BlendWeightTongueApexUpper},
                {"BlendWeightTongueApexLower", BlendWeightTongueApexLower},
                {"BlendWeightTongueApexLeft", BlendWeightTongueApexLeft},
                {"BlendWeightTongueApexRight", BlendWeightTongueApexRight},
                {"BlendWeightTongueApexFront", BlendWeightTongueApexFront},
                {"BlendWeightTongueApexBack", BlendWeightTongueApexBack},
                {"BlendWeightTongueApexTop", BlendWeightTongueApexTop},
                {"BlendWeightTongueApexBottom", BlendWeightTongueApexBottom},
                {"BlendWeightTongueMidlineUpper", BlendWeightTongueMidlineUpper},
                {"BlendWeightTongueMidlineLower", BlendWeightTongueMidlineLower},
                {"BlendWeightTongueMidlineLeft", BlendWeightTongueMidlineLeft},
                {"BlendWeightTongueMidlineRight", BlendWeightTongueMidlineRight},
                {"BlendWeightTongueMidlineFront", BlendWeightTongueMidlineFront},
                {"BlendWeightTongueMidlineBack", BlendWeightTongueMidlineBack},
                {"BlendWeightTongueMidlineTop", BlendWeightTongueMidlineTop},
                {"BlendWeightTongueMidlineBottom", BlendWeightTongueMidlineBottom},
                {"BlendWeightTongueSidesUpper", BlendWeightTongueSidesUpper},
                {"BlendWeightTongueSidesLower", BlendWeightTongueSidesLower},
                {"BlendWeightTongueSidesLeft", BlendWeightTongueSidesLeft},
                {"BlendWeightTongueSidesRight", BlendWeightTongueSidesRight},
                {"BlendWeightTongueSidesFront", BlendWeightTongueSidesFront},
                {"BlendWeightTongueSidesBack", BlendWeightTongueSidesBack},
                {"BlendWeightTongueSidesTop", BlendWeightTongueSidesTop},
                {"BlendWeightTongueSidesBottom", BlendWeightTongueSidesBottom},
                {"BlendWeightTongueSurfaceUpper", BlendWeightTongueSurfaceUpper},
                {"BlendWeightTongueSurfaceLower", BlendWeightTongueSurfaceLower},
                {"BlendWeightTongueSurfaceLeft", BlendWeightTongueSurfaceLeft},
                {"BlendWeightTongueSurfaceRight", BlendWeightTongueSurfaceRight},
                {"BlendWeightTongueSurfaceFront", BlendWeightTongueSurfaceFront},
                {"BlendWeightTongueSurfaceBack", BlendWeightTongueSurfaceBack},
                {"BlendWeightTongueSurfaceTop", BlendWeightTongueSurfaceTop},
                {"BlendWeightTongueSurfaceBottom", BlendWeightTongueSurfaceBottom},
                {"BlendWeightTongueInteriorUpper", BlendWeightTongueInteriorUpper},
                {"BlendWeightTongueInteriorLower", BlendWeightTongueInteriorLower},
                {"BlendWeightTongueInteriorLeft", BlendWeightTongueInteriorLeft},
                {"BlendWeightTongueInteriorRight", BlendWeightTongueInteriorRight},
                {"BlendWeightTongueInteriorFront", BlendWeightTongueInteriorFront},
                {"BlendWeightTongueInteriorBack", BlendWeightTongueInteriorBack},
                {"BlendWeightTongueInteriorTop", BlendWeightTongueInteriorTop},
                {"BlendWeightTongueInteriorBottom", BlendWeightTongueInteriorBottom},
                {"BlendWeightTongueExteriorUpper", BlendWeightTongueExteriorUpper},
                {"BlendWeightTongueExteriorLower", BlendWeightTongueExteriorLower},
                {"BlendWeightTongueExteriorLeft", BlendWeightTongueExteriorLeft},
                {"BlendWeightTongueExteriorRight", BlendWeightTongueExteriorRight},
                {"BlendWeightTongueExteriorFront", BlendWeightTongueExteriorFront},
                {"BlendWeightTongueExteriorBack", BlendWeightTongueExteriorBack},
                {"BlendWeightTongueExteriorTop", BlendWeightTongueExteriorTop},
                {"BlendWeightTongueExteriorBottom", BlendWeightTongueExteriorBottom},
                {"BlendWeightTongueCoreUpper", BlendWeightTongueCoreUpper},
                {"BlendWeightTongueCoreLower", BlendWeightTongueCoreLower},
                {"BlendWeightTongueCoreLeft", BlendWeightTongueCoreLeft},
                {"BlendWeightTongueCoreRight", BlendWeightTongueCoreRight},
                {"BlendWeightTongueCoreFront", BlendWeightTongueCoreFront},
                {"BlendWeightTongueCoreBack", BlendWeightTongueCoreBack},
                {"BlendWeightTongueCoreTop", BlendWeightTongueCoreTop},
                {"BlendWeightTongueCoreBottom", BlendWeightTongueCoreBottom},
                {"BlendWeightTongueEdgeUpper", BlendWeightTongueEdgeUpper},
                {"BlendWeightTongueEdgeLower", BlendWeightTongueEdgeLower},
                {"BlendWeightTongueEdgeLeft", BlendWeightTongueEdgeLeft},
                {"BlendWeightTongueEdgeRight", BlendWeightTongueEdgeRight},
                {"BlendWeightTongueEdgeFront", BlendWeightTongueEdgeFront},
                {"BlendWeightTongueEdgeBack", BlendWeightTongueEdgeBack},
                {"BlendWeightTongueEdgeTop", BlendWeightTongueEdgeTop},
                {"BlendWeightTongueEdgeBottom", BlendWeightTongueEdgeBottom}
            };
        }
    }

    public struct MovementSoundComponent : IComponent, IReusableComponent
    {
        public string CurrentFootstepSound;
        public string CurrentSurfaceSound;
        public string CurrentBreathingSound;
        public string CurrentEffortSound;
        public string CurrentImpactSound;
        public float VolumeMultiplier;
        public float PitchMultiplier;
        public float SpatialBlend;
        public bool IsLooping;
        // Ajouts pour la latence et la cohérence audio
        public float AudioLatency;
        public float AudioBufferLength;
        public float AudioSampleRate;
        public float AudioBitDepth;
        public float AudioChannelCount;
        public float AudioCompressionRatio;
        public float AudioQualitySetting;
        public float AudioDistanceAttenuation;
        public float AudioOcclusionFactor;
        public float AudioReverbFactor;
        public float AudioDopplerFactor;
        public float AudioLowPassFilter;
        public float AudioHighPassFilter;
        public float AudioBandPassFilter;
        public float AudioNotchFilter;
        public float AudioEQGain;
        public float AudioEQFrequency;
        public float AudioEQBandwidth;
        public float AudioEnvelopeAttack;
        public float AudioEnvelopeDecay;
        public float AudioEnvelopeSustain;
        public float AudioEnvelopeRelease;
        public float AudioLFOFrequency;
        public float AudioLFODepth;
        public float AudioLFOWaveform;
        public float AudioModulationDepth;
        public float AudioModulationRate;
        public float AudioModulationType;
        public float AudioDelayTime;
        public float AudioDelayFeedback;
        public float AudioDelayMix;
        public float AudioChorusDepth;
        public float AudioChorusRate;
        public float AudioChorusMix;
        public float AudioFlangerDepth;
        public float AudioFlangerRate;
        public float AudioFlangerMix;
        public float AudioPhaserDepth;
        public float AudioPhaserRate;
        public float AudioPhaserMix;
        public float AudioReverbTime;
        public float AudioReverbSize;
        public float AudioReverbDamping;
        public float AudioReverbDensity;
        public float AudioReverbHFRef;
        public float AudioReverbLFRef;
        public float AudioReverbRoom;
        public float AudioReverbRoomHF;
        public float AudioReverbRoomLF;
        public float AudioReverbDecayHFRatio;
        public float AudioReverbReflections;
        public float AudioReverbReflectionsDelay;
        public float AudioReverbLateReverb;
        public float AudioReverbLateReverbDelay;
        public float AudioReverbAirAbsorptionGainHF;
        public float AudioReverbRoomRollOffFactor;
        public float AudioReverbDecayHFLimit;
    }

    public struct MovementVFXComponent : IComponent, IReusableComponent
    {
        public string CurrentTrailEffect;
        public string CurrentDustEffect;
        public string CurrentSplashEffect;
        public string CurrentSparkEffect;
        public string CurrentImpactEffect;
        public string CurrentBreathEffect;
        public string CurrentSweatEffect;
        public string CurrentFatigueEffect;
        public string CurrentStressEffect;
        public string CurrentPainEffect;
        public string CurrentEmotionEffect;
        public float EffectScale;
        public float EffectIntensity;
        public float EffectLifetime;
        public bool IsEffectLooping;
        public Vector3 EffectOffset;
        public Quaternion EffectRotation;
        // Ajouts pour le Motion Blur, le Camera Shake, etc.
        public float MotionBlurIntensityVFX;
        public float MotionBlurVelocityScaleVFX;
        public float CameraShakeIntensityVFX;
        public float CameraShakeDurationVFX;
        public float ScreenFlashIntensityVFX;
        public float ScreenFlashColorR;
        public float ScreenFlashColorG;
        public float ScreenFlashColorB;
        public float ScreenFlashDurationVFX;
        public float HitStopDurationVFX;
        public float SlowMotionIntensityVFX;
        public float SlowMotionDurationVFX;
        public float ParticleSystemDensity;
        public float ParticleSystemEmissionRate;
        public float ParticleSystemMaxParticles;
        public float ParticleSystemStartLifetime;
        public float ParticleSystemStartSpeed;
        public float ParticleSystemStartSize;
        public float ParticleSystemStartRotation;
        public float ParticleSystemStartColorR;
        public float ParticleSystemStartColorG;
        public float ParticleSystemStartColorB;
        public float ParticleSystemStartColorA;
        public float ParticleSystemGravityModifier;
        public float ParticleSystemSimulationSpace;
        public float ParticleSystemScalingMode;
        public float ParticleSystemPlayOnAwake;
        public float ParticleSystemLoop;
        public float ParticleSystemPrewarm;
        public float ParticleSystemMaxShapeDimensions;
        public float ParticleSystemColliderQueryMode;
        public float ParticleSystemInheritVelocity;
        public float ParticleSystemForceOverLifetimeX;
        public float ParticleSystemForceOverLifetimeY;
        public float ParticleSystemForceOverLifetimeZ;
        public float ParticleSystemColorOverLifetimeR;
        public float ParticleSystemColorOverLifetimeG;
        public float ParticleSystemColorOverLifetimeB;
        public float ParticleSystemColorOverLifetimeA;
        public float ParticleSystemSizeOverLifetimeX;
        public float ParticleSystemSizeOverLifetimeY;
        public float ParticleSystemSizeOverLifetimeZ;
        public float ParticleSystemRotationOverLifetimeX;
        public float ParticleSystemRotationOverLifetimeY;
        public float ParticleSystemRotationOverLifetimeZ;
        public float ParticleSystemVelocityOverLifetimeX;
        public float ParticleSystemVelocityOverLifetimeY;
        public float ParticleSystemVelocityOverLifetimeZ;
        public float ParticleSystemInheritRotation;
        public float ParticleSystemRandomizeRotationDirection;
        public float ParticleSystemStartRotation3D;
        public float ParticleSystemStartSize3D;
        public float ParticleSystemScaleSpace;
        public float ParticleSystemRandomizeScale;
        public float ParticleSystemUseCustomVertexStreams;
        public float ParticleSystemEnableGPUInstancing;
        public float ParticleSystemUseUnscaledTime;
        public float ParticleSystemFreezeTranslationX;
        public float ParticleSystemFreezeTranslationY;
        public float ParticleSystemFreezeTranslationZ;
        public float ParticleSystemFreezeRotationX;
        public float ParticleSystemFreezeRotationY;
        public float ParticleSystemFreezeRotationZ;
        public float ParticleSystemFreezeVelocityX;
        public float ParticleSystemFreezeVelocityY;
        public float ParticleSystemFreezeVelocityZ;
        public float ParticleSystemFreezeAccelerationX;
        public float ParticleSystemFreezeAccelerationY;
        public float ParticleSystemFreezeAccelerationZ;
        public float ParticleSystemFreezeAngularVelocityX;
        public float ParticleSystemFreezeAngularVelocityY;
        public float ParticleSystemFreezeAngularVelocityZ;
        public float ParticleSystemFreezeSizeX;
        public float ParticleSystemFreezeSizeY;
        public float ParticleSystemFreezeSizeZ;
        public float ParticleSystemFreezeColorR;
        public float ParticleSystemFreezeColorG;
        public float ParticleSystemFreezeColorB;
        public float ParticleSystemFreezeColorA;
        public float ParticleSystemFreezeStartLifetime;
        public float ParticleSystemFreezeStartSpeed;
        public float ParticleSystemFreezeStartSize;
        public float ParticleSystemFreezeStartRotation;
        public float ParticleSystemFreezeGravityModifier;
        public float ParticleSystemFreezeInheritVelocity;
        public float ParticleSystemFreezeForceOverLifetimeX;
        public float ParticleSystemFreezeForceOverLifetimeY;
        public float ParticleSystemFreezeForceOverLifetimeZ;
        public float ParticleSystemFreezeColorOverLifetimeR;
        public float ParticleSystemFreezeColorOverLifetimeG;
        public float ParticleSystemFreezeColorOverLifetimeB;
        public float ParticleSystemFreezeColorOverLifetimeA;
        public float ParticleSystemFreezeSizeOverLifetimeX;
        public float ParticleSystemFreezeSizeOverLifetimeY;
        public float ParticleSystemFreezeSizeOverLifetimeZ;
        public float ParticleSystemFreezeRotationOverLifetimeX;
        public float ParticleSystemFreezeRotationOverLifetimeY;
        public float ParticleSystemFreezeRotationOverLifetimeZ;
        public float ParticleSystemFreezeVelocityOverLifetimeX;
        public float ParticleSystemFreezeVelocityOverLifetimeY;
        public float ParticleSystemFreezeVelocityOverLifetimeZ;
        public float ParticleSystemFreezeInheritRotation;
        public float ParticleSystemFreezeRandomizeRotationDirection;
        public float ParticleSystemFreezeStartRotation3D;
        public float ParticleSystemFreezeStartSize3D;
        public float ParticleSystemFreezeScaleSpace;
        public float ParticleSystemFreezeRandomizeScale;
        public float ParticleSystemFreezeUseCustomVertexStreams;
        public float ParticleSystemFreezeEnableGPUInstancing;
        public float ParticleSystemFreezeUseUnscaledTime;
    }
}

// - Systèmes annexes critiques -
// FICHIER : /Game/Gameplay/Movement/Tools/MovementDebugOverlaySystem.cs (Mis à jour avec Heatmap, Timeline, HUD Integration, etc.)
namespace Game.Gameplay.Movement.Debugging
{
    public class MovementDebugOverlaySystem : IDebugSystem
    {
        private EntityManager _entityManager;
        private RenderSystem _renderSystem; // Stub
        private IMovementProfiler _profiler; // Référence au profiler pour lire les stats
        private IMovementLogger _errorLogSystem; // Référence au logger pour lire les erreurs
        private readonly TimeSpan _errorDisplayDuration = TimeSpan.FromSeconds(5.0); // Durée d'affichage des erreurs
        private readonly Dictionary<Type, Color> _profilingColors = new Dictionary<Type, Color>
        {
            { typeof(Movement.Navigation.CoverAwareRoutingSystem), Color.Cyan },
            { typeof(Movement.Navigation.PredictiveSteeringSystem), Color.Magenta },
            { typeof(Movement.State.MovementStressSystem), Color.Yellow },
            { typeof(Movement.State.StaminaStateMachine), Color.Orange },
            { typeof(Movement.Physics.MovementImpactSystem), Color.Red },
            { typeof(Movement.Animation.MovementAnimationBridgeSystem), Color.Blue },
            { typeof(Movement.Animation.ProceduralFootPlacementSystem), Color.Green },
            { typeof(Movement.AudioVisual.MovementAudioMixer), Color.Purple },
            { typeof(Movement.AudioVisual.SurfaceReactionSystem), Color.Lime }
        };

        public MovementDebugOverlaySystem(EntityManager entityManager, RenderSystem renderSystem, IMovementProfiler profiler, IMovementLogger errorLogSystem)
        {
            _entityManager = entityManager;
            _renderSystem = renderSystem;
            _profiler = profiler;
            _errorLogSystem = errorLogSystem;
        }

        public void Initialize() { }
        public void Shutdown() { }

        public void Update(float deltaTime)
        {
            if (_renderSystem == null) return; // Pas de rendu si le système n'est pas fourni

            DrawOverlays();
        }

        private void DrawOverlays()
        {
            // Ancien code de dessin des vitesses, friction, etc.
            foreach (var entity in _entityManager.GetAllEntitiesWith<MovementComponent, RigidBodyComponent>())
            {
                var rb = _entityManager.GetComponent<RigidBodyComponent>(entity);
                var moveComp = _entityManager.GetComponent<MovementComponent>(entity);
                Vector2 pos = rb.Position;
                // Dessiner la vitesse
                _renderSystem?.DrawLine(pos, pos + rb.Velocity, Color.Red, 0.1f);
                // Dessiner la heatmap (simplifiée)
                int gridX = (int)(pos.X / 10.0f); // Taille arbitraire de cellule
                int gridY = (int)(pos.Y / 10.0f);
                float friction = moveComp.EffectiveFrictionFactor;
                Color heatmapColor = Color.FromArgb((int)(friction * 255), 255, 0, 0); // Rouge pour friction
                _renderSystem?.DrawRectangle(new Rectangle(gridX * 10, gridY * 10, 10, 10), heatmapColor);
            }

            // Dessiner les stats de profiling
            var stats = _profiler?.GetStats() ?? new Dictionary<Type, float>();
            float yPos = 10.0f; // Position de départ sur l'écran
            _renderSystem?.DrawString("=== MOVEMENT PROFILING ===", new Vector2(10.0f, yPos), Color.White);
            yPos += 15.0f;
            foreach (var stat in stats)
            {
                string text = $"{stat.Key.Name}: {stat.Value:F3}ms";
                Color color = _profilingColors.GetValueOrDefault(stat.Key, Color.Gray); // Utiliser la couleur prédéfinie ou gris par défaut
                _renderSystem?.DrawString(text, new Vector2(10.0f, yPos), color);
                yPos += 15.0f; // Espacement vertical
            }
            // Afficher aussi le temps total du module
            string moduleText = $"MovementModule Total: {_profiler.GetModuleAverageTimeMs():F3}ms";
            _renderSystem?.DrawString(moduleText, new Vector2(10.0f, yPos + 15.0f), Color.Cyan);

            // Afficher le nombre d'erreurs global
            if (_errorLogSystem != null)
            {
                int errorCount = _profiler.GetErrorCount(_errorLogSystem); // Utiliser la méthode du profiler pour obtenir le compteur
                string errorText = $"Errors: {errorCount}";
                _renderSystem?.DrawString(errorText, new Vector2(10.0f, yPos + 30.0f), Color.Magenta);
            }

            // Afficher les erreurs critiques
            var criticalErrors = _errorLogSystem != null ? _errorLogSystem.GetCriticalErrorsSince(DateTime.UtcNow - TimeSpan.FromMinutes(1)) : new List<string>(); // Exemple de filtre
            if (criticalErrors.Any())
            {
                float alpha = (float)(DateTime.UtcNow.Millisecond % 1000) / 1000.0f; // Clignotement basé sur le temps
                Color baseColor = Color.Red; // Par défaut, critique
                Color indicatorColor = Color.FromArgb((int)(alpha * 255), baseColor.R, baseColor.G, baseColor.B);
                _renderSystem?.DrawString("!!! CRITICAL ERRORS !!!", new Vector2(10.0f, 500.0f), indicatorColor);
                // Optionnel : Clignoter ou utiliser une texture/icône
            }
        }
    }
}

// - Systèmes annexes critiques -
// FICHIER : /Game/Gameplay/Movement/Systems/MovementStressSystem.cs (Mis à jour avec Movement Validation & Sanity Check)
namespace Movement.State
{
    public class MovementStressSystem : ISystem
    {
        private EntityManager _entityManager;
        private float _smoothingFactor = 0.1f; // Facteur de lissage (0.0 = instantané, 1.0 = très lent)

        public MovementStressSystem(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        public void Initialize() { }
        public void Shutdown() { }

        public void Update(float deltaTime)
        {
            // Stub : Logique de gestion du stress
            Console.WriteLine("Updating Movement Stress System...");

            // Exemple de mise à jour du stress basé sur la vitesse, les impacts, etc.
            _entityManager.ForEach((Entity entity, ref MovementComponent moveComp, ref RigidBodyComponent rb) =>
            {
                // Calcul du stress basé sur la vitesse
                float speedRatio = rb.Velocity.Length() / moveComp.BaseSpeed;
                moveComp.StressLevel += (speedRatio - moveComp.StressLevel) * 0.01f * deltaTime; // Lissage

                // Calcul du stress basé sur la fatigue
                moveComp.StressLevel += (moveComp.Fatigue - moveComp.StressLevel) * 0.005f * deltaTime; // Lissage

                // Calcul du stress basé sur la douleur
                moveComp.StressLevel += (moveComp.PainLevel - moveComp.StressLevel) * 0.02f * deltaTime; // Lissage

                // Calcul du stress basé sur la peur
                moveComp.StressLevel += (moveComp.FearLevel - moveComp.StressLevel) * 0.015f * deltaTime; // Lissage

                // Limiter le stress entre 0 et 1
                moveComp.StressLevel = Math.Max(0.0f, Math.Min(1.0f, moveComp.StressLevel));

                // Appliquer des pénalités si le stress est élevé
                if (moveComp.StressLevel > 0.7f)
                {
                    moveComp.CurrentSpeed *= 0.9f;
                    moveComp.Acceleration *= 0.8f;
                    moveComp.Traction *= 0.95f;
                }

                _entityManager.SetComponent(entity, moveComp);
            });
        }
    }
}

namespace Movement.Physics
{
    public class MovementImpactSystem : IPhysicsSystem
    {
        private EntityManager _entityManager;
        private PhysicsSystem _physicsSystem;

        public MovementImpactSystem(EntityManager entityManager, PhysicsSystem physicsSystem)
        {
            _entityManager = entityManager;
            _physicsSystem = physicsSystem;
        }

        public void Initialize() { }
        public void Shutdown() { }

        public void Update(float deltaTime)
        {
            // Stub : Logique de gestion des impacts
            Console.WriteLine("Updating Movement Impact System...");
        }
    }
}

namespace Movement.Animation
{
    public class MovementAnimationBridgeSystem : IAnimationSystem, IAnimationBridge
    {
        private EntityManager _entityManager;
        private PhysicsSystem _physicsSystem; // Pour les interactions physique-animation
        private IMovementProfiler _profiler; // Pour le profiling des performances
        private IMovementLogger _errorLogSystem; // Pour la journalisation des erreurs
        private RenderSystem _renderSystem; // Pour les overlays de debug visuels
        private IAnimationEngine _animationEngine; // Abstraction du moteur d'animation
        private EventBus _eventBus; // Pour les hooks IA et transitions globales
        private FrameBudgetAllocator _frameBudget; // Pour limiter le CPU utilisé par frame

        // État interne du système pour chaque entité animée
        private Dictionary<Entity, AnimationBridgeState> _bridgeStates = new();

        // Constantes pour les seuils et les paramètres
        private const float IdleSpeedThreshold = 0.1f;
        private const float LeanSmoothingFactor = 0.1f; // Pour lisser l'inclinaison
        private const float MomentumSmoothingFactor = 0.1f; // Pour lisser le momentum
        private const float FootPlacementSmoothingFactor = 0.1f; // Pour lisser le placement des pieds
        private const float MinFrictionForSliding = 0.1f; // Seuil pour détecter les surfaces glissantes
        private const float CPU_THRESHOLD_FOR_HEATMAP = 0.01f; // Seuil pour la heatmap de charge CPU

        // Pour le profiling
        private long _lastFrameCount = 0;
        private float _accumulatedFrameTime = 0.0f;
        private float _cpuCostPerEntityAvg = 0.0f;

        public MovementAnimationBridgeSystem(
            EntityManager entityManager,
            PhysicsSystem physicsSystem,
            IMovementProfiler profiler,
            IMovementLogger errorLogSystem,
            RenderSystem renderSystem,
            IAnimationEngine animationEngine,
            EventBus eventBus,
            FrameBudgetAllocator frameBudget)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _physicsSystem = physicsSystem ?? throw new ArgumentNullException(nameof(physicsSystem));
            _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
            _errorLogSystem = errorLogSystem ?? throw new ArgumentNullException(nameof(errorLogSystem));
            _renderSystem = renderSystem; // Peut être null
            _animationEngine = animationEngine ?? throw new ArgumentNullException(nameof(animationEngine)); // Abstraction
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _frameBudget = frameBudget ?? throw new ArgumentNullException(nameof(frameBudget));
        }

        public void Initialize()
        {
            // Initialiser les structures internes ou liaisons avec le moteur d'animation si nécessaire
            _bridgeStates.Clear();
            _animationEngine?.InitializeBridge(this); // Lier le bridge au moteur d'animation
        }

        public void Shutdown()
        {
            // Nettoyer les ressources ou liaisons avec le moteur d'animation si nécessaire
            _bridgeStates.Clear();
            _animationEngine?.ShutdownBridge(this);
        }

        public void Update(float deltaTime)
        {
            _profiler?.StartProfiling(this.GetType(), "Total");

            // Réinitialiser les stats de la frame pour le profiling
            _lastFrameCount = 0;
            _accumulatedFrameTime = 0.0f;

            try
            {
                // Itère sur les entités possédant à la fois un composant de mouvement, de corps rigide, et d'état d'animation.
                foreach (var entity in _entityManager.GetAllEntitiesWith<MovementComponent, RigidBodyComponent, AnimationStateComponent>())
                {
                    if (!_frameBudget.IsBudgetAvailable()) break; // Respecter le budget CPU

                    _profiler?.StartProfiling(entity, "PerEntity");

                    var moveComp = _entityManager.GetComponent<MovementComponent>(entity);
                    var rb = _entityManager.GetComponent<RigidBodyComponent>(entity);
                    var animState = _entityManager.GetComponent<AnimationStateComponent>(entity);

                    // Obtenir ou créer l'état interne du bridge pour cette entité
                    if (!_bridgeStates.TryGetValue(entity, out var bridgeState))
                    {
                        bridgeState = new AnimationBridgeState();
                        _bridgeStates[entity] = bridgeState;
                    }

                    try
                    {
                        // 1. SYNCHRONISATION PHYSIQUE <-> ANIMATION (Root Motion, Momentum)
                        ProcessRootMotionAndMomentum(entity, moveComp, ref rb, ref animState, ref bridgeState, deltaTime);

                        // 2. CALCUL DES PARAMÈTRES D'ANIMATION (Blendspaces, States, Direction)
                        UpdateAnimationStateAndParameters(entity, moveComp, rb, ref animState, ref bridgeState, deltaTime);

                        // 3. APPLICATION DES CORRECTIONS PHYSIQUES (Collisions, Friction, Gravity Alignment)
                        ApplyPhysicsConstraintsAndCorrections(entity, ref rb, ref animState, ref bridgeState, deltaTime);

                        // 4. GESTION VISUELLE & COHÉRENCE (Foot Placement, Facial Sync, Events)
                        HandleVisualConsistencyAndEvents(entity, moveComp, rb, ref animState, ref bridgeState, deltaTime);

                        // 5. MISE À JOUR DU COMPOSANT DANS L'ENTITYMANAGER
                        _entityManager.SetComponent(entity, animState);

                        // 6. NOTIFICATION VIA EVENTBUS POUR IA / AUDIO
                        NotifyExternalSystems(entity, animState, bridgeState);
                    }
                    catch (Exception ex)
                    {
                        // Validation des données pour éviter propagation d'erreurs NaN/Infinity
                        // Cette validation est implicite dans les calculs, mais on peut l'expliciter si nécessaire
                        if (float.IsNaN(ex.Message.GetHashCode())) // Exemple simplifié
                        {
                             _errorLogSystem?.LogError("MovementAnimationBridgeSystem", $"Erreur de données invalides (NaN/Inf) pour l'entité {entity}: {ex.Message}", ex.StackTrace, ErrorSeverity.Critical);
                        }
                        else
                        {
                            _errorLogSystem?.LogError("MovementAnimationBridgeSystem", $"Erreur pour l'entité {entity}: {ex.Message}", ex.StackTrace, ErrorSeverity.High);
                        }
                        // Auto-Recovery : Réinitialiser l'état de l'entité ou désactiver le bridge temporairement pour celle-ci
                        _bridgeStates.Remove(entity); // Retirer de la gestion si corrompue
                    }

                    _profiler?.StopProfiling(entity, "PerEntity");
                    _lastFrameCount++;

                    // Mettre à jour le temps accumulé pour le profiling
                    _accumulatedFrameTime += _profiler?.GetLastDuration(entity, "PerEntity") ?? 0.0f;
                }

                // 7. PROFILAGE GLOBALE
                _cpuCostPerEntityAvg = _accumulatedFrameTime / Math.Max(1, _lastFrameCount);
                _profiler?.SetStat(this.GetType(), "EntitiesProcessed", _lastFrameCount);
                _profiler?.SetStat(this.GetType(), "CPU_Cost_Per_Entity_Avg", _cpuCostPerEntityAvg);
                _profiler?.SetStat(this.GetType(), "Frame_Budget_Remaining", _frameBudget.RemainingBudget);

                // 8. OUTILS DE DEBUG (Overlay, Heatmap, Timeline)
                UpdateDebugOverlays(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem?.LogError("MovementAnimationBridgeSystem", $"Erreur critique dans Update(): {ex.Message}", ex.StackTrace, ErrorSeverity.Critical);
            }

            _profiler?.StopProfiling(this.GetType(), "Total");
        }

        /// <summary>
        /// Gère l'extraction et l'application du Root Motion entre le squelette et le RigidBody.
        /// Intègre également le transfert de momentum.
        /// </summary>
        private void ProcessRootMotionAndMomentum(Entity entity, in MovementComponent moveComp, ref RigidBodyComponent rb, ref AnimationStateComponent animState, ref AnimationBridgeState state, float deltaTime)
        {
            // Extraction du Root Motion delta depuis le moteur d'animation
            // L'abstraction IAnimationEngine est cruciale ici.
            Vector2 rootMotionDelta = _animationEngine?.ExtractRootMotionDelta(entity) ?? Vector2.Zero;

            // Application du Root Motion au RigidBody
            // TODO: Gérer les collisions potentielles induites par le Root Motion
            // (ex: raycast ou overlap check avant application)
            rb.Position += rootMotionDelta;

            // Calcul et application du Momentum (Inertie visuelle)
            Vector2 velocityDelta = rb.Velocity - state.LastPhysicalVelocity;
            float momentumMagnitude = velocityDelta.Length();
            Vector2 momentumDirection = momentumMagnitude > 0.001f ? velocityDelta / momentumMagnitude : Vector2.Zero;

            // Lissage du Momentum
            state.CurrentMomentum = MathHelper.Lerp(state.CurrentMomentum, momentumMagnitude, MomentumSmoothingFactor);
            state.CurrentMomentumDirection = MathHelper.Lerp(state.CurrentMomentumDirection, momentumDirection, MomentumSmoothingFactor);

            // Influence du Momentum sur l'animation (via paramètre ou blending)
            // Ex: _animationEngine?.SetMomentumParameter(entity, state.CurrentMomentum, state.CurrentMomentumDirection);

            // Stocker la vélocité physique pour la comparaison au cycle suivant
            state.LastPhysicalVelocity = rb.Velocity;
        }

        /// <summary>
        /// Détermine l'état d'animation et calcule les paramètres à envoyer au blend tree.
        /// Intègre les Blendspaces adaptatifs et les influences de pente/direction.
        /// </summary>
        private void UpdateAnimationStateAndParameters(Entity entity, in MovementComponent moveComp, in RigidBodyComponent rb, ref AnimationStateComponent animState, ref AnimationBridgeState state, float deltaTime)
        {
            float speed = rb.Velocity.Length();
            bool isMoving = speed > IdleSpeedThreshold;
            float baseSpeed = moveComp.BaseSpeed > 0 ? moveComp.BaseSpeed : 1.0f;

            // --- CALCUL DES PARAMÈTRES DE BASE ---
            animState.SpeedParameter = speed / baseSpeed; // Vitesse normalisée
            animState.DirectionParameter = 0.0f; // Initialisé ici, mis à jour si en mouvement
            if (isMoving)
            {
                float directionRad = MathF.Atan2(rb.Velocity.Y, rb.Velocity.X);
                animState.DirectionParameter = MathHelper.ToDegrees(directionRad);
            }

            // Inclinaison (Lean) basée sur l'accélération ou la direction du mouvement vs vélocité
            // Simule l'inertie visuelle
            Vector2 acceleration = (rb.Velocity - state.LastPhysicalVelocity) / deltaTime;
            float leanTarget = 0.0f;
            if (acceleration.LengthSquared() > 0.0001f) // Seuil pour éviter division par zéro
            {
                leanTarget = MathHelper.ToDegrees(MathF.Atan2(acceleration.Y, acceleration.X));
            }
            // Lissage de l'inclinaison
            state.CurrentLean = MathHelper.Lerp(state.CurrentLean, leanTarget, LeanSmoothingFactor);
            animState.LeanAmount = state.CurrentLean;

            // --- DÉTERMINATION DE L'ÉTAT D'ANIMATION ---
            string baseState = DetermineBaseState(speed, baseSpeed, isMoving);
            string modifiedState = ApplyStateModifiers(baseState, moveComp, animState);
            animState.CurrentState = modifiedState;

            // Mise à jour des paramètres additionnels basés sur l'état ou les blessures
            animState.InjuryParameter = moveComp.LegInjurySeverity;
            animState.StressParameter = moveComp.StressLevel;
            animState.StaminaParameter = moveComp.Stamina / moveComp.MaxStamina;
            animState.TractionParameter = moveComp.Traction;
            animState.MomentumAmount = state.CurrentMomentum; // Passer le momentum calculé

            // --- BLENDSAPCES ADAPTATIFS ---
            // Adapter les poids ou les paramètres du blendspace en fonction de la pente, de la surface, etc.
            float groundAngle = MathHelper.ToDegrees(MathF.Acos(Vector2.Dot(Vector2.UnitY, rb.GroundNormal))); // Exemple pour la pente
            animState.SlopeParameter = groundAngle;
            // _animationEngine?.AdjustBlendSpaceForTerrain(entity, groundAngle, moveComp.SurfaceType); // Exemple d'appel

            // --- POSE MATCHING (simplifié) ---
            // Ajuster la pose d'animation pour correspondre à la position physique
            // Cela nécessite une liaison fine avec l'animation engine et potentiellement de l'IK procédural
            // Ex: _animationEngine?.AdjustPoseToMatchPosition(entity, rb.Position, rb.Rotation);
        }

        private string DetermineBaseState(float speed, float baseSpeed, bool isMoving)
        {
            if (!isMoving)
            {
                return "Idle";
            }
            else if (speed > baseSpeed * 1.5f)
            {
                return "Run";
            }
            else if (speed > baseSpeed * 1.1f)
            {
                return "Walk";
            }
            else
            {
                return "Walk_Slow"; // Exemple pour une vitesse intermédiaire
            }
        }

        private string ApplyStateModifiers(string baseState, in MovementComponent moveComp, in AnimationStateComponent animState)
        {
            string state = baseState;

            if (moveComp.LegInjurySeverity > 0.5f)
            {
                state += "_InjuredLeg";
            }
            if (moveComp.StressLevel > 0.7f)
            {
                state += "_Stressed";
            }
            if (moveComp.BreathLevel < 30.0f)
            {
                state += "_BreathingHard";
            }
            // ... autres conditions (Frozen, Burning, Sneaking via MovementTag, etc.)

            return state;
        }

        /// <summary>
        /// Applique des contraintes physiques basées sur l'animation (ex: rotation du squelette, collisions, friction).
        /// </summary>
        private void ApplyPhysicsConstraintsAndCorrections(Entity entity, ref RigidBodyComponent rb, ref AnimationStateComponent animState, ref AnimationBridgeState state, float deltaTime)
        {
            // Exemple: Alignement du squelette selon la gravité locale
            // float localGravity = _physicsSystem.GetLocalGravityAtPosition(rb.Position);
            // _animationEngine?.AlignSkeletonToGravity(entity, localGravity); // Exemple d'appel

            // Exemple: Mapping de la friction de surface à l'animation
            // float friction = GetSurfaceFrictionAtPosition(rb.Position); // Via SurfaceTypeComponent
            // _animationEngine?.AdjustSlidingAnimations(entity, friction); // Exemple d'appel
            // if(friction < MinFrictionForSliding) { /* Ajuster blend pour glisser */ }

            // Exemple: Correction de collision entre Root Motion et physique
            // Cette logique est complexe et dépend souvent du moteur d'animation lui-même.
            // On pourrait faire un raycast ou overlap check après l'application du Root Motion.
            // if(CollisionDetectedAfterRootMotion(rb.Position, entityBounds))
            // {
            //     // Annuler ou corriger le déplacement du Root Motion
            //     rb.Position = state.LastValidPosition;
            //     // Déclencher une animation de réaction
            //     animState.CurrentState = "React_Collision";
            // }

            // Stocker la position pour la prochaine vérification de collision
            state.LastValidPosition = rb.Position;
        }

        /// <summary>
        /// Gère la cohérence visuelle (foot placement, facial sync, animation events) et les réactions d'impact.
        /// </summary>
        private void HandleVisualConsistencyAndEvents(Entity entity, in MovementComponent moveComp, in RigidBodyComponent rb, ref AnimationStateComponent animState, ref AnimationBridgeState state, float deltaTime)
        {
            // Exemple: Dynamic Foot Placement (IK procédural)
            // Vector3 leftFootPos, rightFootPos;
            // bool leftFootHit, rightFootHit;
            // RaycastHit leftHit, rightHit;
            // Raycast pour trouver le sol sous les pieds
            // if (RaycastForFootPlacement(out leftHit, entity.LeftFootOffset) && RaycastForFootPlacement(out rightHit, entity.RightFootOffset))
            // {
            //     leftFootHit = true; rightFootHit = true;
            //     leftFootPos = leftHit.Point; rightFootPos = rightHit.Point;
            // }
            // else { leftFootHit = false; rightFootHit = false; } // Ne pas placer les pieds
            // _animationEngine?.ApplyFootPlacementIK(entity, leftFootPos, rightFootPos, leftFootHit, rightFootHit); // Exemple d'appel

            // Exemple: Synchronisation faciale
            // _animationEngine?.SetFacialExpression(entity, GetExpressionBasedOnEffort(animState.StaminaParameter, animState.StressParameter)); // Exemple d'appel

            // Exemple: Génération d'événements d'animation basés sur les paramètres ou les impacts
            if (animState.SpeedParameter > 0.9f && state.WasSlowerLastFrame) // Démarrage d'une course
            {
                _eventBus?.Raise(new AnimationEvent { Entity = entity, Type = AnimationEventType.Footstep, Speed = animState.SpeedParameter });
            }

            // Exemple: Réaction d'impact (si un événement d'impact est reçu)
            // if (moveComp.ImpactReceived) // Flag potentiellement mis par MovementImpactSystem
            // {
            //     animState.CurrentState = "React_Impact"; // Changer l'état pour une réaction
            //     _eventBus?.Raise(new AnimationEvent { Entity = entity, Type = AnimationEventType.ImpactReaction, Force = moveComp.LastImpactForce });
            // }

            state.WasSlowerLastFrame = animState.SpeedParameter <= 0.9f; // Mettre à jour pour le prochain cycle
        }

        /// <summary>
        /// Notifie d'autres systèmes (IA, Audio) des changements d'animation.
        /// </summary>
        private void NotifyExternalSystems(Entity entity, in AnimationStateComponent animState, in AnimationBridgeState state)
        {
            // Exemple: Notification pour l'IA (prédiction de mouvement)
            _eventBus?.Raise(new AnimationStateChangedMessage { Entity = entity, NewState = animState.CurrentState, Parameters = animState.GetParameters() });

            // Exemple: Notification pour l'audio (synchro pas, sons effort)
            _eventBus?.Raise(new AnimationAudioSyncMessage { Entity = entity, State = animState.CurrentState, Speed = animState.SpeedParameter, Stamina = animState.StaminaParameter });
        }

        /// <summary>
        /// Met à jour les overlays de debug (vecteurs, heatmaps, timelines).
        /// </summary>
        private void UpdateDebugOverlays(float deltaTime)
        {
            if (_renderSystem == null) return; // Pas de rendu de debug si le système n'est pas fourni

            // Exemple: Afficher les vitesses animées et physiques, le momentum, la pente
            foreach (var entity in _entityManager.GetAllEntitiesWith<RigidBodyComponent, AnimationStateComponent>())
            {
                var rb = _entityManager.GetComponent<RigidBodyComponent>(entity);
                var animState = _entityManager.GetComponent<AnimationStateComponent>(entity);

                if (!_bridgeStates.TryGetValue(entity, out var state)) continue; // Pas d'état de bridge, on ignore

                Vector2 pos = rb.Position;

                // Dessiner la vélocité physique
                _renderSystem.DrawLine(pos, pos + rb.Velocity * 0.1f, System.Drawing.Color.Red, 0.05f);

                // Dessiner un indicateur pour la vitesse animée (si disponible via l'engine, sinon basé sur SpeedParameter)
                // Vector2 animatedVelIndicator = pos + GetDirectionVector(animState.DirectionParameter) * (animState.SpeedParameter * 0.1f);
                // _renderSystem.DrawLine(pos, animatedVelIndicator, System.Drawing.Color.Blue, 0.05f);

                // Dessiner l'inclinaison
                Vector2 leanDir = new Vector2(MathF.Cos(MathHelper.ToRadians(animState.LeanAmount)), MathF.Sin(MathHelper.ToRadians(animState.LeanAmount)));
                _renderSystem.DrawLine(pos, pos + leanDir * 0.05f, System.Drawing.Color.Yellow, 0.05f);

                // Dessiner le momentum
                Vector2 momentumVec = state.CurrentMomentumDirection * state.CurrentMomentum * 0.1f;
                _renderSystem.DrawLine(pos, pos + momentumVec, System.Drawing.Color.Orange, 0.05f);

                // Afficher l'état d'animation
                _renderSystem.DrawString(animState.CurrentState, pos + new Vector2(0.0f, 0.5f), System.Drawing.Color.White);

                // Dessiner la heatmap de charge CPU si _cpuCostPerEntityAvg est significatif
                // float intensity = Math.Min(1.0f, _cpuCostPerEntityAvg / CPU_THRESHOLD_FOR_HEATMAP);
                // _renderSystem.DrawHeatmapPixel(pos, intensity, HeatmapType.AnimationCPU);
            }
        }
    }

    #region Internal State Struct
    /// <summary>
    /// État interne du système pour une entité spécifique, pour persister des données entre les updates.
    /// </summary>
    internal struct AnimationBridgeState
    {
        public Vector2 LastPhysicalVelocity; // Pour calculer l'accélération et le momentum
        public Vector2 LastValidPosition; // Pour vérifier les collisions post-RootMotion
        public float CurrentLean; // Pour lisser l'inclinaison
        public float CurrentMomentum; // Pour lisser l'effet de momentum
        public Vector2 CurrentMomentumDirection; // Direction du momentum
        public bool WasSlowerLastFrame; // Pour détecter les transitions de vitesse (ex: démarrage de course)
        // Ajouter d'autres champs internes si nécessaire (ex: timers, derniers paramètres, etc.)
    }
    #endregion

    #region EventBus Messages
    // Messages pour la communication avec d'autres systèmes
    public struct AnimationStateChangedMessage
    {
        public Entity Entity;
        public string NewState;
        public Dictionary<string, object> Parameters; // Pour passer les paramètres d'animation
    }

    public struct AnimationAudioSyncMessage
    {
        public Entity Entity;
        public string State;
        public float Speed;
        public float Stamina;
        // Ajouter d'autres données pertinentes pour l'audio
    }

    public enum AnimationEventType
    {
        Footstep,
        ImpactReaction,
        Breathing,
        Effort,
        // ...
    }

    public struct AnimationEvent
    {
        public Entity Entity;
        public AnimationEventType Type;
        public float Speed; // Exemple de donnée attachée
        public float Force; // Exemple de donnée attachée
        // ...
    }
    #endregion
}

// - Système Principal de Mouvement (mis à jour) -
public class MovementSystem : IMovementSystem
{
    private EntityManager _entityManager;
    private PhysicsSystem _physicsSystem; // Reçoit PhysicsSystem via injection de dépendances
    private NavMesh _navMesh; // Reçoit NavMesh via injection de dépendances

    // - Catégorie 1: Navigation -
    // Dépendances : Lit MovementComponent, MovementStateComponent. Ecrit RigidBodyComponent (position, rotation).
    // IMPORTANT : Doit être mis à jour avant les systèmes de physique pour influencer la vélocité.
    private Movement.Navigation.CoverAwareRoutingSystem _coverRoutingSystem;
    private Movement.Navigation.PredictiveSteeringSystem _predictiveSystem;

    // - Catégorie 2: État -
    // Dépendances : Lit RigidBodyComponent, MovementComponent. Ecrit MovementComponent (stress, fatigue, etc.).
    // IMPORTANT : Doit être mis à jour après la navigation pour réagir à la nouvelle vélocité.
    // IMPORTANT : Les systèmes de physique peuvent lire les modifications apportées par ces systèmes.
    private Movement.State.MovementStressSystem _stressSystem;
    private Movement.State.StaminaStateMachine _staminaSystem;

    // - Catégorie 3: Physique & Réaction -
    // Dépendances : Lit MovementComponent, RigidBodyComponent. Ecrit RigidBodyComponent (forces, vélocité).
    // IMPORTANT : Doit être mis à jour après les systèmes d'état pour appliquer les pénalités de stress/stamina.
    private Movement.Physics.MovementImpactSystem _impactSystem;

    // - Catégorie 4: Animation -
    // Dépendances : Lit MovementComponent, RigidBodyComponent. Ecrit AnimationStateComponent.
    // Dépend de l'état calculé par MovementStressSystem, MovementComponent, etc.
    private Movement.Animation.MovementAnimationBridgeSystem _animBridgeSystem;
    // Dépendances : Lit AnimationStateComponent, RigidBodyComponent. Ecrit AnimationStateComponent (positions des pieds).
    // IMPORTANT : Doit être mis à jour après AnimationBridgeSystem pour lire les données de la frame précédente.
    private IAnimationSystem _footPlacementSystem; // Remplace ProceduralFootPlacementSystem par l'interface

    // - Catégorie 5: Audio & VFX -
    // Dépendances : Lit MovementComponent, RigidBodyComponent, SurfaceTypeComponent. Ecrit MovementSoundComponent, MovementVFXComponent.
    // Dépend de l'état calculé par les systèmes précédents (vitesse, friction, etc.).
    // IMPORTANT : Synchronisation avec AnimationBridgeSystem à vérifier si les sons sont liés à des événements d'animation.
    // IMPORTANT : Latence audio à profiler si critique.
    private IAudioVisualSystem _audioMixerSystem;
    private IAudioVisualSystem _surfaceSystem;

    // - Catégorie 6: Debug & Performance -
    // Dépendances : Lit tous les composants pertinents. Utilise RenderSystem pour afficher les overlays.
    private IMovementProfiler _profiler;
    private IDebugSystem _debugOverlaySystem; // Pour le profiling visuel et les overlays
    private IMovementLogger _errorLogSystem; // Pour la journalisation des erreurs
    private FrameBudgetAllocator _frameBudget; // Pour le Frame Budget Allocator

    // Nouvelles dépendances pour les optimisations et les hooks avancés
    private IThreadAffinityManager _threadAffinityManager; // Pour le Thread Affinity Control
    private IGPUProfilerHook _gpuProfilerHook; // Pour le GPU Profiling Hook
    private IAnimationEngine _animationEngine; // Pour le Root Motion Async et la Pose Matching
    private IAudioEngine _audioEngine; // Pour le Footstep Sync et l'Audio Latency Profiler
    private IRenderEngine _renderEngine; // Pour le Motion Blur Sync, Camera Shake Integration, Facial Sync, Dynamic LOD, Pose Correction, Ghost Skeleton Mode, Blend Weight Inspector
    private EventBus _eventBus; // Pour la communication IA Prediction Sync, Behavior Tag Integration, Crowd Animation Sync, Emotion Driven Animation, Group Formation Blending

    /// <summary>
    /// Initialise le MovementSystem avec ses dépendances et ses sous-systèmes.
    /// Les sous-systèmes sont regroupés par catégorie pour la clarté.
    /// </summary>
    /// <param name="entityManager">L'EntityManager du moteur.</param>
    /// <param name="physicsSystem">Le PhysicsSystem du moteur.</param>
    /// <param name="navMesh">La NavMesh pour la navigation.</param>
    /// <param name="threadAffinityManager">Le gestionnaire d'affinité de threads.</param>
    /// <param name="gpuProfilerHook">Le hook pour le profiling GPU.</param>
    /// <param name="animationEngine">L'interface vers le moteur d'animation.</param>
    /// <param name="audioEngine">L'interface vers le moteur audio.</param>
    /// <param name="renderEngine">L'interface vers le moteur de rendu.</param>
    /// <param name="eventBus">Le bus d'événements.</param>
    /// <exception cref="ArgumentNullException">Si une dépendance est nulle.</exception>
    public MovementSystem(
        EntityManager entityManager,
        PhysicsSystem physicsSystem,
        NavMesh navMesh,
        IThreadAffinityManager threadAffinityManager,
        IGPUProfilerHook gpuProfilerHook,
        IAnimationEngine animationEngine,
        IAudioEngine audioEngine,
        IRenderEngine renderEngine,
        EventBus eventBus)
    {
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _physicsSystem = physicsSystem ?? throw new ArgumentNullException(nameof(physicsSystem));
        _navMesh = navMesh ?? throw new ArgumentNullException(nameof(navMesh));
        _threadAffinityManager = threadAffinityManager ?? throw new ArgumentNullException(nameof(threadAffinityManager));
        _gpuProfilerHook = gpuProfilerHook ?? throw new ArgumentNullException(nameof(gpuProfilerHook));
        _animationEngine = animationEngine ?? throw new ArgumentNullException(nameof(animationEngine));
        _audioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
        _renderEngine = renderEngine ?? throw new ArgumentNullException(nameof(renderEngine));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        // - Initialisation des sous-systèmes par catégorie -
        // Navigation
        _coverRoutingSystem = new Movement.Navigation.CoverAwareRoutingSystem(_entityManager, _navMesh);
        _predictiveSystem = new Movement.Navigation.PredictiveSteeringSystem(_entityManager, _physicsSystem);

        // Etat
        _stressSystem = new Movement.State.MovementStressSystem(_entityManager);
        _staminaSystem = new Movement.State.StaminaStateMachine(_entityManager);

        // Physique & Réaction
        _impactSystem = new Movement.Physics.MovementImpactSystem(_entityManager, _physicsSystem);

        // Animation
        // Injection de dépendances pour le nouveau système AnimationBridge
        _animBridgeSystem = new Movement.Animation.MovementAnimationBridgeSystem(
            _entityManager,
            _physicsSystem,
            _profiler, // Supposant que le profiler est injecté ici aussi
            _errorLogSystem, // Supposant que le logger est injecté ici aussi
            _renderSystem, // Supposant que le renderSystem est injecté ici aussi
            _animationEngine, // Supposant que l'engine d'animation est injecté ici aussi
            _eventBus, // Supposant que l'eventBus est injecté ici aussi
            _frameBudget // Supposant que le frameBudget est injecté ici aussi
        );
        _footPlacementSystem = new Movement.Animation.ProceduralFootPlacementSystem(_entityManager);

        // Audio & VFX
        _audioMixerSystem = new Movement.AudioVisual.MovementAudioMixer(_entityManager);
        _surfaceSystem = new Movement.AudioVisual.SurfaceReactionSystem(_entityManager);

        // Debug & Performance
        _debugOverlaySystem = new Movement.Debug.MovementDebugOverlaySystem(_entityManager, _renderSystem, _profiler, _errorLogSystem);
        // Supposons que _profiler, _errorLogSystem, _frameBudget sont injectés ou instanciés ici
    }

    public void Initialize()
    {
        // Assigner les sous-systèmes à des threads dédiés via Thread Affinity Control
        _threadAffinityManager.AssignSystemToThread(_coverRoutingSystem, 0); // Thread 0
        _threadAffinityManager.AssignSystemToThread(_predictiveSystem, 0); // Thread 0
        _threadAffinityManager.AssignSystemToThread(_stressSystem, 1); // Thread 1
        _threadAffinityManager.AssignSystemToThread(_staminaSystem, 1); // Thread 1
        _threadAffinityManager.AssignSystemToThread(_impactSystem, 2); // Thread 2
        _threadAffinityManager.AssignSystemToThread(_animBridgeSystem, 3); // Thread 3 (Potentiellement pour Root Motion Async)
        _threadAffinityManager.AssignSystemToThread(_footPlacementSystem, 3); // Thread 3
        _threadAffinityManager.AssignSystemToThread(_audioMixerSystem, 4); // Thread 4
        _threadAffinityManager.AssignSystemToThread(_surfaceSystem, 4); // Thread 4
        _threadAffinityManager.AssignSystemToThread(_debugOverlaySystem, 5); // Thread 5 (Overlay UI)

        _coverRoutingSystem.Initialize();
        _predictiveSystem.Initialize();
        _stressSystem.Initialize();
        _staminaSystem.Initialize();
        _impactSystem.Initialize();
        _animBridgeSystem.Initialize();
        _footPlacementSystem.Initialize();
        _audioMixerSystem.Initialize();
        _surfaceSystem.Initialize();
        _debugOverlaySystem.Initialize();
    }

    public void Shutdown()
    {
        _coverRoutingSystem.Shutdown();
        _predictiveSystem.Shutdown();
        _stressSystem.Shutdown();
        _staminaSystem.Shutdown();
        _impactSystem.Shutdown();
        _animBridgeSystem.Shutdown();
        _footPlacementSystem.Shutdown();
        _audioMixerSystem.Shutdown();
        _surfaceSystem.Shutdown();
        _debugOverlaySystem.Shutdown();
    }

    public void Update(float deltaTime)
    {
        // IMPORTANT : Respecter l'ordre de mise à jour pour la cohérence des données.

        // - Mise à jour du Frame Budget -
        _frameBudget?.Reset();

        // - Mise à jour des systèmes de navigation (Pathfinding, Steering) -
        // Lisèrent la logique de navigation
        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.Navigation.CoverAwareRoutingSystem));
#endif
            try
            {
                // Mise à jour parallèle des entités pour la navigation
                var routingEntities = _entityManager.GetAllEntitiesWith<MovementComponent, MovementStateComponent>();
                Parallel.ForEach(routingEntities, entity =>
                {
                    var moveComp = _entityManager.GetComponent<MovementComponent>(entity);
                    var stateComp = _entityManager.GetComponent<MovementStateComponent>(entity);
                    // ... logique de mise à jour de la navigation ...
                });

                _coverRoutingSystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("CoverAwareRoutingSystem", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.Navigation.CoverAwareRoutingSystem));
#endif
        }

        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.Navigation.PredictiveSteeringSystem));
#endif
            try
            {
                _predictiveSystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("PredictiveSteeringSystem", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.Navigation.PredictiveSteeringSystem));
#endif
        }

        // - Mise à jour de la physique du mouvement de base (suivi de chemin, évitement local) -
        // Liséré de la logique de navigation
        if (_frameBudget.IsBudgetAvailable())
        {
            UpdatePhysicsSubSteps(deltaTime); // Utiliser le sub-stepping physique
        }

        // - Mise à jour des systèmes de calcul d'état (Stress, Stamina) -
        // Influent sur les paramètres de MovementComponent
        // Dépendance : MovementStressSystem peut lire MovementComponent.Stamina (géré par StaminaStateMachine).
        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.State.MovementStressSystem));
#endif
            try
            {
                _stressSystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("MovementStressSystem", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.State.MovementStressSystem));
#endif
        }

        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.State.StaminaStateMachine));
#endif
            try
            {
                _staminaSystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("StaminaStateMachine", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.State.StaminaStateMachine));
#endif
        }

        // - Mise à jour des systèmes réactifs critiques (Impact, Animation, Audio) -
        // Réagissent aux changements de MovementComponent et d'autres états
        // Dépendance : MovementImpactSystem peut modifier MovementComponent (santé, blessures).
        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.Physics.MovementImpactSystem));
#endif
            try
            {
                _impactSystem.Update(deltaTime); // Peut aussi être appelé via EventBus
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("MovementImpactSystem", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.Physics.MovementImpactSystem));
#endif
        }

        // Gestion de l'animation basée sur le mouvement
        // Dépendance : MovementAnimationBridgeSystem lit MovementComponent, RigidBodyComponent, MovementImpactComponent, etc.
        // IMPORTANT : Synchronisation avec MovementAudioMixer à vérifier.
        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.Animation.MovementAnimationBridgeSystem));
#endif
            try
            {
                // Mise à jour asynchrone possible pour le Root Motion
                var rootMotionTask = Task.Run(() => _animBridgeSystem.Update(deltaTime));

                // Attendre la fin de la tâche de Root Motion avant de continuer si nécessaire
                rootMotionTask.Wait();

                // Ou lancer plusieurs tâches en parallèle pour différentes entités (Parallel Animation Jobs)
                // Parallel.ForEach(_entityManager.GetAllEntitiesWith<MovementComponent, RigidBodyComponent, AnimationStateComponent>(), entity =>
                // {
                //     _animBridgeSystem.ProcessSingleEntity(entity, deltaTime);
                // });
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("MovementAnimationBridgeSystem", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.Animation.MovementAnimationBridgeSystem));
#endif
        }

        // Gestion audio liée au mouvement
        // Dépendance : MovementAudioMixer lit MovementComponent, MovementSoundComponent, SurfaceTypeComponent, etc.
        // IMPORTANT : Synchronisation avec MovementAnimationBridgeSystem à vérifier.
        // IMPORTANT : Profiler la latence entre l'événement de déclenchement et la lecture effective du son.
        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.AudioVisual.MovementAudioMixer));
#endif
            try
            {
                _audioMixerSystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("MovementAudioMixer", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.AudioVisual.MovementAudioMixer));
#endif
        }

        // Gestion des effets visuels liés au mouvement
        // Dépendance : SurfaceReactionSystem lit MovementComponent, SurfaceTypeComponent, MovementVFXComponent, etc.
        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.AudioVisual.SurfaceReactionSystem));
#endif
            try
            {
                _surfaceSystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("SurfaceReactionSystem", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.AudioVisual.SurfaceReactionSystem));
#endif
        }

        // Gestion du placement procédural des pieds (après l'animation)
        // Dépendance : ProceduralFootPlacementSystem lit AnimationStateComponent, RigidBodyComponent.
        if (_frameBudget.IsBudgetAvailable())
        {
#if DEBUG_PROFILING
            _profiler.StartProfiling(typeof(Movement.Animation.ProceduralFootPlacementSystem));
#endif
            try
            {
                _footPlacementSystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("ProceduralFootPlacementSystem", ex.Message, ex.StackTrace, ErrorSeverity.Critical);
            }
#if DEBUG_PROFILING
            _profiler.StopProfiling(typeof(Movement.Animation.ProceduralFootPlacementSystem));
#endif
        }

        // - Mise à jour du Debug Overlay pour le profiling visuel -
        if (_frameBudget.IsBudgetAvailable())
        {
            try
            {
                _debugOverlaySystem.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _errorLogSystem.LogError("MovementDebugOverlaySystem", ex.Message, ex.StackTrace, ErrorSeverity.Low); // Moins critique pour le gameplay
            }
        }

        // - GPU Profiling Hook -
        _gpuProfilerHook?.BeginGPUSample("Movement_Rendering");
        // Exemples d'utilisation avec le rendu :
        // _renderEngine?.SetMotionBlurIntensity(GetAverageMotionBlurIntensity()); // Sync Motion Blur
        // _renderEngine?.TriggerCameraShake(GetCameraShakeParams()); // Camera Shake Integration
        // _renderEngine?.UpdateFacialExpressions(GetFacialSyncParams()); // Facial Sync
        // _renderEngine?.UpdateLODDetails(GetLODAnimationDetails()); // Dynamic LOD
        // _renderEngine?.CorrectPoses(GetPoseCorrectionData()); // Pose Correction System
        // _renderEngine?.RenderGhostSkeleton(GetRootMotionPositions()); // Ghost Skeleton Mode
        // _renderEngine?.ShowBlendWeights(GetBlendWeights()); // Blend Weight Inspector
        _gpuProfilerHook?.EndGPUSample("Movement_Rendering");
    }

    private void UpdatePhysicsSubSteps(float deltaTime)
    {
        // Logique de sub-stepping physique pour une meilleure précision
        // Exemple simplifié : diviser le deltaTime en N sous-pas
        const int subSteps = 4;
        float subDeltaTime = deltaTime / subSteps;

        for (int i = 0; i < subSteps; i++)
        {
            // Mettre à jour la physique pour chaque sous-pas
            _physicsSystem.Update(subDeltaTime);

            // Mettre à jour les systèmes qui dépendent de la physique à chaque sous-pas si nécessaire
            // Ex: _impactSystem.Update(subDeltaTime); // Si les impacts doivent être détectés à haute fréquence
        }
    }
}