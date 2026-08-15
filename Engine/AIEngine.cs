using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Pour le multithreading
using System.Diagnostics; // Pour le profiling
using System.Drawing; // Pour les couleurs de debug
using System.Threading; // Pour la gestion des threads et la synchronisation

// --- 137. CORE ENGINE (Concepts repris) ---
// IManager, IMessage, EventBus, IComponent, ISystem, Entity, EntityManager sont définis dans Engine.cs
// IPhysicsBody, ColliderComponent, RigidBodyComponent, PhysicsSystem sont définis dans PhysicsEngine.cs
// InputManager est défini dans Engine.cs
// Vector2 est défini dans Engine.cs ou ailleurs

// --- 140. AI ENGINE (Intelligence Artificielle) ---

// Enumération des tags d'entité (remplace les chaînes)
[Flags]
public enum AIEntityTag : ulong
{
    None = 0,
    Player = 1 << 0,
    Enemy = 1 << 1,
    Ally = 1 << 2,
    Obstacle = 1 << 3,
    Item = 1 << 4,
    Corpse = 1 << 5,
    // Ajouter d'autres tags au besoin
}

// Enumération des factions (ajoutée pour la communication)
[Flags]
public enum AIFaction : ulong
{
    Neutral = 0,
    FactionA = 1 << 0,
    FactionB = 1 << 1,
    // ...
}

// Interface pour les composants réutilisables (Pooling)
public interface IReusableComponent
{
    void Reset(); // Pour remettre le composant à zéro avant de le réutiliser
}

// Structure pour les entrées de mémoire avec expiration, décroissance et type
public struct TimedMemoryEntry : IReusableComponent
{
    public Vector2 Position { get; set; }
    public float Timestamp { get; set; }
    public float ExpirationTime { get; set; }
    public AIEntityTag EntityType { get; set; } // Type de l'entité mémorisée
    public float Relevance { get; set; } // Pertinence décroissante
    public float DecayRate { get; set; } // Taux de décroissance de la pertinence
    public float Confidence { get; set; } // Niveau de confiance dans l'information
    // Ajouté : EmotionMemory (5. Mémoire émotionnelle)
    public float EmotionalImpact { get; set; } // Impact émotionnel de l'événement mémorisé

    public TimedMemoryEntry(Vector2 pos, float time, float duration, AIEntityTag type, float decayRate = 0.1f, float confidence = 1.0f, float emotion = 0.0f)
    {
        Position = pos;
        Timestamp = time;
        ExpirationTime = duration;
        EntityType = type;
        Relevance = 1.0f; // Pertinence initiale
        DecayRate = decayRate;
        Confidence = confidence;
        EmotionalImpact = emotion;
    }

    public bool IsExpired(float currentTime) => (currentTime - Timestamp) >= ExpirationTime;
    public void UpdateRelevance(float deltaTime) => Relevance = Math.Max(0, Relevance - DecayRate * deltaTime);

    public void Reset()
    {
        Position = Vector2.Zero;
        Timestamp = 0.0f;
        ExpirationTime = 0.0f;
        EntityType = AIEntityTag.None;
        Relevance = 1.0f;
        DecayRate = 0.1f;
        Confidence = 1.0f;
        EmotionalImpact = 0.0f;
    }
}

// Structure pour les modifications de perception liées à l'environnement
public struct PerceptionModifiers
{
    public float SightRangeMultiplier { get; set; }
    public float HearingRangeMultiplier { get; set; }
    public float DetectionNoiseAdditive { get; set; }
    public float VisibilityMultiplier { get; set; } // Influence sur la probabilité d'être vu

    public PerceptionModifiers(float sightMult = 1.0f, float hearMult = 1.0f, float noiseAdd = 0.0f, float visMult = 1.0f)
    {
        SightRangeMultiplier = sightMult;
        HearingRangeMultiplier = hearMult;
        DetectionNoiseAdditive = noiseAdd;
        VisibilityMultiplier = visMult;
    }
}

// Structure pour les modes de vision spécialisés (ajoutée pour 4. Vision nocturne / thermique)
public struct SpecializedVisionModes
{
    public bool NightVisionActive { get; set; }
    public bool ThermalVisionActive { get; set; }
    public float NightVisionRangeBonus { get; set; }
    public float ThermalVisionHeatSensitivity { get; set; }

    public SpecializedVisionModes(bool nvActive = false, bool thActive = false, float nvBonus = 2.0f, float thSens = 1.0f)
    {
        NightVisionActive = nvActive;
        ThermalVisionActive = thActive;
        NightVisionRangeBonus = nvBonus;
        ThermalVisionHeatSensitivity = thSens;
    }
}

// Composant de base pour une entité contrôlée par l'IA
public struct AIControllerComponent : IComponent, IReusableComponent
{
    public AIBehaviorType BehaviorType { get; set; }
    public float AwarenessRange { get; set; } = 10.0f;
    public float ReactionTime { get; set; } = 0.1f;
    public float LastDecisionTime { get; set; } = 0.0f;
    public AIEntityTag TagMask { get; set; } = AIEntityTag.None; // Remplace la liste de chaînes
    public int GroupId { get; set; } = -1; // ID du groupe (pour coopération)
    // Ajouté : Faction (5. Protocole inter-groupe)
    public AIFaction Faction { get; set; } = AIFaction.Neutral;

    public AIControllerComponent(AIBehaviorType behavior)
    {
        BehaviorType = behavior;
        AwarenessRange = 10.0f;
        ReactionTime = 0.1f;
        LastDecisionTime = 0.0f;
        TagMask = AIEntityTag.None; // Initialisation du masque
        GroupId = -1;
        Faction = AIFaction.Neutral;
    }

    // Méthodes utilitaires pour les tags
    public bool HasTag(AIEntityTag tag) => (TagMask & tag) != AIEntityTag.None;
    public void AddTag(AIEntityTag tag) => TagMask |= tag;
    public void RemoveTag(AIEntityTag tag) => TagMask &= ~tag;
    public void ToggleTag(AIEntityTag tag) => TagMask ^= tag;
    public void ClearTags() => TagMask = AIEntityTag.None;
    public bool HasAnyTag(params AIEntityTag[] tags)
    {
        foreach (var tag in tags)
        {
            if ((TagMask & tag) != AIEntityTag.None)
                return true;
        }
        return false;
    }

    // Méthodes utilitaires pour les factions
    public bool IsFaction(AIFaction faction) => (Faction & faction) != AIFaction.Neutral;
    public bool IsFriendlyTo(AIFaction otherFaction) => (Faction & otherFaction) != AIFaction.Neutral; // Simplifié

    public void Reset()
    {
        BehaviorType = AIBehaviorType.Idle;
        AwarenessRange = 10.0f;
        ReactionTime = 0.1f;
        LastDecisionTime = 0.0f;
        TagMask = AIEntityTag.None;
        GroupId = -1;
        Faction = AIFaction.Neutral;
    }
}

public enum AIBehaviorType
{
    Idle,
    Patrol,
    Chase,
    Flee,
    Combat,
    Explore,
    Curious,
    Investigate,
    Custom
}

// Composant de perception (ajout de LastSeenPosition et LastHeardPosition avec expiration, type d'entité, bruit, vision périphérique, perception environnementale, modifications, fatigue sensorielle, modes de vision)
public struct AISensorComponent : IComponent, IReusableComponent
{
    public float SightRange { get; set; } = 5.0f;
    public float HearingRange { get; set; } = 8.0f;
    public float FieldOfView { get; set; } = 90.0f; // En degrés
    public float PeripheralVisionAngle { get; set; } = 45.0f; // Angle de vision périphérique
    public float DetectionNoise { get; set; } = 0.1f; // Facteur de bruit pour la détection (0.0 = parfait, 1.0 = très bruité)
    public List<Entity> DetectedEntities { get; set; } = new();

    // Ajout de LastSeenPosition et LastHeardPosition avec expiration et type d'entité
    public Dictionary<int, TimedMemoryEntry> LastSeenPositions { get; set; } = new(); // Clé = Entity.ID
    public Dictionary<int, TimedMemoryEntry> LastHeardPositions { get; set; } = new(); // Clé = Entity.ID
    // Ajout de LastNoticedEntities pour la mémoire spatiale hiérarchique
    public Dictionary<int, TimedMemoryEntry> LastNoticedEntities { get; set; } = new(); // Clé = Entity.ID

    // Perception environnementale
    public EnvironmentState EnvironmentState { get; set; } = new EnvironmentState();
    // Modifications de perception basées sur l'environnement
    public PerceptionModifiers PerceptionMods { get; set; } = new PerceptionModifiers();
    // Modes de vision spécialisés
    public SpecializedVisionModes VisionModes { get; set; } = new SpecializedVisionModes();

    // Fatigue sensorielle (ajoutée)
    public float SensoryFatigue { get; set; } = 0.0f; // Niveau de fatigue sensorielle (0.0 à 1.0)
    public float SensoryFatigueDecayRate { get; set; } = 0.01f; // Taux de récupération
    public float PerceptualBlurRadius { get; set; } = 0.5f; // Rayon de flou pour la vision périphérique (ajoutée)
    // Ajouté : TrustInPerception (8. Évaluation de confiance)
    public float TrustInPerception { get; set; } = 1.0f; // Niveau de confiance dans la perception (0.0 à 1.0)

    // Variables internes pour les calculs temporaires
    private float _effectiveSightRange;
    private float _effectiveHearingRange;
    private float _effectiveDetectionNoise;

    public AISensorComponent(float sight, float hearing, float fov)
    {
        SightRange = sight;
        HearingRange = hearing;
        FieldOfView = fov;
        PeripheralVisionAngle = fov / 2; // Exemple de réglage
        DetectionNoise = 0.1f;
        DetectedEntities = new List<Entity>();
        LastSeenPositions = new Dictionary<int, TimedMemoryEntry>();
        LastHeardPositions = new Dictionary<int, TimedMemoryEntry>();
        LastNoticedEntities = new Dictionary<int, TimedMemoryEntry>();
        EnvironmentState = new EnvironmentState();
        PerceptionMods = new PerceptionModifiers();
        VisionModes = new SpecializedVisionModes();
        SensoryFatigue = 0.0f;
        SensoryFatigueDecayRate = 0.01f;
        PerceptualBlurRadius = 0.5f; // Valeur par défaut
        TrustInPerception = 1.0f; // Confiance initiale
        _effectiveSightRange = sight;
        _effectiveHearingRange = hearing;
        _effectiveDetectionNoise = 0.1f;
    }

    public void UpdateEffectivePerceptions()
    {
        // Appliquer les modifications environnementales
        _effectiveSightRange = SightRange * PerceptionMods.SightRangeMultiplier;
        _effectiveHearingRange = HearingRange * PerceptionMods.HearingRangeMultiplier;
        _effectiveDetectionNoise = DetectionNoise + PerceptionMods.DetectionNoiseAdditive;

        // Appliquer les modes de vision spécialisés
        if (VisionModes.NightVisionActive)
        {
            _effectiveSightRange += VisionModes.NightVisionRangeBonus;
        }
        if (VisionModes.ThermalVisionActive)
        {
            // La vision thermique peut avoir des règles différentes
        }

        // Appliquer la fatigue sensorielle
        float fatigueMultiplier = 1.0f - (SensoryFatigue * 0.5f); // La fatigue réduit la portée et augmente le bruit
        _effectiveSightRange *= fatigueMultiplier;
        _effectiveHearingRange *= fatigueMultiplier;
        _effectiveDetectionNoise += SensoryFatigue * 0.2f;

        // Appliquer la confiance dans la perception (modifie la probabilité de détection réelle)
        // Cela peut influencer les seuils internes du système de perception
    }

    public void UpdateSensoryFatigue(float deltaTime)
    {
        // La perception elle-même peut augmenter la fatigue
        SensoryFatigue = Math.Min(1.0f, SensoryFatigue + (DetectedEntities.Count * 0.001f * deltaTime));
        // La fatigue diminue avec le temps
        SensoryFatigue = Math.Max(0.0f, SensoryFatigue - (SensoryFatigueDecayRate * deltaTime));
    }

    public float GetEffectiveSightRange() => _effectiveSightRange;
    public float GetEffectiveHearingRange() => _effectiveHearingRange;
    public float GetEffectiveDetectionNoise() => _effectiveDetectionNoise;

    public void Reset()
    {
        SightRange = 5.0f;
        HearingRange = 8.0f;
        FieldOfView = 90.0f;
        PeripheralVisionAngle = 45.0f;
        DetectionNoise = 0.1f;
        DetectedEntities.Clear();
        LastSeenPositions.Clear();
        LastHeardPositions.Clear();
        LastNoticedEntities.Clear();
        EnvironmentState.Reset();
        PerceptionMods = new PerceptionModifiers();
        VisionModes = new SpecializedVisionModes();
        SensoryFatigue = 0.0f;
        PerceptualBlurRadius = 0.5f;
        TrustInPerception = 1.0f; // Réinitialiser la confiance
        _effectiveSightRange = 5.0f;
        _effectiveHearingRange = 8.0f;
        _effectiveDetectionNoise = 0.1f;
    }
}

public struct EnvironmentState
{
    public bool IsInDarkness { get; set; } = false;
    public bool IsInFog { get; set; } = false;
    public bool IsInRain { get; set; } = false;
    public float AmbientNoiseLevel { get; set; } = 0.0f;
    public float LightLevel { get; set; } = 1.0f;
    public float Temperature { get; set; } = 20.0f;
    // Ajouté : Cycle jour/nuit (3. Cycle jour/nuit)
    public float DayNightRatio { get; set; } = 1.0f; // 1.0 = jour, 0.0 = nuit

    public void Reset()
    {
        IsInDarkness = false;
        IsInFog = false;
        IsInRain = false;
        AmbientNoiseLevel = 0.0f;
        LightLevel = 1.0f;
        Temperature = 20.0f;
        DayNightRatio = 1.0f;
    }
}

// Composant d'action
public struct AIActionComponent : IComponent, IReusableComponent
{
    public AIActionType ActionType { get; set; }
    public Entity Target { get; set; }
    public Vector2 Destination { get; set; }
    public float ActionDuration { get; set; } = 0.0f;
    public float ActionStartTime { get; set; } = 0.0f;

    public AIActionComponent(AIActionType type, Entity target = default, Vector2 dest = default)
    {
        ActionType = type;
        Target = target;
        Destination = dest;
        ActionDuration = 0.0f;
        ActionStartTime = 0.0f;
    }

    public void Reset()
    {
        ActionType = AIActionType.None;
        Target = default;
        Destination = Vector2.Zero;
        ActionDuration = 0.0f;
        ActionStartTime = 0.0f;
    }
}

public enum AIActionType
{
    None,
    MoveTo,
    Attack,
    Flee,
    Wait,
    UseAbility,
    SeekCover,
    FormUp,
    Investigate,
    TacticalRetreat,
    Camouflage, // Ajouté
    CreateDiversion // Ajouté
}

// Composant de navigation
public struct AINavigationComponent : IComponent, IReusableComponent
{
    public List<Vector2> Path { get; set; } = new();
    public int CurrentWaypointIndex { get; set; } = 0;
    public float ArrivalThreshold { get; set; } = 0.5f;
    public bool IsPathValid { get; set; } = false;
    public bool IsMoving { get; set; } = false;

    public AINavigationComponent()
    {
        Path = new List<Vector2>();
        CurrentWaypointIndex = 0;
        ArrivalThreshold = 0.5f;
        IsPathValid = false;
        IsMoving = false;
    }

    public void Reset()
    {
        Path.Clear();
        CurrentWaypointIndex = 0;
        IsPathValid = false;
        IsMoving = false;
    }
}

// --- 140.1. STRUCTURES DE BASE ---

// Composant d'état (FSM) avec niveau d'alerte, suspicion et conscience de contexte (2. Conscience de contexte)
public struct AIStateComponent : IComponent, IReusableComponent
{
    public AIState CurrentState { get; set; }
    public AIState PreviousState { get; set; }
    public float StateEnterTime { get; set; }
    public AIAlertLevel AlertLevel { get; set; } // Niveau d'alerte
    public float SuspicionLevel { get; set; } // Niveau de suspicion (0.0 à 1.0)
    public AIContextFlags ContextFlags { get; set; } // Conscience de contexte (isolé, groupe, stressé)

    public AIStateComponent(AIState initialState)
    {
        CurrentState = initialState;
        PreviousState = AIState.Idle;
        StateEnterTime = 0.0f;
        AlertLevel = AIAlertLevel.Calm;
        SuspicionLevel = 0.0f;
        ContextFlags = AIContextFlags.None;
    }

    public void Reset()
    {
        CurrentState = AIState.Idle;
        PreviousState = AIState.Idle;
        StateEnterTime = 0.0f;
        AlertLevel = AIAlertLevel.Calm;
        SuspicionLevel = 0.0f;
        ContextFlags = AIContextFlags.None;
    }
}

public enum AIState
{
    Idle,
    Alert,
    Combat,
    Flee,
    Patrol,
    Search,
    Investigate,
    Curious,
    Panic,
    FormUp,
    TacticalRetreat,
    Camouflaging, // Ajouté
    CreatingDiversion // Ajouté
}

public enum AIAlertLevel
{
    Calm,
    Suspicious,
    Alert,
    Combat
}

[Flags]
public enum AIContextFlags
{
    None = 0,
    Isolated = 1 << 0, // Agit seul
    InGroup = 1 << 1, // Est en groupe
    Stressed = 1 << 2, // Sous stress
    LowHealth = 1 << 3, // Santé faible
    Outnumbered = 1 << 4 // Numériquement inférieur
}

// Composant Blackboard (optimisé)
public struct AIBlackboard : IComponent, IReusableComponent
{
    public Dictionary<int, object> Data { get; set; } = new(); // Clé numérique pour meilleure performance

    public AIBlackboard(Dictionary<int, object> initialData = null)
    {
        Data = initialData ?? new Dictionary<int, object>();
    }

    public void SetValue(int key, object value) => Data[key] = value;
    public T GetValue<T>(int key) => Data.ContainsKey(key) ? (T)Data[key] : default(T);
    public bool ContainsKey(int key) => Data.ContainsKey(key);
    public void RemoveKey(int key) => Data.Remove(key);

    public void Reset()
    {
        Data.Clear();
    }
}

// Composant Threat (optimisé, ajout de pondération dynamique et pondérations modifiables)
public struct AIThreatComponent : IComponent, IReusableComponent
{
    public Dictionary<int, float> ThreatLevels { get; set; } = new(); // Clé = Entity.ID
    public int PrimaryTargetId { get; set; } = -1; // Stocke l'ID de l'entité cible
    public List<ThreatHistoryEntry> PastThreats { get; set; } = new(); // Historique des menaces (ajoutée)

    // Pondérations pour le calcul de la menace (dynamique)
    public float DistanceWeight { get; set; } = 1.0f;
    public float VisibilityWeight { get; set; } = 1.0f;
    public float DangerWeight { get; set; } = 1.0f;
    public float FearWeight { get; set; } = 0.5f; // Nouveau poids pour la peur (moral)

    public AIThreatComponent()
    {
        ThreatLevels = new Dictionary<int, float>();
        PrimaryTargetId = -1;
        PastThreats = new List<ThreatHistoryEntry>(); // Initialisation
        DistanceWeight = 1.0f;
        VisibilityWeight = 1.0f;
        DangerWeight = 1.0f;
        FearWeight = 0.5f;
    }

    // Ancienne méthode UpdateThreat, conservée pour compatibilité ou usage simple
    public void UpdateThreat(int entityId, float threat)
    {
        if (threat <= 0)
        {
            ThreatLevels.Remove(entityId);
            if (PrimaryTargetId == entityId) PrimaryTargetId = -1;
        }
        else
        {
            ThreatLevels[entityId] = threat;
            // Mettre à jour l'historique
            UpdateThreatHistory(entityId, threat);
            RecalculatePrimaryTarget();
        }
    }

    // Nouvelle méthode pour calculer la menace avec pondération
    public void CalculateAndSetThreat(int entityId, float baseThreat, float distance, bool isVisible, float dangerPotential, float fearFactor = 0.0f)
    {
        float weightedThreat = baseThreat;
        weightedThreat += (1.0f / (distance + 1)) * DistanceWeight;
        weightedThreat += (isVisible ? 1.0f : 0.0f) * VisibilityWeight;
        weightedThreat += dangerPotential * DangerWeight;
        weightedThreat += fearFactor * FearWeight;

        UpdateThreat(entityId, weightedThreat);
    }

    private void UpdateThreatHistory(int entityId, float threatLevel)
    {
        var existingEntry = PastThreats.Find(entry => entry.EntityId == entityId);
        if (existingEntry != null)
        {
            existingEntry.Timestamp = Time.ElapsedTime;
            existingEntry.ThreatLevel = threatLevel;
        }
        else
        {
            PastThreats.Add(new ThreatHistoryEntry(entityId, threatLevel, Time.ElapsedTime));
        }
        // Limiter la taille de l'historique
        if (PastThreats.Count > 10) PastThreats.RemoveAt(0);
    }

    private void RecalculatePrimaryTarget()
    {
        int bestTargetId = -1;
        float highestThreat = float.MinValue;
        foreach (var kvp in ThreatLevels)
        {
            if (kvp.Value > highestThreat)
            {
                highestThreat = kvp.Value;
                bestTargetId = kvp.Key;
            }
        }
        PrimaryTargetId = bestTargetId;
    }

    // Méthode pour nettoyer les menaces expirées ou invalides
    public void CleanupExpiredThreats(List<int> validEntityIds)
    {
        var idsToRemove = new List<int>();
        foreach (var kvp in ThreatLevels)
        {
            if (!validEntityIds.Contains(kvp.Key))
            {
                idsToRemove.Add(kvp.Key);
            }
        }
        foreach (var id in idsToRemove)
        {
            ThreatLevels.Remove(id);
            if (PrimaryTargetId == id) PrimaryTargetId = -1;
        }
    }

    public void Reset()
    {
        ThreatLevels.Clear();
        PrimaryTargetId = -1;
        PastThreats.Clear(); // Réinitialisation de l'historique
        DistanceWeight = 1.0f;
        VisibilityWeight = 1.0f;
        DangerWeight = 1.0f;
        FearWeight = 0.5f;
    }
}

// Entrée pour l'historique des menaces (ajoutée)
public struct ThreatHistoryEntry
{
    public int EntityId { get; set; }
    public float ThreatLevel { get; set; }
    public float Timestamp { get; set; }

    public ThreatHistoryEntry(int id, float threat, float time)
    {
        EntityId = id;
        ThreatLevel = threat;
        Timestamp = time;
    }
}

// Composant Memory (optimisé avec expiration et décroissance)
public struct AIMemoryComponent : IComponent, IReusableComponent
{
    public Dictionary<int, TimedMemoryEntry> Positions { get; set; } = new();
    public Dictionary<int, float> Timers { get; set; } = new();
    public Dictionary<int, int> Entities { get; set; } = new();
    public Dictionary<Vector2, TimedMemoryEntry> Areas { get; set; } = new();
    // Ajouté : EmotionalMemoryEntries (5. Mémoire émotionnelle)
    public List<TimedMemoryEntry> EmotionalEvents { get; set; } = new(); // Liste d'événements marquants émotionnellement

    public AIMemoryComponent()
    {
        Positions = new Dictionary<int, TimedMemoryEntry>();
        Timers = new Dictionary<int, float>();
        Entities = new Dictionary<int, int>();
        Areas = new Dictionary<Vector2, TimedMemoryEntry>();
        EmotionalEvents = new List<TimedMemoryEntry>(); // Initialisation
    }

    public void RememberPosition(int key, Vector2 pos, AIEntityTag entityType, float expirationTime, float decayRate = 0.1f, float confidence = 1.0f, float emotion = 0.0f)
    {
        var entry = new TimedMemoryEntry(pos, Time.ElapsedTime, expirationTime, entityType, decayRate, confidence, emotion);
        Positions[key] = entry;
    }
    public void RememberEntity(int key, int entityId) => Entities[key] = entityId;
    public void SetTimer(int key, float duration) => Timers[key] = duration;
    public void RememberArea(Vector2 center, float expirationTime, float confidence = 1.0f, AIEntityTag type = AIEntityTag.None)
    {
        var entry = new TimedMemoryEntry(center, Time.ElapsedTime, expirationTime, type, 0.05f, confidence);
        Areas[center] = entry;
    }
    // Ajouté : RememberEmotionalEvent (5. Mémoire émotionnelle)
    public void RememberEmotionalEvent(Vector2 location, float impact, AIEntityTag eventType)
    {
        var entry = new TimedMemoryEntry(location, Time.ElapsedTime, 60.0f, eventType, 0.02f, 1.0f, impact); // Expiration après 1 minute
        EmotionalEvents.Add(entry);
        if (EmotionalEvents.Count > 20) EmotionalEvents.RemoveAt(0); // Limiter la taille
    }

    public Vector2 GetRememberedPosition(int key) => Positions.ContainsKey(key) ? Positions[key].Position : Vector2.Zero;
    public int GetRememberedEntity(int key) => Entities.ContainsKey(key) ? Entities[key] : -1;
    public AIEntityTag GetRememberedEntityType(int key) => Positions.ContainsKey(key) ? Positions[key].EntityType : AIEntityTag.None;
    public float GetRelevance(int key) => Positions.ContainsKey(key) ? Positions[key].Relevance : 0.0f;
    public bool IsTimerActive(int key) => Timers.ContainsKey(key) && Timers[key] > 0.0f;
    public TimedMemoryEntry GetRememberedArea(Vector2 center) => Areas.ContainsKey(center) ? Areas[center] : new TimedMemoryEntry();

    public void UpdateTimers(float deltaTime)
    {
        var keysToRemove = new List<int>();
        foreach (var kvp in Timers)
        {
            Timers[kvp.Key] -= deltaTime;
            if (Timers[kvp.Key] <= 0.0f)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            Timers.Remove(key);
            Positions.Remove(key);
            Entities.Remove(key);
        }

        keysToRemove.Clear();
        foreach (var kvp in Positions)
        {
            var entry = kvp.Value;
            entry.UpdateRelevance(deltaTime);
            if (entry.IsExpired(Time.ElapsedTime) || entry.Relevance <= 0.0f)
            {
                keysToRemove.Add(kvp.Key);
            }
            else
            {
                Positions[kvp.Key] = entry;
            }
        }
        foreach (var key in keysToRemove)
        {
            Positions.Remove(key);
        }

        var areasToRemove = new List<Vector2>();
        foreach (var kvp in Areas)
        {
            var entry = kvp.Value;
            entry.UpdateRelevance(deltaTime);
            if (entry.IsExpired(Time.ElapsedTime) || entry.Relevance <= 0.0f)
            {
                areasToRemove.Add(kvp.Key);
            }
            else
            {
                Areas[kvp.Key] = entry;
            }
        }
        foreach (var area in areasToRemove)
        {
            Areas.Remove(area);
        }

        // Mettre à jour les événements émotionnels
        for (int i = EmotionalEvents.Count - 1; i >= 0; i--)
        {
            var entry = EmotionalEvents[i];
            entry.UpdateRelevance(deltaTime);
            if (entry.IsExpired(Time.ElapsedTime) || entry.Relevance <= 0.0f)
            {
                EmotionalEvents.RemoveAt(i);
            }
            else
            {
                EmotionalEvents[i] = entry;
            }
        }
    }

    public void Reset()
    {
        Positions.Clear();
        Timers.Clear();
        Entities.Clear();
        Areas.Clear();
        EmotionalEvents.Clear(); // Réinitialisation des souvenirs émotionnels
    }
}

// Composant Mémoire Collective (ajouté)
public struct AISharedMemoryComponent : IComponent, IReusableComponent
{
    public Dictionary<int, TimedMemoryEntry> SharedPositions { get; set; } = new(); // Clé = Entity.ID de l'allié qui a partagé
    public Dictionary<int, int> SharedTargets { get; set; } = new(); // Clé = Entity.ID de l'allié, Valeur = ID de la cible partagée
    // Ajouté : SharedLearnedPatterns (3. Apprentissage collectif)
    public Dictionary<string, int> SharedLearnedPatterns { get; set; } = new(); // Clé = nom du pattern, Valeur = fréquence observée par les alliés

    public void SharePosition(int sharerId, TimedMemoryEntry entry) => SharedPositions[sharerId] = entry;
    public void ShareTarget(int sharerId, int targetId) => SharedTargets[sharerId] = targetId;
    public void SharePattern(string patternName) => SharedLearnedPatterns[patternName] = SharedLearnedPatterns.GetValueOrDefault(patternName, 0) + 1;

    public void Reset()
    {
        SharedPositions.Clear();
        SharedTargets.Clear();
        SharedLearnedPatterns.Clear(); // Réinitialisation des patterns partagés
    }
}

// --- 140.2. COMPOSANTS SUPPLÉMENTAIRES ---

// Composant Moral (6)
public struct AIMoralComponent : IComponent, IReusableComponent
{
    public float Fear { get; set; } = 0.0f;
    public float Confidence { get; set; } = 1.0f;
    public float CollectiveMorale { get; set; } = 1.0f; // Moral du groupe (influencé par les alliés)
    // Ajouté : Anger/Rage (4. AIEmotionSystem)
    public float Anger { get; set; } = 0.0f;

    public AIMoralComponent(float fear = 0.0f, float confidence = 1.0f, float collective = 1.0f, float anger = 0.0f)
    {
        Fear = fear;
        Confidence = confidence;
        CollectiveMorale = collective;
        Anger = anger;
    }

    public void Reset()
    {
        Fear = 0.0f;
        Confidence = 1.0f;
        CollectiveMorale = 1.0f;
        Anger = 0.0f;
    }
}

// Composant Fatigue (7)
public struct AIFatigueComponent : IComponent, IReusableComponent
{
    public float ActivityTime { get; set; } = 0.0f;
    public float FatigueLevel { get; set; } = 0.0f;
    public float SpeedMultiplier { get; set; } = 1.0f;
    public float AccuracyMultiplier { get; set; } = 1.0f;
    public float DecisionSpeedMultiplier { get; set; } = 1.0f;
    // Ajouté : Influence de la température (4. Influence de la température)
    public float TemperatureInfluence { get; set; } = 0.0f; // Effet de la température ambiante sur la fatigue

    public AIFatigueComponent()
    {
        ActivityTime = 0.0f;
        FatigueLevel = 0.0f;
        SpeedMultiplier = 1.0f;
        AccuracyMultiplier = 1.0f;
        DecisionSpeedMultiplier = 1.0f;
        TemperatureInfluence = 0.0f;
    }

    public void UpdateFatigue(float deltaTime, float ambientTemperature = 20.0f) // Prend en compte la température
    {
        ActivityTime += deltaTime;
        // Calculer la fatigue de base
        float baseFatigue = ActivityTime / 300.0f; // Exemple : 5 minutes pour 100%
        // Appliquer l'influence de la température
        float tempEffect = Math.Max(0.0f, (ambientTemperature - 20.0f) / 50.0f); // Exemple : +10% de fatigue par 50°C au-dessus de 20°C
        FatigueLevel = Math.Min(1.0f, baseFatigue + tempEffect);

        SpeedMultiplier = 1.0f - (FatigueLevel * 0.3f);
        AccuracyMultiplier = 1.0f - (FatigueLevel * 0.2f);
        DecisionSpeedMultiplier = 1.0f - (FatigueLevel * 0.1f);
    }

    public void Reset()
    {
        ActivityTime = 0.0f;
        FatigueLevel = 0.0f;
        SpeedMultiplier = 1.0f;
        AccuracyMultiplier = 1.0f;
        DecisionSpeedMultiplier = 1.0f;
        TemperatureInfluence = 0.0f;
    }
}

// Composant Curiosité (13)
public struct AICuriosityComponent : IComponent, IReusableComponent
{
    public Dictionary<Vector2, float> ExploredAreas { get; set; } = new();
    public float ExplorationUrgency { get; set; } = 0.0f;
    public Vector2 LastInterestingPoint { get; set; } = Vector2.Zero;
    // Carte d'exploration (ajoutée)
    public Dictionary<Vector2, float> ExplorationMap { get; set; } = new(); // Clé = Position discrétisée, Valeur = Fréquence de visite

    public AICuriosityComponent()
    {
        ExploredAreas = new Dictionary<Vector2, float>();
        ExplorationUrgency = 0.0f;
        LastInterestingPoint = Vector2.Zero;
        ExplorationMap = new Dictionary<Vector2, float>();
    }

    public void MarkAreaExplored(Vector2 center, float timeSinceLastVisit = 0.0f) => ExploredAreas[center] = timeSinceLastVisit;
    public void UpdateExplorationUrgency(float deltaTime)
    {
        float maxTime = 0.0f;
        foreach (var kvp in ExploredAreas)
        {
            ExploredAreas[kvp.Key] = kvp.Value + deltaTime;
            maxTime = Math.Max(maxTime, kvp.Value);
        }
        ExplorationUrgency = Math.Min(1.0f, maxTime / 60.0f);
    }

    public void UpdateExplorationMap(Vector2 pos, float cellSize = 1.0f)
    {
        Vector2 cell = new Vector2((int)(pos.X / cellSize), (int)(pos.Y / cellSize));
        if (ExplorationMap.ContainsKey(cell))
        {
            ExplorationMap[cell]++;
        }
        else
        {
            ExplorationMap[cell] = 1.0f;
        }
    }

    public Vector2 CalculateAutonomousExplorationTarget()
    {
        // Exemple : trouver la cellule la moins visitée
        Vector2 targetCell = Vector2.Zero;
        float minVisits = float.MaxValue;
        foreach (var kvp in ExplorationMap)
        {
            if (kvp.Value < minVisits)
            {
                minVisits = kvp.Value;
                targetCell = kvp.Key;
            }
        }
        // Convertir la cellule en position réelle
        return new Vector2(targetCell.X + 0.5f, targetCell.Y + 0.5f); // Centre de la cellule
    }

    public void SetInterestingPoint(Vector2 point) => LastInterestingPoint = point;

    public void Reset()
    {
        ExploredAreas.Clear();
        ExplorationUrgency = 0.0f;
        LastInterestingPoint = Vector2.Zero;
        ExplorationMap.Clear();
    }
}

// Composant Routine
public struct AIRoutineComponent : IComponent, IReusableComponent
{
    public List<AIRoutineTask> Tasks { get; set; } = new();
    public int CurrentTaskIndex { get; set; } = 0;
    public float TaskStartTime { get; set; } = 0.0f;

    public AIRoutineComponent()
    {
        Tasks = new List<AIRoutineTask>();
        CurrentTaskIndex = 0;
        TaskStartTime = 0.0f;
    }

    public void AddTask(AIRoutineTask task) => Tasks.Add(task);
    public AIRoutineTask GetCurrentTask() => CurrentTaskIndex < Tasks.Count ? Tasks[CurrentTaskIndex] : null;
    public void AdvanceToNextTask() { CurrentTaskIndex++; TaskStartTime = Time.ElapsedTime; }

    public void Reset()
    {
        Tasks.Clear();
        CurrentTaskIndex = 0;
        TaskStartTime = 0.0f;
    }
}

public class AIRoutineTask
{
    public string Name { get; set; }
    public AIState State { get; set; }
    public Vector2 Location { get; set; }
    public float Duration { get; set; }

    public AIRoutineTask(string name, AIState state, Vector2 loc, float dur)
    {
        Name = name;
        State = state;
        Location = loc;
        Duration = dur;
    }
}

// Composant Combat (partiellement déplacé dans AICombatSystem, mais certaines stats persistent ici)
public struct AICombatComponent : IComponent, IReusableComponent
{
    public float Ammo { get; set; } = 100.0f;
    public float MaxAmmo { get; set; } = 100.0f;
    public float Energy { get; set; } = 100.0f;
    public float MaxEnergy { get; set; } = 100.0f;
    public float MoraleImpact { get; set; } = 0.0f;
    public float RageLevel { get; set; } = 0.0f;
    // Santé (ajoutée)
    public float Health { get; set; } = 100.0f;
    public float MaxHealth { get; set; } = 100.0f;

    public AICombatComponent(float ammo = 100.0f, float energy = 100.0f, float health = 100.0f)
    {
        Ammo = ammo;
        MaxAmmo = ammo;
        Energy = energy;
        MaxEnergy = energy;
        Health = health;
        MaxHealth = health;
        MoraleImpact = 0.0f;
        RageLevel = 0.0f;
    }

    public void ConsumeAmmo(float amount) { Ammo = Math.Max(0, Ammo - amount); }
    public void ConsumeEnergy(float amount) { Energy = Math.Max(0, Energy - amount); }
    public void TakeDamage(float damage) { Health = Math.Max(0, Health - damage); MoraleImpact -= damage * 0.01f; }
    public void IncreaseRage(float amount) { RageLevel = Math.Min(1.0f, RageLevel + amount); }

    public void Reset()
    {
        Ammo = MaxAmmo;
        Energy = MaxEnergy;
        Health = MaxHealth;
        MoraleImpact = 0.0f;
        RageLevel = 0.0f;
    }
}

// Composant Apprentissage (14)
public struct AILearningComponent : IComponent, IReusableComponent
{
    public Dictionary<string, float> LearnedWeights { get; set; } = new();
    public Dictionary<Vector2, float> LearnedDangerousZones { get; set; } = new();
    public Dictionary<string, int> LearnedPlayerPatterns { get; set; } = new();
    public List<string> AvoidedActions { get; set; } = new();
    // PatternMemory (ajouté)
    public List<GameStateSnapshot> PatternMemory { get; set; } = new(); // Historique d'états du jeu
    // Renforcement (ajouté)
    public Dictionary<string, float> ActionRewards { get; set; } = new(); // Clé = nom de l'action, Valeur = récompense cumulative
    // Ajouté : éléments pour les nouveaux aspects de l'apprentissage
    public Dictionary<string, float> PredictedRewards { get; set; } = new(); // 3. RewardPredictor
    public Dictionary<string, float> ContextualWeights { get; set; } = new(); // 4. Apprentissage contextuel
    public Dictionary<string, float> EmotionalLearningFactors { get; set; } = new(); // 7. Apprentissage émotionnel
    public Dictionary<Vector2, float> SpatialLearningMap { get; set; } = new(); // 7. Apprentissage spatial
    public Dictionary<string, float> SocialLearningPatterns { get; set; } = new(); // 8. Apprentissage social
    public float SurvivalBias { get; set; } = 1.0f; // 9. Apprentissage de survie
    public Dictionary<string, float> InformationTrust { get; set; } = new(); // 10. Apprentissage de confiance

    public AILearningComponent()
    {
        LearnedWeights = new Dictionary<string, float>();
        LearnedDangerousZones = new Dictionary<Vector2, float>();
        LearnedPlayerPatterns = new Dictionary<string, int>();
        AvoidedActions = new List<string>();
        PatternMemory = new List<GameStateSnapshot>();
        ActionRewards = new Dictionary<string, float>();
        PredictedRewards = new Dictionary<string, float>(); // Initialisation
        ContextualWeights = new Dictionary<string, float>(); // Initialisation
        EmotionalLearningFactors = new Dictionary<string, float>(); // Initialisation
        SpatialLearningMap = new Dictionary<Vector2, float>(); // Initialisation
        SocialLearningPatterns = new Dictionary<string, float>(); // Initialisation
        SurvivalBias = 1.0f; // Initialisation
        InformationTrust = new Dictionary<string, float>(); // Initialisation
    }

    public void UpdateLearnedWeights(string weightName, float outcomeFactor)
    {
        if (!LearnedWeights.ContainsKey(weightName))
        {
            LearnedWeights[weightName] = 1.0f;
        }
        LearnedWeights[weightName] += outcomeFactor * 0.01f;
        LearnedWeights[weightName] = Math.Max(0.1f, LearnedWeights[weightName]);
    }

    public void LearnDangerousZone(Vector2 center, float dangerLevel)
    {
        if (LearnedDangerousZones.ContainsKey(center))
        {
            LearnedDangerousZones[center] = (LearnedDangerousZones[center] + dangerLevel) / 2.0f;
        }
        else
        {
            LearnedDangerousZones[center] = dangerLevel;
        }
    }

    public void LearnPlayerPattern(string patternName)
    {
        if (LearnedPlayerPatterns.ContainsKey(patternName))
        {
            LearnedPlayerPatterns[patternName]++;
        }
        else
        {
            LearnedPlayerPatterns[patternName] = 1;
        }
    }

    public void LearnToAvoidAction(string actionName)
    {
        if (!AvoidedActions.Contains(actionName))
        {
            AvoidedActions.Add(actionName);
        }
    }

    // Renforcement (ajouté)
    public void RewardAction(string actionName, float reward)
    {
        if (ActionRewards.ContainsKey(actionName))
        {
            ActionRewards[actionName] += reward;
        }
        else
        {
            ActionRewards[actionName] = reward;
        }
    }

    public void PunishAction(string actionName, float penalty)
    {
        RewardAction(actionName, -penalty); // Récompense négative
    }

    // PatternMemory (ajouté)
    public void StoreGameState(GameStateSnapshot state)
    {
        PatternMemory.Add(state);
        if (PatternMemory.Count > 50) PatternMemory.RemoveAt(0); // Limiter la taille
    }

    public bool RecognizePattern(List<GameStateSnapshot> recentStates)
    {
        // Recherche simplifiée dans PatternMemory
        for (int i = 0; i < PatternMemory.Count - recentStates.Count; i++)
        {
            bool matches = true;
            for (int j = 0; j < recentStates.Count; j++)
            {
                if (!recentStates[j].Equals(PatternMemory[i + j])) // Méthode Equals à implémenter dans GameStateSnapshot
                {
                    matches = false;
                    break;
                }
            }
            if (matches) return true;
        }
        return false;
    }

    // Ajouté : Mise à jour des nouveaux aspects
    public void UpdatePredictedReward(string actionName, float prediction) => PredictedRewards[actionName] = prediction;
    public void UpdateContextualWeight(string context, float weight) => ContextualWeights[context] = weight;
    public void UpdateEmotionalLearningFactor(string emotion, float factor) => EmotionalLearningFactors[emotion] = factor;
    public void UpdateSpatialLearningMap(Vector2 location, float effectiveness) => SpatialLearningMap[location] = effectiveness;
    public void UpdateSocialLearningPattern(string pattern, float observedValue) => SocialLearningPatterns[pattern] = observedValue;
    public void AdjustSurvivalBias(float adjustment) => SurvivalBias = Math.Max(0.1f, SurvivalBias + adjustment);
    public void UpdateInformationTrust(string source, float trust) => InformationTrust[source] = Math.Max(0.0f, Math.Min(1.0f, trust));

    public void Reset()
    {
        LearnedWeights.Clear();
        LearnedDangerousZones.Clear();
        LearnedPlayerPatterns.Clear();
        AvoidedActions.Clear();
        PatternMemory.Clear();
        ActionRewards.Clear();
        PredictedRewards.Clear(); // Réinitialisation
        ContextualWeights.Clear(); // Réinitialisation
        EmotionalLearningFactors.Clear(); // Réinitialisation
        SpatialLearningMap.Clear(); // Réinitialisation
        SocialLearningPatterns.Clear(); // Réinitialisation
        SurvivalBias = 1.0f; // Réinitialisation
        InformationTrust.Clear(); // Réinitialisation
    }
}

// Structure pour un instantané d'état du jeu (ajouté pour PatternMemory)
public struct GameStateSnapshot : IEquatable<GameStateSnapshot>
{
    public Vector2 PlayerPosition { get; set; }
    public AIState PlayerState { get; set; }
    public List<Entity> NearbyEnemies { get; set; }
    public float TimeStamp { get; set; }

    public GameStateSnapshot(Vector2 pos, AIState state, List<Entity> enemies, float time)
    {
        PlayerPosition = pos;
        PlayerState = state;
        NearbyEnemies = new List<Entity>(enemies); // Copie
        TimeStamp = time;
    }

    public bool Equals(GameStateSnapshot other)
    {
        // Comparaison approximative pour la reconnaissance de motif
        return Vector2.Distance(PlayerPosition, other.PlayerPosition) < 1.0f &&
               PlayerState == other.PlayerState &&
               NearbyEnemies.Count == other.NearbyEnemies.Count; // Peut être plus sophistiqué
    }
}

// Composant Profil Comportemental (16)
public struct AIBehaviorProfileComponent : IComponent, IReusableComponent
{
    public Dictionary<string, float> BehavioralTendencies { get; set; } = new();

    public AIBehaviorProfileComponent()
    {
        BehavioralTendencies = new Dictionary<string, float>();
    }

    public void SetTendency(string behavior, float tendency) => BehavioralTendencies[behavior] = Math.Max(0.0f, Math.Min(1.0f, tendency));
    public float GetTendency(string behavior) => BehavioralTendencies.ContainsKey(behavior) ? BehavioralTendencies[behavior] : 0.0f;

    public void Reset()
    {
        BehavioralTendencies.Clear();
    }
}

// Composant Groupe (19)
public struct AIGroupComponent : IComponent, IReusableComponent
{
    public int GroupId { get; set; } = -1;
    public List<Entity> Members { get; set; } = new();
    public Vector2 FormationCenter { get; set; } = Vector2.Zero;
    public FormationType FormationType { get; set; } = FormationType.Line;
    public Entity Leader { get; set; } = default;
    // Ajouté : Hiérarchie de commandement (5. Canal de commandement)
    public Dictionary<Entity, Entity> CommandStructure { get; set; } = new(); // Clé = Subordonné, Valeur = Chef direct

    public AIGroupComponent(int groupId, FormationType formation = FormationType.Line, Entity leader = default)
    {
        GroupId = groupId;
        Members = new List<Entity>();
        FormationCenter = Vector2.Zero;
        FormationType = formation;
        Leader = leader;
        CommandStructure = new Dictionary<Entity, Entity>(); // Initialisation
    }

    public void Reset()
    {
        GroupId = -1;
        Members.Clear();
        FormationCenter = Vector2.Zero;
        FormationType = FormationType.Line;
        Leader = default;
        CommandStructure.Clear(); // Réinitialisation de la hiérarchie
    }
}

public enum FormationType
{
    Line,
    Column,
    Wedge,
    Circle,
    Defensive
}

// --- 140.3. NOUVEAUX COMPOSANTS POUR LES SUGGESTIONS ---

// Composant pour la réflexion cognitive (1. Méta-raisonnement)
public struct AICognitiveReflectionComponent : IComponent, IReusableComponent
{
    public Dictionary<string, float> DecisionQualityScores { get; set; } = new(); // Clé = nom de la décision, Valeur = score de qualité
    public float OverallCognitiveEfficiency { get; set; } = 1.0f; // Efficacité globale

    public void UpdateDecisionQuality(string decisionName, float qualityScore)
    {
        if (DecisionQualityScores.ContainsKey(decisionName))
        {
            // Moyenne glissante
            DecisionQualityScores[decisionName] = (DecisionQualityScores[decisionName] + qualityScore) / 2.0f;
        }
        else
        {
            DecisionQualityScores[decisionName] = qualityScore;
        }
    }

    public void Reset()
    {
        DecisionQualityScores.Clear();
        OverallCognitiveEfficiency = 1.0f;
    }
}

// Composant pour la communication (2. Langage IA simplifié, 3. Système de négociation, 4. Communication émotionnelle, 5. Canal de commandement)
public struct AICommunicationChannelComponent : IComponent, IReusableComponent
{
    public AIFaction ChannelFaction { get; set; } // Sur quel canal la communication est-elle autorisée
    public List<AICommunicationMessage> ReceivedMessages { get; set; } = new(); // Messages reçus
    public List<AICommunicationMessage> SentMessages { get; set; } = new(); // Messages envoyés
    public float EmotionalSignalStrength { get; set; } = 0.0f; // Niveau de stress/colère transmis (4. Communication émotionnelle)

    public void SendMessage(AICommunicationMessage message)
    {
        // Envoyer via EventBus ou un autre mécanisme
        EventBus.Instance.Publish(message);
        SentMessages.Add(message);
    }

    public void ReceiveMessage(AICommunicationMessage message)
    {
        ReceivedMessages.Add(message);
        // Gérer les signaux émotionnels
        if (message.Type == AICommunicationType.EmotionalStress)
        {
            EmotionalSignalStrength = Math.Max(EmotionalSignalStrength, message.EmotionalValue);
        }
    }

    public void Reset()
    {
        ChannelFaction = AIFaction.Neutral;
        ReceivedMessages.Clear();
        SentMessages.Clear();
        EmotionalSignalStrength = 0.0f;
    }
}

public struct AICommunicationMessage : IMessage
{
    public Entity Sender { get; set; }
    public AICommunicationType Type { get; set; }
    public string Content { get; set; } // Peut être structuré en JSON/XML
    public float EmotionalValue { get; set; } // Valeur pour les signaux émotionnels (ex: stress = 0.8)
    public AIFaction TargetFaction { get; set; } // Pour les canaux de communication
    public Entity TargetEntity { get; set; } // Pour les communications ciblées

    public AICommunicationMessage(Entity sender, AICommunicationType type, string content, AIFaction targetFaction = AIFaction.Neutral, Entity target = default, float emotion = 0.0f)
    {
        Sender = sender;
        Type = type;
        Content = content;
        TargetFaction = targetFaction;
        TargetEntity = target;
        EmotionalValue = emotion;
    }
}

public enum AICommunicationType
{
    Alert,      // "Menace détectée"
    RequestHelp, // "Besoin d'aide"
    ReportStatus, // "Sain et sauf"
    EmotionalStress, // "Stress élevé"
    NegotiationOffer, // "Proposition d'alliance"
    CommandOrder, // "Ordre de déplacement"
    // ...
}

// Composant pour la négociation (3. Système de négociation)
public struct AINegotiationComponent : IComponent, IReusableComponent
{
    public Dictionary<Entity, float> TrustRating { get; set; } = new(); // Clé = Entité alliée, Valeur = Niveau de confiance
    public List<AINegotiationOffer> PendingOffers { get; set; } = new(); // Offres en attente

    public void AddTrust(Entity ally, float amount) => TrustRating[ally] = TrustRating.GetValueOrDefault(ally, 0.5f) + amount;
    public void AddPendingOffer(AINegotiationOffer offer) => PendingOffers.Add(offer);

    public void Reset()
    {
        TrustRating.Clear();
        PendingOffers.Clear();
    }
}

public class AINegotiationOffer
{
    public Entity From { get; set; }
    public Entity To { get; set; }
    public string OfferType { get; set; } // "AllianceTemporaire", "EchangeInfos", etc.
    public Dictionary<string, object> Terms { get; set; } = new(); // Détails de l'offre
    public float ValidityTime { get; set; } // Temps avant expiration

    public AINegotiationOffer(Entity from, Entity to, string type, Dictionary<string, object> terms, float validity)
    {
        From = from;
        To = to;
        OfferType = type;
        Terms = terms;
        ValidityTime = validity;
    }
}

// Composant pour le comportement de camouflage (5. Comportement de camouflage)
public struct AICamouflageComponent : IComponent, IReusableComponent
{
    public float CamouflageEffectiveness { get; set; } = 0.0f; // 0.0 = visible, 1.0 = invisible
    public float EnvironmentalMatchScore { get; set; } = 0.0f; // Score de correspondance avec l'environnement
    public float MovementNoiseReduction { get; set; } = 0.0f; // Réduction du bruit lié au mouvement

    public AICamouflageComponent(float effectiveness = 0.0f, float matchScore = 0.0f, float noiseReduction = 0.0f)
    {
        CamouflageEffectiveness = effectiveness;
        EnvironmentalMatchScore = matchScore;
        MovementNoiseReduction = noiseReduction;
    }

    public void Reset()
    {
        CamouflageEffectiveness = 0.0f;
        EnvironmentalMatchScore = 0.0f;
        MovementNoiseReduction = 0.0f;
    }
}

// Composant pour la gestion de la gravité variable (4. Réaction à la gravité variable)
public struct AIGravityAdaptationComponent : IComponent, IReusableComponent
{
    public float CurrentGravityMultiplier { get; set; } = 1.0f; // Multiplieur de gravité actuel
    public float JumpForceAdjustment { get; set; } = 0.0f; // Ajustement de la force de saut
    public float MovementSpeedAdjustment { get; set; } = 0.0f; // Ajustement de la vitesse de déplacement

    public AIGravityAdaptationComponent(float gravity = 1.0f)
    {
        CurrentGravityMultiplier = gravity;
        JumpForceAdjustment = 0.0f;
        MovementSpeedAdjustment = 0.0f;
    }

    public void Reset()
    {
        CurrentGravityMultiplier = 1.0f;
        JumpForceAdjustment = 0.0f;
        MovementSpeedAdjustment = 0.0f;
    }
}

// --- 140.4. MESSAGES/EVENTS (17) ---
public class UnderAttackEvent : IMessage
{
    public Entity Attacker { get; }
    public Entity Target { get; }
    public UnderAttackEvent(Entity attacker, Entity target) { Attacker = attacker; Target = target; }
}

public class TargetLostEvent : IMessage
{
    public Entity Target { get; }
    public Entity Observer { get; }
    public TargetLostEvent(Entity target, Entity observer) { Target = target; Observer = observer; }
}

public class HeardNoiseEvent : IMessage
{
    public Entity Source { get; }
    public Entity Observer { get; }
    public Vector2 NoisePosition { get; }
    public float NoiseLevel { get; }
    public HeardNoiseEvent(Entity source, Entity observer, Vector2 pos, float level) { Source = source; Observer = observer; NoisePosition = pos; NoiseLevel = level; }
}

public class ThreatSharedEvent : IMessage
{
    public Entity SharingEntity { get; }
    public int SharedTargetId { get; }
    public float SharedThreatLevel { get; }
    public ThreatSharedEvent(Entity entity, int targetId, float threat) { SharingEntity = entity; SharedTargetId = targetId; SharedThreatLevel = threat; }
}

// --- 140.5. NOUVEAUX SYSTÈMES ---

// Système de base pour l'IA (orchestre les autres systèmes) - Devient un manager
public class AISystemManager : ISystem // Renommé pour clarté
{
    private EntityManager _entityManager;
    private InputManager _inputManager;
    private PhysicsSystem _physicsSystem;
    private NavMesh _navMesh;
    private Dictionary<Type, ISystem> _subSystems = new(); // Dictionnaire des sous-systèmes
    private SystemProfiler _profiler = new SystemProfiler();
    private AIParallelJobManager _jobManager; // Nouveau gestionnaire de jobs
    private AIPerformanceManager _perfManager; // Nouveau gestionnaire de performance
    // Ajouté : Budget d'allocations (5. FrameBudgetAllocator)
    private AIFrameBudgetAllocator _frameBudgetAllocator;
    // Ajouté : Monitor de ressources (5. AIResourceMonitor)
    private AIResourceMonitorSystem _resourceMonitor;

    public AISystemManager(EntityManager entityManager, InputManager inputManager, PhysicsSystem physicsSystem, NavMesh navMesh)
    {
        _entityManager = entityManager;
        _inputManager = inputManager;
        _physicsSystem = physicsSystem;
        _navMesh = navMesh;

        // Instancier les sous-systèmes
        var perceptionSystem = new AIPerceptionSystem(_entityManager, _physicsSystem);
        var threatSystem = new AIThreatSystem(_entityManager);
        var memorySystem = new AIMemorySystem(_entityManager);
        var stateSystem = new AIStateSystem(_entityManager);
        var decisionSystem = new AIDecisionSystem(_entityManager, _physicsSystem, _navMesh, threatSystem, memorySystem, stateSystem);
        var movementSystem = new AIMovementSystem(_entityManager, _physicsSystem, _navMesh);
        var combatSystem = new AICombatSystem(_entityManager);
        var groupSystem = new AIGroupSystem(_entityManager);
        var sharedMemorySystem = new AISharedMemorySystem(_entityManager);
        var debugOverlaySystem = new AIDebugOverlaySystem(_entityManager);
        var replaySystem = new AIReplaySystem(_entityManager);
        var stressTestSystem = new AIStressTestSystem(_entityManager, this);
        // Ajoutés : nouveaux systèmes
        var emotionSystem = new AIEmotionSystem(_entityManager);
        var weatherAdaptationSystem = new AIWeatherAdaptationSystem(_entityManager);
        var communicationSystem = new AICommunicationSystem(_entityManager);
        var reflectionSystem = new AICognitiveReflectionSystem(_entityManager);
        var resourceMonitor = new AIResourceMonitorSystem(_entityManager);

        // Enregistrer les sous-systèmes
        _subSystems[typeof(AIPerceptionSystem)] = perceptionSystem;
        _subSystems[typeof(AIThreatSystem)] = threatSystem;
        _subSystems[typeof(AIMemorySystem)] = memorySystem;
        _subSystems[typeof(AIStateSystem)] = stateSystem;
        _subSystems[typeof(AIDecisionSystem)] = decisionSystem;
        _subSystems[typeof(AIMovementSystem)] = movementSystem;
        _subSystems[typeof(AICombatSystem)] = combatSystem;
        _subSystems[typeof(AIGroupSystem)] = groupSystem;
        _subSystems[typeof(AISharedMemorySystem)] = sharedMemorySystem;
        _subSystems[typeof(AIDebugOverlaySystem)] = debugOverlaySystem;
        _subSystems[typeof(AIReplaySystem)] = replaySystem;
        _subSystems[typeof(AIStressTestSystem)] = stressTestSystem;
        // Ajoutés
        _subSystems[typeof(AIEmotionSystem)] = emotionSystem;
        _subSystems[typeof(AIWeatherAdaptationSystem)] = weatherAdaptationSystem;
        _subSystems[typeof(AICommunicationSystem)] = communicationSystem;
        _subSystems[typeof(AICognitiveReflectionSystem)] = reflectionSystem;
        _subSystems[typeof(AIResourceMonitorSystem)] = resourceMonitor;

        _jobManager = new AIParallelJobManager(perceptionSystem, threatSystem, decisionSystem);
        _perfManager = new AIPerformanceManager(_entityManager);
        _frameBudgetAllocator = new AIFrameBudgetAllocator(_subSystems.Values); // Exemple
        _resourceMonitor = resourceMonitor; // Référence pour la gestion
    }

    public T GetSubSystem<T>() where T : ISystem
    {
        return _subSystems[typeof(T)] as T;
    }

    public void Initialize()
    {
        _profiler.Start("Initialize");
        foreach (var sys in _subSystems.Values)
        {
            sys.Initialize();
        }
        _jobManager.Initialize();
        _perfManager.Initialize();
        _frameBudgetAllocator.Initialize();
        _resourceMonitor.Initialize();
        _profiler.Stop("Initialize");
    }

    public void Update(float deltaTime)
    {
        _profiler.Start("Update");

        // Mettre à jour les performances (tick adaptatif)
        _perfManager.Update(deltaTime);
        // Allouer le budget de frame
        _frameBudgetAllocator.AllocateBudget(deltaTime);

        // Le gestionnaire de jobs met à jour certains systèmes en parallèle
        _jobManager.Update(deltaTime);

        // Mettre à jour les systèmes restants séquentiellement
        var sequentialSystems = new List<ISystem> { GetSubSystem<AIStateSystem>(), GetSubSystem<AIDecisionSystem>(), GetSubSystem<AIMovementSystem>(), GetSubSystem<AICombatSystem>(), GetSubSystem<AIGroupSystem>(), GetSubSystem<AISharedMemorySystem>(), GetSubSystem<AIDebugOverlaySystem>(), GetSubSystem<AIReplaySystem>(), GetSubSystem<AIEmotionSystem>(), GetSubSystem<AIWeatherAdaptationSystem>(), GetSubSystem<AICommunicationSystem>(), GetSubSystem<AICognitiveReflectionSystem>() };
        foreach (var sys in sequentialSystems)
        {
            if (_perfManager.ShouldUpdate(sys.GetType()) && _frameBudgetAllocator.HasBudgetRemaining(sys.GetType()))
            {
                 _profiler.Start(sys.GetType().Name);
                 sys.Update(deltaTime);
                 _profiler.Stop(sys.GetType().Name);
                 _frameBudgetAllocator.ConsumeBudget(sys.GetType(), 1); // Exemple simplifié
            }
        }

        _profiler.Stop("Update");
    }

    public void Shutdown()
    {
        _profiler.Start("Shutdown");
        _jobManager.Shutdown();
        _perfManager.Shutdown();
        _frameBudgetAllocator.Shutdown();
        _resourceMonitor.Shutdown();
        foreach (var sys in _subSystems.Values)
        {
            sys.Shutdown();
        }
        _profiler.Stop("Shutdown");
        _profiler.PrintResults();
    }
}

// Système de Décision (mis à jour)
public class AIDecisionSystem : ISystem
{
    private EntityManager _entityManager;
    private PhysicsSystem _physicsSystem;
    private NavMesh _navMesh;
    private AIThreatSystem _threatSystem;
    private AIMemorySystem _memorySystem;
    private AIStateSystem _stateSystem;
    private List<AIGoal> _availableGoals = new();
    private AIGroupSystem _groupSystem;
    private AICombatSystem _combatSystem;
    // Ajouté : éléments pour les nouvelles suggestions
    private Dictionary<Entity, List<DecisionRecord>> _decisionHistory = new(); // 10. Auto-profiling cognitif (via historique)
    private AICognitiveReflectionSystem _reflectionSystem; // Référence pour évaluer les décisions

    public AIDecisionSystem(EntityManager entityManager, PhysicsSystem physicsSystem, NavMesh navMesh, AIThreatSystem threatSystem, AIMemorySystem memorySystem, AIStateSystem stateSystem)
    {
        _entityManager = entityManager;
        _physicsSystem = physicsSystem;
        _navMesh = navMesh;
        _threatSystem = threatSystem;
        _memorySystem = memorySystem;
        _stateSystem = stateSystem;
        _groupSystem = new AIGroupSystem(entityManager);
        _combatSystem = new AICombatSystem(entityManager);
        _reflectionSystem = new AICognitiveReflectionSystem(entityManager); // Injection de dépendance simplifiée

        InitializeGoals();
    }

    private void InitializeGoals()
    {
        _availableGoals.Add(new AIGoal("Combat", (entity) =>
        {
            var threat = _entityManager.GetComponent<AIThreatComponent>(entity);
            var state = _entityManager.GetComponent<AIStateComponent>(entity);
            var moral = _entityManager.GetComponent<AIMoralComponent>(entity);
            var learning = _entityManager.GetComponent<AILearningComponent>(entity);
            var profile = _entityManager.GetComponent<AIBehaviorProfileComponent>(entity);

            float score = threat.ThreatLevels.ContainsKey(threat.PrimaryTargetId) ? threat.ThreatLevels[threat.PrimaryTargetId] : 0.0f;
            score *= state.AlertLevel == AIAlertLevel.Combat ? 1.5f : 1.0f;
            score *= moral.Confidence;
            // Facteur d'apprentissage : récompenser l'attaque si efficace
            if (learning.ActionRewards.ContainsKey("Attack")) score += learning.ActionRewards["Attack"] * 0.1f;
            // Facteur de profil : les entités agressives ont un bonus
            score += profile.GetTendency("Aggressive") * 0.5f;
            // Facteur de contexte : l'IA est-elle isolée ? stressée ?
            if (state.ContextFlags.HasFlag(AIContextFlags.Isolated)) score *= 0.5f; // Moins combatif si isolé
            if (state.ContextFlags.HasFlag(AIContextFlags.Stressed)) score *= 0.8f; // Légère réduction si stressé
            return score;
        }, new List<MicroAction> { new MicroAction(AIActionType.Attack) }));

        _availableGoals.Add(new AIGoal("Flee", (entity) =>
        {
            var threat = _entityManager.GetComponent<AIThreatComponent>(entity);
            var moral = _entityManager.GetComponent<AIMoralComponent>(entity);
            var combat = _entityManager.GetComponent<AICombatComponent>(entity);
            var learning = _entityManager.GetComponent<AILearningComponent>(entity);
            var state = _entityManager.GetComponent<AIStateComponent>(entity); // Pour le contexte

            float score = threat.ThreatLevels.ContainsKey(threat.PrimaryTargetId) ? threat.ThreatLevels[threat.PrimaryTargetId] : 0.0f;
            score *= moral.Fear;
            // Pénaliser la fuite si la santé est élevée et l'apprentissage déconseille de fuir
            if (combat.Health / combat.MaxHealth > 0.8f && learning.ActionRewards.ContainsKey("Flee") && learning.ActionRewards["Flee"] < 0.0f) score *= 0.1f;
            // Boost de la fuite si contexte "LowHealth" ou "Outnumbered"
            if (state.ContextFlags.HasFlag(AIContextFlags.LowHealth)) score *= 2.0f;
            if (state.ContextFlags.HasFlag(AIContextFlags.Outnumbered)) score *= 1.5f;
            return score;
        }, new List<MicroAction> { new MicroAction(AIActionType.Flee) }));

        _availableGoals.Add(new AIGoal("Curious", (entity) =>
        {
            var curiosity = _entityManager.GetComponent<AICuriosityComponent>(entity);
            var state = _entityManager.GetComponent<AIStateComponent>(entity);
            var learning = _entityManager.GetComponent<AILearningComponent>(entity);

            float score = curiosity.ExplorationUrgency * 0.5f;
            score *= state.CurrentState == AIState.Idle ? 1.0f : 0.1f;
            // Encourager l'exploration si l'apprentissage a récemment été récompensé
            if (learning.ActionRewards.ContainsKey("Explore")) score += learning.ActionRewards["Explore"] * 0.2f;
            return score;
        }, new List<MicroAction> { new MicroAction(AIActionType.MoveTo) }));
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var aiEntities = _entityManager.GetAllEntitiesWithComponent<AIControllerComponent>();

        // foreach (var entity in aiEntities)
        // {
        //     var controller = _entityManager.GetComponent<AIControllerComponent>(entity);
        //     var sensor = _entityManager.GetComponent<AISensorComponent>(entity);
        //     var threat = _entityManager.GetComponent<AIThreatComponent>(entity);
        //     var memory = _entityManager.GetComponent<AIMemoryComponent>(entity);
        //     var state = _entityManager.GetComponent<AIStateComponent>(entity);
        //     var blackboard = _entityManager.GetComponent<AIBlackboard>(entity);
        //     var action = _entityManager.GetComponent<AIActionComponent>(entity);
        //     var moral = _entityManager.GetComponent<AIMoralComponent>(entity);
        //     var fatigue = _entityManager.GetComponent<AIFatigueComponent>(entity);
        //     var curiosity = _entityManager.GetComponent<AICuriosityComponent>(entity);
        //     var learning = _entityManager.GetComponent<AILearningComponent>(entity);
        //     var profile = _entityManager.GetComponent<AIBehaviorProfileComponent>(entity);
        //     var routine = _entityManager.GetComponent<AIRoutineComponent>(entity);
        //     var combatStats = _entityManager.GetComponent<AICombatComponent>(entity);
        //     var reflection = _entityManager.GetComponent<AICognitiveReflectionComponent>(entity); // Nouveau composant
        //     var emotion = _entityManager.GetComponent<AIEmotionComponent>(entity); // Nouveau composant (via AIEmotionSystem)

        //     fatigue.UpdateFatigue(deltaTime * fatigue.DecisionSpeedMultiplier, sensor.EnvironmentState.Temperature); // Passer la température
        //     _entityManager.AddComponent(entity, fatigue);

        //     curiosity.UpdateExplorationUrgency(deltaTime);
        //     _entityManager.AddComponent(entity, curiosity);

        //     UpdateAlertLevel(entity, sensor, threat, ref state);

        //     // Calculer le score global pour chaque objectif
        //     AIGoal bestGoal = null;
        //     float bestScore = float.MinValue;
        //     foreach (var goal in _availableGoals)
        //     {
        //         float score = goal.Scorer(entity);

        //         // Appliquer les facteurs globaux
        //         score *= moral.Confidence;
        //         score *= fatigue.AccuracyMultiplier;
        //         score += curiosity.ExplorationUrgency * 0.5f;
        //         score += profile.GetTendency("Aggressive");

        //         // Priorisation dynamique : réévaluer les actions en cours
        //         if (action.ActionType != AIActionType.None)
        //         {
        //             if (action.ActionType == AIActionType.Attack && threat.PrimaryTargetId != action.Target.Id)
        //             {
        //                 score *= 0.5f; // Réduire la priorité de l'attaque si la cible est perdue
        //             }
        //         }

        //         if (score > bestScore)
        //         {
        //             bestScore = score;
        //             bestGoal = goal;
        //         }
        //     }

        //     if (bestGoal != null && bestGoal.Actions.Count > 0)
        //     {
        //         var microAction = bestGoal.Actions[0];
        //         action.ActionType = microAction.ActionType;
        //         action.Target = microAction.Target;
        //         action.Destination = microAction.Destination;

        //         // Adaptation comportementale
        //         if (learning.AvoidedActions.Contains(action.ActionType.ToString()))
        //         {
        //             action.ActionType = SelectAlternativeAction(action.ActionType);
        //         }

        //         // Calculer la destination d'exploration autonome si nécessaire
        //         if (action.ActionType == AIActionType.MoveTo && action.Destination == Vector2.Zero)
        //         {
        //             action.Destination = curiosity.CalculateAutonomousExplorationTarget();
        //         }

        //         // Calculer la destination de retrait tactique si nécessaire
        //         if (action.ActionType == AIActionType.Flee && combatStats.Health / combatStats.MaxHealth < 0.3f)
        //         {
        //             action.Destination = CalculateTacticalRetreatDestination(entity, threat);
        //             action.ActionType = AIActionType.TacticalRetreat;
        //         }

        //         // --- NOUVEAUTÉS ---
        //         // 3. Auto-diagnostic : Comparer l'action planifiée avec l'état réel
        //         if (action.ActionType == AIActionType.Attack && threat.PrimaryTargetId == -1)
        //         {
        //             Console.WriteLine($"[DEBUG] IA {entity.Id} a décidé d'attaquer mais aucune menace n'est présente.");
        //             // Ajouter un score d'inconsistance ou déclencher un correctif
        //         }

        //         // 4. Anticipation d'événements : Simuler les conséquences (stub)
        //         SimulateActionOutcome(entity, action);

        //         // 7. Réflexion post-action : Analyser le résultat de la précédente action (stub)
        //         AnalyzePreviousActionOutcome(entity);

        //         // 10. Auto-profiling cognitif : Mettre à jour l'historique
        //         RecordDecision(entity, bestGoal?.Name, action.ActionType, bestScore);

        //         // 9. Raisonnement contrefactuel : "Et si j'avais choisi X ?" (stub)
        //         EvaluateCounterfactuals(entity, bestGoal?.Name, action.ActionType);
        //         // ------------------
        //     }

        //     // Enregistrer la décision pour le replay
        //     var replaySys = ServiceLocator.GetService<AIReplaySystem>();
        //     if (replaySys != null) replaySys.RecordDecision(entity, bestGoal?.Name, action.ActionType);

        //     _entityManager.AddComponent(entity, state);
        //     _entityManager.AddComponent(entity, action);
        //     _entityManager.AddComponent(entity, routine);
        //     _entityManager.AddComponent(entity, reflection); // Mettre à jour la réflexion cognitive
        // }
    }

    // private void UpdateAlertLevel(Entity entity, AISensorComponent sensor, AIThreatComponent threat, ref AIStateComponent state)
    // {
    //     // ... (logique existante) ...
    //     // Mettre à jour les ContextFlags
    //     state.ContextFlags = AIContextFlags.None;
    //     if (/* condition pour être isolé */) state.ContextFlags |= AIContextFlags.Isolated;
    //     if (/* condition pour être en groupe */) state.ContextFlags |= AIContextFlags.InGroup;
    //     if (/* condition pour être stressé */) state.ContextFlags |= AIContextFlags.Stressed;
    //     if (threat.ThreatLevels.Count > 2) state.ContextFlags |= AIContextFlags.Outnumbered; // Exemple
    //     if (GetSubSystem<AICombatComponent>(entity).Health / GetSubSystem<AICombatComponent>(entity).MaxHealth < 0.3f) state.ContextFlags |= AIContextFlags.LowHealth; // Exemple
    // }

    // private AIActionType SelectAlternativeAction(AIActionType originalAction) { /* ... */ return originalAction; }
    // private Vector2 CalculateTacticalRetreatDestination(Entity entity, AIThreatComponent threat) { /* ... */ return Vector2.Zero; }
    // private void SimulateActionOutcome(Entity entity, AIActionComponent action) { /* ... */ } // Stub
    // private void AnalyzePreviousActionOutcome(Entity entity) { /* ... */ } // Stub
    // private void EvaluateCounterfactuals(Entity entity, string chosenGoal, AIActionType chosenAction) { /* ... */ } // Stub

    // private void RecordDecision(Entity entity, string goal, AIActionType action, float score)
    // {
    //     if (!_decisionHistory.ContainsKey(entity))
    //     {
    //         _decisionHistory[entity] = new List<DecisionRecord>();
    //     }
    //     _decisionHistory[entity].Add(new DecisionRecord(Time.ElapsedTime, goal, action, score));
    //     if (_decisionHistory[entity].Count > 10) _decisionHistory[entity].RemoveAt(0); // Limiter l'historique
    // }

    public void Shutdown() { }
}

public struct DecisionRecord
{
    public float Time { get; }
    public string Goal { get; }
    public AIActionType Action { get; }
    public float Score { get; }

    public DecisionRecord(float time, string goal, AIActionType action, float score)
    {
        Time = time;
        Goal = goal;
        Action = action;
        Score = score;
    }
}

// Système de Perception (mis à jour)
public class AIPerceptionSystem : ISystem
{
    private EntityManager _entityManager;
    private PhysicsSystem _physicsSystem;

    public AIPerceptionSystem(EntityManager entityManager, PhysicsSystem physicsSystem)
    {
        _entityManager = entityManager;
        _physicsSystem = physicsSystem;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var sensorEntities = _entityManager.GetAllEntitiesWithComponent<AISensorComponent>();

        // foreach (var entity in sensorEntities)
        // {
        //     var sensor = _entityManager.GetComponent<AISensorComponent>(entity);
        //     var controller = _entityManager.GetComponent<AIControllerComponent>(entity);
        //     var myRb = _entityManager.GetComponent<RigidBodyComponent>(entity);

        //     if (myRb == null) continue;

        //     sensor.DetectedEntities.Clear();
        //     sensor.UpdateEffectivePerceptions(); // Mettre à jour les perceptions en fonction de l'environnement et de la fatigue
        //     sensor.UpdateSensoryFatigue(deltaTime); // Mettre à jour la fatigue sensorielle

        //     // ... (logique de détection existante) ...

        //     // --- NOUVEAUTÉS ---
        //     // 4. Réaction à la pollution/gaz (4. Réaction à la pollution / gaz)
        //     if (IsGasPresent(myRb.Position)) // Stub
        //     {
        //         // Réduire la portée de la vue, augmenter le stress
        //         sensor.PerceptionMods.SightRangeMultiplier *= 0.5f;
        //         var moral = _entityManager.GetComponent<AIMoralComponent>(entity);
        //         moral.Fear += 0.1f * deltaTime;
        //         _entityManager.AddComponent(entity, moral);
        //     }

        //     // 5. Réaction à la lumière artificielle (5. Réaction à la lumière artificielle)
        //     if (IsBrightLightNearby(myRb.Position)) // Stub
        //     {
        //         // Augmenter la probabilité de détecter des cibles
        //         sensor.PerceptionMods.VisibilityMultiplier *= 1.5f;
        //     }

        //     // Appliquer le flou perceptuel à la vision périphérique
        //     foreach (var kvp in sensor.LastSeenPositions)
        //     {
        //         var entry = kvp.Value;
        //         if (Vector2.Distance(myRb.Position, entry.Position) > sensor.GetEffectiveSightRange() * 0.7f)
        //         {
        //             float blurAmount = sensor.PerceptualBlurRadius * sensor.SensoryFatigue;
        //             entry.Position += new Vector2((float)(new Random().NextDouble() - 0.5) * blurAmount, (float)(new Random().NextDouble() - 0.5) * blurAmount);
        //             sensor.LastSeenPositions[kvp.Key] = entry;
        //         }
        //     }

        //     _entityManager.AddComponent(entity, sensor);
        // }
    }

    // private bool IsGasPresent(Vector2 pos) { /* ... */ return false; } // Stub
    // private bool IsBrightLightNearby(Vector2 pos) { /* ... */ return false; } // Stub

    public void Shutdown() { }
}

// Système de Groupe (mis à jour)
public class AIGroupSystem : ISystem
{
    private EntityManager _entityManager;

    public AIGroupSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var groupComponents = _entityManager.GetAllEntitiesWithComponent<AIGroupComponent>();

        // foreach (var groupComp in groupComponents)
        // {
        //     // Calculer le centre de la formation
        //     Vector2 center = Vector2.Zero;
        //     foreach (var member in groupComp.Members)
        //     {
        //         var rb = _entityManager.GetComponent<RigidBodyComponent>(member);
        //         if (rb != null) center += rb.Position;
        //     }
        //     center /= Math.Max(1, groupComp.Members.Count);
        //     groupComp.FormationCenter = center;

        //     // Mettre à jour le moral collectif
        //     float collectiveMorale = CalculateCollectiveMorale(groupComp.Members);
        //     foreach (var member in groupComp.Members)
        //     {
        //         var moralComp = _entityManager.GetComponent<AIMoralComponent>(member);
        //         moralComp.CollectiveMorale = collectiveMorale;
        //         _entityManager.AddComponent(member, moralComp);
        //     }

        //     // Partager la mémoire entre alliés (via AISharedMemorySystem)
        //     ShareMemoryWithinGroup(groupComp);

        //     // --- NOUVEAUTÉS ---
        //     // 5. Comportement de leadership (5. Comportement de leadership)
        //     if (groupComp.Leader != null && _entityManager.EntityExists(groupComp.Leader))
        //     {
        //         BroadcastOrdersFromLeader(groupComp.Leader, groupComp.Members, groupComp.CommandStructure); // Utiliser la hiérarchie
        //     }

        //     // 1. Comportement de survie collective (1. Comportement de survie collective)
        //     ProtectWeakGroupMembers(groupComp);

        //     // ... (logique de formation existante) ...
        // }
    }

    // private float CalculateCollectiveMorale(List<Entity> members) { /* ... */ return 1.0f; }
    // private void ShareMemoryWithinGroup(AIGroupComponent groupComp) { /* ... */ }
    // private void BroadcastOrdersFromLeader(Entity leader, List<Entity> members, Dictionary<Entity, Entity> hierarchy) { /* ... */ } // Stub
    // private void ProtectWeakGroupMembers(AIGroupComponent groupComp) { /* ... */ } // Stub

    public void Shutdown() { }
}

// Système de Mémoire Partagée (ajouté)
public class AISharedMemorySystem : ISystem
{
    private EntityManager _entityManager;

    public AISharedMemorySystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var sharedMemoryComponents = _entityManager.GetAllEntitiesWithComponent<AISharedMemoryComponent>();

        // foreach (var entity in sharedMemoryComponents)
        // {
        //     var sharedMem = _entityManager.GetComponent<AISharedMemoryComponent>(entity);
        //     var groupComp = _entityManager.GetComponent<AIGroupComponent>(entity);

        //     if (groupComp != null)
        //     {
        //         foreach (var member in groupComp.Members)
        //         {
        //             if (member == entity) continue;

        //             var otherSharedMem = _entityManager.GetComponent<AISharedMemoryComponent>(member);
        //             if (otherSharedMem != null)
        //             {
        //                 foreach (var posEntry in otherSharedMem.SharedPositions)
        //                 {
        //                     sharedMem.SharedPositions[posEntry.Key] = posEntry.Value;
        //                 }
        //                 foreach (var targetEntry in otherSharedMem.SharedTargets)
        //                 {
        //                     sharedMem.SharedTargets[targetEntry.Key] = targetEntry.Value;
        //                 }
        //                 // 3. Apprentissage collectif (3. Apprentissage collectif)
        //                 foreach (var patternEntry in otherSharedMem.SharedLearnedPatterns)
        //                 {
        //                     sharedMem.SharedLearnedPatterns[patternEntry.Key] = sharedMem.SharedLearnedPatterns.GetValueOrDefault(patternEntry.Key, 0) + patternEntry.Value;
        //                 }
        //             }
        //         }
        //     }

        //     _entityManager.AddComponent(entity, sharedMem);
        // }
    }

    public void Shutdown() { }
}

// Système de Combat (mis à jour)
public class AICombatSystem : ISystem
{
    private EntityManager _entityManager;

    public AICombatSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var combatEntities = _entityManager.GetAllEntitiesWithComponent<AICombatComponent>();

        // foreach (var entity in combatEntities)
        // {
        //     var combat = _entityManager.GetComponent<AICombatComponent>(entity);
        //     var threat = _entityManager.GetComponent<AIThreatComponent>(entity);
        //     var action = _entityManager.GetComponent<AIActionComponent>(entity);
        //     var moral = _entityManager.GetComponent<AIMoralComponent>(entity);
        //     var fatigue = _entityManager.GetComponent<AIFatigueComponent>(entity);

        //     if (threat.PrimaryTargetId != -1)
        //     {
        //         var targetRb = _entityManager.GetComponent<RigidBodyComponent>(GetEntityById(threat.PrimaryTargetId));
        //         if (targetRb != null)
        //         {
        //             // Visée prédictive
        //             Vector2 predictedPos = PredictTargetPosition(targetRb.Position, targetRb.Velocity, GetWeaponRange());

        //             // Gestion des munitions
        //             if (combat.Ammo <= 0) action.ActionType = AIActionType.Wait;

        //             // Gestion de l'énergie
        //             combat.ConsumeEnergy(1.0f * deltaTime);

        //             // Évaluation du risque
        //             float risk = EvaluateRisk(threat.PrimaryTargetId, targetRb);
        //             if (risk > 0.8f) moral.Fear += 0.1f * deltaTime;

        //             // Priorité de cible
        //             int prioritizedTarget = SetTargetPriority(threat.ThreatLevels);

        //             // Gestion de la peur
        //             if (moral.Fear > 0.7f) action.ActionType = AIActionType.Flee;

        //             // Gestion de la santé et retraite tactique
        //             if (combat.Health / combat.MaxHealth < 0.3f)
        //             {
        //                 if (IsTacticalRetreatFeasible(entity, threat))
        //                 {
        //                     action.ActionType = AIActionType.TacticalRetreat;
        //                 }
        //                 else
        //                 {
        //                     moral.Confidence *= 0.9f;
        //                 }
        //             }

        //             // Blessures
        //             if (IsInjured(entity)) fatigue.SpeedMultiplier *= 0.8f;

        //             // Rage
        //             if (combat.RageLevel > 0.5f) combat.IncreaseRage(0.01f * deltaTime);

        //             // --- NOUVEAUTÉS ---
        //             // 2. Adaptation tactique (2. Comportement d'adaptation tactique)
        //             AdaptTacticToEnemyType(entity, targetRb, ref action);

        //             // 4. Diversion (4. Comportement de diversion)
        //             if (ShouldCreateDiversion(entity, threat))
        //             {
        //                 action.ActionType = AIActionType.CreateDiversion;
        //             }

        //             // 3. Camouflage (3. Comportement de camouflage)
        //             if (ShouldUseCamouflage(entity, threat))
        //             {
        //                 action.ActionType = AIActionType.Camouflage;
        //             }
        //         }
        //     }

        //     _entityManager.AddComponent(entity, combat);
        //     _entityManager.AddComponent(entity, action);
        //     _entityManager.AddComponent(entity, moral);
        //     _entityManager.AddComponent(entity, fatigue);
        // }
    }

    // private Vector2 PredictTargetPosition(Vector2 currentPos, Vector2 velocity, float weaponRange) { /* ... */ return currentPos; }
    // private float GetWeaponRange() { /* ... */ return 10.0f; }
    // private float EvaluateRisk(int targetId, RigidBodyComponent targetRb) { /* ... */ return 0.5f; }
    // private int SetTargetPriority(Dictionary<int, float> threatLevels) { /* ... */ return -1; }
    // private bool IsInjured(Entity entity) { /* ... */ return false; }
    // private bool IsTacticalRetreatFeasible(Entity entity, AIThreatComponent threat) { /* ... */ return true; }
    // private void AdaptTacticToEnemyType(Entity entity, RigidBodyComponent enemyRb, ref AIActionComponent action) { /* ... */ } // Stub
    // private bool ShouldCreateDiversion(Entity entity, AIThreatComponent threat) { /* ... */ return false; } // Stub
    // private bool ShouldUseCamouflage(Entity entity, AIThreatComponent threat) { /* ... */ return false; } // Stub

    public void Shutdown() { }
}

// Système de Debug Overlay (mis à jour)
public class AIDebugOverlaySystem : ISystem
{
    private EntityManager _entityManager;
    private SystemProfiler _profiler;
    private Dictionary<Vector2, int> _perceptionHeatmap = new();

    public AIDebugOverlaySystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
        _profiler = new SystemProfiler();
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        if (!AIDebugModeManager.IsDebugEnabled) return;

        // var aiEntities = _entityManager.GetAllEntitiesWithComponent<AIControllerComponent>();

        // foreach (var entity in aiEntities)
        // {
        //     var sensor = _entityManager.GetComponent<AISensorComponent>(entity);
        //     var threat = _entityManager.GetComponent<AIThreatComponent>(entity);
        //     var state = _entityManager.GetComponent<AIStateComponent>(entity);
        //     var myRb = _entityManager.GetComponent<RigidBodyComponent>(entity);
        //     if (myRb == null) continue;

        //     // Dessiner le cône de vision
        //     DrawVisionCone(myRb.Position, myRb.Direction, sensor.FieldOfView, sensor.GetEffectiveSightRange());

        //     // Dessiner la zone d'audition
        //     DrawHearingCircle(myRb.Position, sensor.GetEffectiveHearingRange());

        //     // Dessiner les menaces perçues
        //     foreach (var kvp in threat.ThreatLevels)
        //     {
        //         var targetRb = _entityManager.GetComponent<RigidBodyComponent>(GetEntityById(kvp.Key));
        //         if (targetRb != null)
        //         {
        //             DrawThreatLine(myRb.Position, targetRb.Position, kvp.Value);
        //         }
        //     }

        //     // Dessiner l'état actuel
        //     DrawStateIndicator(myRb.Position, state.CurrentState);

        //     // Dessiner les destinations de navigation
        //     var nav = _entityManager.GetComponent<AINavigationComponent>(entity);
        //     if (nav.IsPathValid && nav.Path.Count > 0)
        //     {
        //         DrawPath(nav.Path);
        //     }

        //     // Mettre à jour la heatmap
        //     UpdateHeatmap(myRb.Position);
        // }

        // Dessiner la heatmap
        // DrawHeatmap();

        // Dessiner le graphique de performance
        // DrawPerformanceGraph(_profiler.GetRecentMetrics());

        // --- NOUVEAUTÉS ---
        // 5. AIStressVisualizer : Dessiner les zones de saturation CPU (stub)
        // DrawCPUStressZones();
        // ------------------
    }

    // private void DrawVisionCone(Vector2 pos, Vector2 dir, float fov, float range) { /* ... */ }
    // private void DrawHearingCircle(Vector2 pos, float range) { /* ... */ }
    // private void DrawThreatLine(Vector2 from, Vector2 to, float threatLevel) { /* ... */ }
    // private void DrawStateIndicator(Vector2 pos, AIState state) { /* ... */ }
    // private void DrawPath(List<Vector2> path) { /* ... */ }
    // private void UpdateHeatmap(Vector2 pos) { /* ... */ }
    // private void DrawHeatmap() { /* ... */ }
    // private void DrawPerformanceGraph(Dictionary<string, float> metrics) { /* ... */ }
    // private void DrawCPUStressZones() { /* ... */ } // Stub

    public void Shutdown() { }
}

// Système de Replay (ajouté)
public class AIReplaySystem : ISystem
{
    private EntityManager _entityManager;
    private Dictionary<Entity, List<AIReplayDecision>> _replayData = new();

    public AIReplaySystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime) { /* Logique pour gérer le mode replay */ }
    public void Shutdown() { }

    public void RecordDecision(Entity entity, string goalName, AIActionType actionType)
    {
        if (!_replayData.ContainsKey(entity))
        {
            _replayData[entity] = new List<AIReplayDecision>();
        }
        _replayData[entity].Add(new AIReplayDecision(Time.ElapsedTime, goalName, actionType));
    }

    public List<AIReplayDecision> GetReplayData(Entity entity)
    {
        return _replayData.ContainsKey(entity) ? _replayData[entity] : new List<AIReplayDecision>();
    }
}

public struct AIReplayDecision
{
    public float Time { get; }
    public string GoalName { get; }
    public AIActionType ActionType { get; }

    public AIReplayDecision(float time, string goal, AIActionType action)
    {
        Time = time;
        GoalName = goal;
        ActionType = action;
    }
}

// Système de Stress Test (ajouté)
public class AIStressTestSystem : ISystem
{
    private EntityManager _entityManager;
    private AISystemManager _systemManager;
    private int _spawnedEntityCount = 0;
    private const int TargetEntityCount = 1000;

    public AIStressTestSystem(EntityManager entityManager, AISystemManager systemManager)
    {
        _entityManager = entityManager;
        _systemManager = systemManager;
    }

    public void Initialize()
    {
        SpawnEntities(TargetEntityCount);
    }

    public void Update(float deltaTime)
    {
        MeasurePerformance();
    }

    public void Shutdown()
    {
        // Nettoyer les entités créées ?
    }

    private void SpawnEntities(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Entity newEntity = _entityManager.CreateEntity();
            // Ajouter des composants IA basiques
            _entityManager.AddComponent(newEntity, new AIControllerComponent(AIBehaviorType.Combat));
            _entityManager.AddComponent(newEntity, new AISensorComponent(5.0f, 8.0f, 90.0f));
            _entityManager.AddComponent(newEntity, new AIThreatComponent());
            _entityManager.AddComponent(newEntity, new AIStateComponent(AIState.Idle));
            _entityManager.AddComponent(newEntity, new AIActionComponent(AIActionType.Wait));
            _entityManager.AddComponent(newEntity, new AINavigationComponent());
            _entityManager.AddComponent(newEntity, new AIMemoryComponent());
            _entityManager.AddComponent(newEntity, new AIMoralComponent());
            _entityManager.AddComponent(newEntity, new AIFatigueComponent());
            _entityManager.AddComponent(newEntity, new AICuriosityComponent());
            _entityManager.AddComponent(newEntity, new AILearningComponent());
            _entityManager.AddComponent(newEntity, new AIBehaviorProfileComponent());
            _entityManager.AddComponent(newEntity, new AIRoutineComponent());
            _entityManager.AddComponent(newEntity, new AICombatComponent(100.0f, 100.0f, 100.0f));
            // Ajouter les nouveaux composants
            _entityManager.AddComponent(newEntity, new AICognitiveReflectionComponent());
            _entityManager.AddComponent(newEntity, new AICommunicationChannelComponent());
            _entityManager.AddComponent(newEntity, new AINegotiationComponent());
            _entityManager.AddComponent(newEntity, new AICamouflageComponent());
            _entityManager.AddComponent(newEntity, new AIGravityAdaptationComponent());
        }
        _spawnedEntityCount = count;
    }

    private void MeasurePerformance()
    {
        // Exemple : Calculer le nombre moyen d'entités mises à jour par frame
        // Comparer avec un objectif de FPS
        // Logguer les résultats
        // EventBus.Instance.Publish(new AIPerformanceEvent(_spawnedEntityCount, Time.ElapsedTime, 60.0f));
    }
}

// --- 140.6. NOUVEAUX SYSTÈMES POUR LES SUGGESTIONS ---

// Système d'émotions (4. AIEmotionSystem)
public class AIEmotionSystem : ISystem
{
    private EntityManager _entityManager;

    public AIEmotionSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var emotionEntities = _entityManager.GetAllEntitiesWithComponent<AIMoralComponent>(); // Utiliser MoralComponent comme base pour les émotions

        // foreach (var entity in emotionEntities)
        // {
        //     var moral = _entityManager.GetComponent<AIMoralComponent>(entity);
        //     var sensor = _entityManager.GetComponent<AISensorComponent>(entity);
        //     var threat = _entityManager.GetComponent<AIThreatComponent>(entity);

        //     // Mettre à jour les émotions en fonction des composants existants
        //     // Exemple : La peur augmente avec la menace et diminue avec la confiance
        //     moral.Fear = Math.Max(0.0f, Math.Min(1.0f, moral.Fear + (threat.ThreatLevels.Values.DefaultIfEmpty(0).Max() * 0.01f * deltaTime) - (moral.Confidence * 0.005f * deltaTime)));
        //     // Exemple : La colère augmente avec les dégâts subis (via AICombatComponent) ou les menaces
        //     var combat = _entityManager.GetComponent<AICombatComponent>(entity);
        //     moral.Anger = Math.Max(0.0f, Math.Min(1.0f, moral.Anger + (1.0f - combat.Health / combat.MaxHealth) * 0.1f * deltaTime)); // Colère liée à la perte de vie

        //     _entityManager.AddComponent(entity, moral);
        // }
    }

    public void Shutdown() { }
}

// Système d'adaptation à la météo (4. AIWeatherAdaptationSystem)
public class AIWeatherAdaptationSystem : ISystem
{
    private EntityManager _entityManager;
    private WeatherSystem _weatherSystem; // Référence à un système de météo global

    public AIWeatherAdaptationSystem(EntityManager entityManager) // Supposons WeatherSystem injecté
    {
        _entityManager = entityManager;
        // _weatherSystem = weatherSys; // Injection de dépendance
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var sensorEntities = _entityManager.GetAllEntitiesWithComponent<AISensorComponent>();

        // foreach (var entity in sensorEntities)
        // {
        //     var sensor = _entityManager.GetComponent<AISensorComponent>(entity);
        //     var moral = _entityManager.GetComponent<AIMoralComponent>(entity);

        //     // Adapter la perception en fonction de la météo
        //     var currentWeather = _weatherSystem.CurrentWeather; // Supposons un accès
        //     switch (currentWeather)
        //     {
        //         case WeatherType.Rain:
        //             sensor.PerceptionMods.SightRangeMultiplier = 0.7f; // Réduction de la vue
        //             sensor.PerceptionMods.HearingRangeMultiplier = 1.2f; // Légère augmentation de l'ouïe
        //             break;
        //         case WeatherType.Fog:
        //             sensor.PerceptionMods.SightRangeMultiplier = 0.3f; // Grande réduction de la vue
        //             break;
        //         case WeatherType.Storm:
        //             moral.Fear += 0.01f * deltaTime; // Stress météo
        //             sensor.PerceptionMods.DetectionNoiseAdditive += 0.2f; // Plus de bruit dans les capteurs
        //             break;
        //         // ... autres conditions ...
        //     }

        //     _entityManager.AddComponent(entity, sensor);
        //     _entityManager.AddComponent(entity, moral);
        // }
    }

    public void Shutdown() { }
}

// Système de communication (2. Langage IA simplifié, 3. Système de négociation, 4. Communication émotionnelle, 5. Canal de commandement)
public class AICommunicationSystem : ISystem
{
    private EntityManager _entityManager;

    public AICommunicationSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize()
    {
        EventBus.Instance.Subscribe<AICommunicationMessage>(HandleCommunicationMessage); // S'abonner aux messages
    }

    public void Update(float deltaTime)
    {
        // Gérer la logique de mise en file d'attente des messages à envoyer
        // Gérer les timers d'expiration des messages
    }

    public void Shutdown()
    {
        EventBus.Instance.Unsubscribe<AICommunicationMessage>(HandleCommunicationMessage);
    }

    private void HandleCommunicationMessage(AICommunicationMessage msg)
    {
        // Filtrer les messages selon la faction ou le canal
        // Distribuer les messages aux IA concernées
        // var recipients = GetEntitiesInFaction(msg.TargetFaction); // ou GetEntitiesInRange(msg.Sender)
        // foreach (var recipient in recipients)
        // {
        //     var channel = _entityManager.GetComponent<AICommunicationChannelComponent>(recipient);
        //     if (channel != null)
        //     {
        //         channel.ReceiveMessage(msg);
        //         _entityManager.AddComponent(recipient, channel);
        //     }
        // }
    }
}

// Système de réflexion cognitive (1. Méta-raisonnement)
public class AICognitiveReflectionSystem : ISystem
{
    private EntityManager _entityManager;

    public AICognitiveReflectionSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var reflectionEntities = _entityManager.GetAllEntitiesWithComponent<AICognitiveReflectionComponent>();

        // foreach (var entity in reflectionEntities)
        // {
        //     var reflection = _entityManager.GetComponent<AICognitiveReflectionComponent>(entity);
        //     var decisionHistory = GetDecisionHistoryForEntity(entity); // Via AIDecisionSystem ou un historique centralisé

        //     // Exemple : Calculer un score de qualité basé sur les résultats des décisions récentes
        //     float avgQuality = decisionHistory.Where(d => Time.ElapsedTime - d.Time < 5.0f).Average(d => d.Score); // Dernières 5 secondes
        //     reflection.OverallCognitiveEfficiency = avgQuality; // Mettre à jour l'efficacité globale

        //     // Enregistrer dans le composant
        //     _entityManager.AddComponent(entity, reflection);
        // }
    }

    public void Shutdown() { }
}

// Système de gestion des ressources (5. AIResourceMonitor)
public class AIResourceMonitorSystem : ISystem
{
    private EntityManager _entityManager;
    private long _lastMemoryUsage = 0;
    private float _lastFrameTime = 0.0f;

    public AIResourceMonitorSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        _lastFrameTime = deltaTime;
        _lastMemoryUsage = GC.GetTotalMemory(false); // Utilisation mémoire brute (approximative)

        // Exemple : Publier un événement de saturation
        // if (_lastFrameTime > 0.05f) // Si la frame dure plus de 50ms
        // {
        //     EventBus.Instance.Publish(new AIResourceSaturationEvent(ResourceType.CPU, _lastFrameTime));
        // }
    }

    public void Shutdown() { }

    public long GetLastMemoryUsage() => _lastMemoryUsage;
    public float GetLastFrameTime() => _lastFrameTime;
}

// Système de gestion du budget de frame (5. FrameBudgetAllocator)
public class AIFrameBudgetAllocator
{
    private Dictionary<Type, float> _systemBudgets = new(); // Budget alloué par système
    private Dictionary<Type, float> _systemConsumption = new(); // Consommation actuelle
    private float _totalFrameBudgetMs; // Budget total pour tous les systèmes IA cette frame (ex: 8ms sur 16.66fps)

    public AIFrameBudgetAllocator(IEnumerable<ISystem> systems)
    {
        _totalFrameBudgetMs = 8.0f; // Exemple : 8ms par frame pour l'IA
        foreach (var sys in systems)
        {
            _systemBudgets[sys.GetType()] = _totalFrameBudgetMs / systems.Count(); // Répartition égale initiale
            _systemConsumption[sys.GetType()] = 0.0f;
        }
    }

    public void Initialize() { }
    public void AllocateBudget(float deltaTimeMs) { /* Réallouer dynamiquement les budgets en fonction de la charge */ }
    public bool HasBudgetRemaining(Type systemType) => _systemConsumption[systemType] < _systemBudgets[systemType];
    public void ConsumeBudget(Type systemType, float amount) => _systemConsumption[systemType] += amount;
    public void Shutdown() { }
}

// Système de gestion des jobs parallèles (5. AIThreadBalancer)
public class AIParallelJobManager
{
    private AIPerceptionSystem _perceptionSystem;
    private AIThreatSystem _threatSystem;
    private AIDecisionSystem _decisionSystem;
    private List<AIParallelJobHandle> _handles = new();
    // Ajouté : ThreadBalancer (5. AIThreadBalancer)
    private AIThreadBalancer _balancer;

    public AIParallelJobManager(AIPerceptionSystem perceptionSys, AIThreatSystem threatSys, AIDecisionSystem decisionSys)
    {
        _perceptionSystem = perceptionSys;
        _threatSystem = threatSys;
        _decisionSystem = decisionSys;
        _balancer = new AIThreadBalancer(); // Nouveau gestionnaire
    }

    public void Initialize() { _balancer.Initialize(); }
    public void Update(float deltaTime)
    {
        _balancer.Update(deltaTime);

        var perceptionHandle = new AIParallelJobHandle(() => _perceptionSystem.Update(deltaTime));
        var threatHandle = new AIParallelJobHandle(() => _threatSystem.Update(deltaTime), new List<AIParallelJobHandle> { perceptionHandle });
        var decisionHandle = new AIParallelJobHandle(() => _decisionSystem.Update(deltaTime), new List<AIParallelJobHandle> { threatHandle });

        _handles.Add(perceptionHandle);
        _handles.Add(threatHandle);
        _handles.Add(decisionHandle);

        foreach (var handle in _handles)
        {
            _balancer.Schedule(handle); // Utiliser le balancer
        }
        _handles.Clear();
    }

    public void Shutdown()
    {
        _balancer.Shutdown();
    }
}

public class AIThreadBalancer
{
    private List<AIParallelJobHandle> _scheduledJobs = new();
    private int _currentThreadCount = 1;

    public void Initialize() { }
    public void Update(float deltaTime) { /* Ajuster _currentThreadCount dynamiquement */ }
    public void Schedule(AIParallelJobHandle job) { /* Exécuter le job sur un thread approprié */ }
    public void Shutdown() { }
}

// Système de graphes de comportement (5. AIBehaviorGraph)
public class AIBehaviorGraphSystem : ISystem
{
    private EntityManager _entityManager;
    // Structure pour représenter un nœud de comportement
    private Dictionary<Entity, AIBehaviorGraphNode> _currentGraphs = new();

    public AIBehaviorGraphSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize() { }
    public void Update(float deltaTime)
    {
        // var aiEntities = _entityManager.GetAllEntitiesWithComponent<AIControllerComponent>();

        // foreach (var entity in aiEntities)
        // {
        //     if (!_currentGraphs.ContainsKey(entity)) continue; // Avoir un graphe assigné

        //     var currentNode = _currentGraphs[entity];
        //     // Exécuter le nœud actuel (Action, Condition, etc.)
        //     // Passer au nœud suivant en fonction des résultats
        //     var nextState = currentNode.Execute(entity, _entityManager, deltaTime);
        //     if (nextState != null) _currentGraphs[entity] = nextState;
        // }
    }

    public void Shutdown() { }
}

public abstract class AIBehaviorGraphNode
{
    public List<AIBehaviorGraphNode> Children { get; set; } = new();
    public abstract AIBehaviorGraphNode Execute(Entity entity, EntityManager entityManager, float deltaTime);
}

// --- 140.7. MESSAGES DE DEBUG ---
public class AIDecisionEvent : IMessage
{
    public Entity Entity { get; }
    public string SelectedGoal { get; }
    public AIActionType SelectedAction { get; }
    public AIDecisionEvent(Entity entity, string goal, AIActionType action) { Entity = entity; SelectedGoal = goal; SelectedAction = action; }
}

public class AIPerformanceEvent : IMessage
{
    public int EntityCount { get; }
    public float CurrentTime { get; }
    public float TargetFPS { get; }
    public AIPerformanceEvent(int count, float time, float target) { EntityCount = count; CurrentTime = time; TargetFPS = target; }
}

public class AIResourceSaturationEvent : IMessage // Ajouté pour le monitoring
{
    public ResourceType Type { get; }
    public float CurrentLoad { get; }
    public AIResourceSaturationEvent(ResourceType type, float load) { Type = type; CurrentLoad = load; }
}

public enum ResourceType
{
    CPU,
    Memory,
    Network
}

// --- 140.8. TIME HELPER ---
public static class Time
{
    public static float ElapsedTime => (float)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0f;
}

// --- 140.9. MANAGERS DE DEBUG ---
public static class AIDebugModeManager
{
    public static bool IsDebugEnabled { get; set; } = false;
    public static bool IsSlowMotionEnabled { get; set; } = false;
    public static bool IsGhostModeEnabled { get; set; } = false;
    public static bool IsReplayModeEnabled { get; set; } = false;
    public static bool IsStressTestRunning { get; set; } = false;
    // ... autres modes ...
}

// --- 140.10. PROFILING ---
public class SystemProfiler
{
    private Dictionary<string, long> _startTimes = new();
    private Dictionary<string, long> _totalTimes = new();
    private Dictionary<string, int> _callCounts = new();
    private Queue<float> _recentMetrics = new();
    private const int MetricHistorySize = 100;

    public void Start(string operation)
    {
        _startTimes[operation] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void Stop(string operation)
    {
        if (_startTimes.TryGetValue(operation, out long startTime))
        {
            long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;
            _totalTimes[operation] = _totalTimes.GetValueOrDefault(operation, 0) + elapsed;
            _callCounts[operation] = _callCounts.GetValueOrDefault(operation, 0) + 1;
            _startTimes.Remove(operation);
        }
    }

    public Dictionary<string, float> GetRecentMetrics()
    {
        var metrics = new Dictionary<string, float>();
        foreach (var kvp in _totalTimes)
        {
            metrics[kvp.Key] = (float)kvp.Value / Math.Max(1, _callCounts[kvp.Key]);
        }
        return metrics;
    }

    public void PrintResults()
    {
        Console.WriteLine("--- Profiling Results ---");
        foreach (var kvp in _totalTimes)
        {
            string op = kvp.Key;
            long totalTime = kvp.Value;
            int calls = _callCounts[op];
            Console.WriteLine($"{op}: Total={totalTime}ms, Calls={calls}, Avg={(float)totalTime / calls:F2}ms");
        }
    }
}