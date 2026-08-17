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

// Les 22 types que ce fichier declarait introuvables existaient a 21, et
// plusieurs dans le fichier LUI-MEME : il declare ses types dans six espaces de
// noms differents, qui ne se voient donc pas les uns les autres. Ce n'etait pas
// un probleme de types manquants mais d'imports.
using Snake2000.Engine.Core;    // Entity, EntityManager, EventBus, IComponent, ISystem, Vector2
using Snake2000.Engine.AI;      // IReusableComponent, NavMesh
using Snake2000.Engine.Physics; // PhysicsSystem, RigidBodyComponent
using Engine.Rendering;         // IRenderEngine
using Game.Gameplay.Movement.Animation;    // IAnimationSystem
using Game.Gameplay.Movement.Physics;      // IPhysicsSystem
using Game.Gameplay.Movement.AudioVisual;  // IAudioVisualSystem
using Game.Gameplay.Movement.Debugging;    // IDebugSystem
using Game.Gameplay.Movement.Tools;        // FrameBudgetAllocator, IMovementLogger, IMovementProfiler
using Game.Gameplay.Movement.Components;   // AnimationStateComponent

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

        // MathHelper : huit appels dans ce fichier, aucune declaration. Les trois
        // methodes sont celles qui y sont appelees, et rien de plus.
        public static class MathHelper
        {
            public static float Lerp(float a, float b, float t) => a + (b - a) * t;
            public static float ToDegrees(float radians) => radians * (180f / MathF.PI);
            public static float ToRadians(float degrees) => degrees * (MathF.PI / 180f);
        }

        public interface IMovementLogger
        {
            System.Collections.Generic.IEnumerable<string> GetCriticalErrorsSince(DateTime depuis);
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

        /// <summary>
        /// Erreurs critiques journalisees depuis une date. Appelee ligne 1197, dont
        /// le `.Any()` qui suit fixe le type de retour a une sequence.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> GetCriticalErrorsSince(DateTime depuis)
            => System.Array.Empty<string>();

        public void LogError(string source, string message, string stackTrace, ErrorSeverity severity)
        {
            string key = source + ":" + message; // Clé simple pour regrouper les erreurs identiques
            if (_errors.ContainsKey(key))
            {
                // ErrorInfo est un struct : `_errors[key].Count++` modifiait la COPIE
                // rendue par l'indexeur, et le compteur ne bougeait jamais. Le
                // compilateur le refuse (CS1612) — avec une classe, la meme ecriture
                // serait passee en silence. Lire, modifier, reecrire.
                var info = _errors[key];
                info.Count++;
                info.LastLoggedTime = DateTime.UtcNow;
                _errors[key] = info;
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
        // Contrat IReusableComponent : remise a zero avant reutilisation par le
        // pool. Sur un struct, `this = default` remet TOUS les champs a leur valeur
        // par defaut — rien ne peut etre oublie quand un champ est ajoute plus tard.
        public void Reset() => this = default;

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
    }

    public struct MovementStateComponent : IComponent, IReusableComponent
    {
        // Contrat IReusableComponent : remise a zero avant reutilisation par le
        // pool. Sur un struct, `this = default` remet TOUS les champs a leur valeur
        // par defaut — rien ne peut etre oublie quand un champ est ajoute plus tard.
        public void Reset() => this = default;

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
        // Contrat IReusableComponent : remise a zero avant reutilisation par le
        // pool. Sur un struct, `this = default` remet TOUS les champs a leur valeur
        // par defaut — rien ne peut etre oublie quand un champ est ajoute plus tard.
        public void Reset() => this = default;

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
        // Contrat IReusableComponent : remise a zero avant reutilisation par le
        // pool. Sur un struct, `this = default` remet TOUS les champs a leur valeur
        // par defaut — rien ne peut etre oublie quand un champ est ajoute plus tard.
        public void Reset() => this = default;

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
        // Contrat IReusableComponent : remise a zero avant reutilisation par le
        // pool. Sur un struct, `this = default` remet TOUS les champs a leur valeur
        // par defaut — rien ne peut etre oublie quand un champ est ajoute plus tard.
        public void Reset() => this = default;

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
    // Le seul membre de IMovementSystem, declare dans
    // Engine/Animation/AnimationEngineStub.Index.cs:87. Vector3 et Quaternion
    // viennent d'Engine.Animation, deja importe en tete de fichier.
    //
    // Corps vide plutot qu'une pose ecrite au hasard : aucun appelant n'existe
    // encore, donc rien ne dicte ce que le systeme doit faire de la racine.
    public void SetAnimationRootMotion(string entityId, Vector3 position, Quaternion rotation)
    {
    }

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
    // Champ absent alors que deux sites l'utilisent (1868, 1880) pour construire
    // MovementDebugOverlaySystem. RenderSystem vient d'Engine.Rendering, deja
    // importe en tete de fichier.
    private Engine.Rendering.RenderSystem _renderSystem;

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