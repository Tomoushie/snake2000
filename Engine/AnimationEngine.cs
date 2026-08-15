using System;
using System.Collections.Generic;
using System.Linq;

// --- 137. CORE ENGINE (Concepts repris) ---
// IManager, IMessage, EventBus, IComponent, ISystem, Entity, EntityManager sont définis dans Engine.cs
// Vector2, Vector3 sont définis dans Engine.cs ou ailleurs

// --- CLASSES UTILITAIRES ---
public static class MathUtils
{
    public static class AngleHelper
    {
        public static float LerpAngle(float a, float b, float t)
        {
            float delta = ((b - a + 180f) % 360f) - 180f;
            float result = a + delta * t;
            result = (result + 360f) % 360f;
            return result;
        }
    }
}

// --- 141. ANIMATION ENGINE (Animations) ---

// Identifiant unique pour un os (remplace les chaînes)
public struct BoneId : IEquatable<BoneId>
{
    public int Index { get; }
    public BoneId(int index) => Index = index;
    public static readonly BoneId Invalid = new BoneId(-1);

    public bool Equals(BoneId other) => Index == other.Index;
    public override bool Equals(object obj) => obj is BoneId other && Equals(other);
    public override int GetHashCode() => Index.GetHashCode();
    public static bool operator ==(BoneId left, BoneId right) => left.Equals(right);
    public static bool operator !=(BoneId left, BoneId right) => !left.Equals(right);
}

// Données de transformation pour un seul os
public struct BoneTransform
{
    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public BoneTransform(Vector2 pos, float rot) { Position = pos; Rotation = rot; }
}

// Données d'un os dans le squelette
public struct BoneDefinition
{
    public BoneId Id { get; set; }
    public int ParentIndex { get; set; } // -1 pour la racine
    public BoneTransform BindPose { get; set; }
    public BoneTransform InverseBindPose { get; set; }

    public BoneDefinition(int index, int parentIndex, BoneTransform bindPose, BoneTransform invBindPose)
    {
        Id = new BoneId(index);
        ParentIndex = parentIndex;
        BindPose = bindPose;
        InverseBindPose = invBindPose;
    }
}

// Squelette : définit la structure commune
public class Skeleton
{
    public BoneDefinition[] Bones { get; }
    public Dictionary<string, int> NameToIndexMap { get; } // Toujours utile pour les assets
    public int BoneCount => Bones.Length;

    public Skeleton(List<string> boneNames, List<int> parentIndices, List<BoneTransform> bindPoses, List<BoneTransform> invBindPoses)
    {
        if (boneNames.Count != parentIndices.Count || boneNames.Count != bindPoses.Count || boneNames.Count != invBindPoses.Count)
            throw new ArgumentException("Bone names, parent indices, bind poses, and inverse bind poses must have the same length.");

        int count = boneNames.Count;
        Bones = new BoneDefinition[count];
        NameToIndexMap = new Dictionary<string, int>();

        for (int i = 0; i < count; i++)
        {
            Bones[i] = new BoneDefinition(i, parentIndices[i], bindPoses[i], invBindPoses[i]);
            NameToIndexMap[boneNames[i]] = i;
        }
    }

    public BoneId GetBoneId(string name) => NameToIndexMap.TryGetValue(name, out var index) ? new BoneId(index) : BoneId.Invalid;
}

// Frame d'animation
public class AnimationFrame
{
    public float Time { get; }
    public BoneTransform[] BoneTransforms { get; }

    public AnimationFrame(float time, int boneCount)
    {
        Time = time;
        BoneTransforms = new BoneTransform[boneCount];
    }
}

// Clip d'animation
public class AnimationClip
{
    public string Name { get; set; }
    public float Duration { get; set; }
    public AnimationFrame[] Frames { get; set; } = Array.Empty<AnimationFrame>(); // Utilisation de tableau pour accès rapide
    public List<AnimationEvent> Events { get; set; } = new();
    public bool Loop { get; set; } = false;
    public Skeleton Skeleton { get; }

    // Champs pour la recherche incrémentale
    private int _lastFrameIndex = 0;

    public AnimationClip(string name, float duration, Skeleton skeleton)
    {
        Name = name;
        Duration = duration;
        Skeleton = skeleton;
    }

    // Méthode pour interpoler une frame selon le temps
    // Remplissage d'un buffer existant
    public void GetInterpolatedFrame(float time, BoneTransform[] outputBuffer, int startIndex = 0)
    {
        if (Frames == null || Frames.Length == 0 || outputBuffer.Length - startIndex < Skeleton.BoneCount) return;

        if (Frames.Length == 1 || time <= 0)
        {
            Array.Copy(Frames[0].BoneTransforms, 0, outputBuffer, startIndex, Skeleton.BoneCount);
            return;
        }

        AnimationFrame frameA = Frames[0];
        AnimationFrame frameB = Frames[Frames.Length - 1];
        if (time >= Frames[Frames.Length - 1].Time)
        {
            Array.Copy(Frames[Frames.Length - 1].BoneTransforms, 0, outputBuffer, startIndex, Skeleton.BoneCount);
            return;
        }

        // Recherche incrémentale avec cache de l'index
        int frameIndex = FindFrameIndex(time);
        if (frameIndex >= 0 && frameIndex < Frames.Length - 1)
        {
            frameA = Frames[frameIndex];
            frameB = Frames[frameIndex + 1];
        }

        float t = (time - frameA.Time) / (frameB.Time - frameA.Time);
        for (int i = 0; i < Skeleton.BoneCount; i++)
        {
            var posA = frameA.BoneTransforms[i].Position;
            var rotA = frameA.BoneTransforms[i].Rotation;
            var posB = frameB.BoneTransforms[i].Position;
            var rotB = frameB.BoneTransforms[i].Rotation;

            outputBuffer[startIndex + i] = new BoneTransform(
                posA + (posB - posA) * t,
                MathUtils.AngleHelper.LerpAngle(rotA, rotB, t)
            );
        }
    }

    // Helper pour trouver l'index de la frame - Utilise la recherche incrémentale
    private int FindFrameIndex(float time)
    {
        int i = _lastFrameIndex;
        if (time >= Frames[i].Time)
        {
            // Chercher vers l'avant
            while (i < Frames.Length - 1 && time > Frames[i + 1].Time) i++;
        }
        else
        {
            // Chercher vers l'arrière
            while (i > 0 && time < Frames[i].Time) i--;
        }
        _lastFrameIndex = i; // Mettre à jour le cache
        return i;
    }
}

// Événement d'animation
public class AnimationEvent
{
    public string Name { get; set; }
    public float TriggerTime { get; set; }
    public Action<Entity> Callback { get; set; }
    public AnimationEvent(string name, float time, Action<Entity> callback) { Name = name; TriggerTime = time; Callback = callback; }
}

// Transition entre états (contient la durée)
public class AnimationTransition
{
    public Func<Entity, bool> Condition { get; set; }
    public int DestinationStateId { get; set; }
    public bool HasTriggered { get; set; } = false;
    public float TransitionDuration { get; set; } = 0.2f;
}

// État d'animation
public class AnimationState
{
    public int Id { get; set; }
    public string Name { get; set; }
    public AnimationClip Clip { get; set; }
    public List<AnimationTransition> Transitions { get; set; } = new();
    public float SpeedMultiplier { get; set; } = 1.0f;
    public bool Loop { get; set; } = true;

    public AnimationState(int id, string name, AnimationClip clip) { Id = id; Name = name; Clip = clip; }
}

// --- ASSETS PARTAGÉS (Exemple pour la machine d'états) ---
public class AnimationStateMachineAsset
{
    public List<AnimationState> States { get; set; } = new();
    public int DefaultStateId { get; set; } = -1;

    public AnimationState GetState(int id) => States.FirstOrDefault(s => s.Id == id);
}

// --- COMPOSANTS ---

// Composant pour gérer l'état d'animation d'une entité (simplifié)
public struct AnimationPlaybackComponent : IComponent
{
    public int CurrentStateId { get; set; }
    public float CurrentTime { get; set; }
    public float PlaybackSpeed { get; set; }
    public bool IsPlaying { get; set; }
    public bool Loop { get; set; }
    public List<AnimationEvent> PendingEvents { get; set; }

    public AnimationPlaybackComponent() { CurrentStateId = -1; CurrentTime = 0.0f; PlaybackSpeed = 1.0f; IsPlaying = false; Loop = false; PendingEvents = new List<AnimationEvent>(); }
}

// Composant pour gérer le blending
public struct AnimationBlendingComponent : IComponent
{
    public int PreviousStateId { get; set; }
    public int TransitionTargetStateId { get; set; }
    public float TransitionProgress { get; set; }
    public float TransitionDuration { get; set; }
    public BoneTransform[] PreviousPoseBuffer { get; set; }
    public BoneTransform[] TargetPoseBuffer { get; set; }
    public BoneTransform[] FinalPoseBuffer { get; set; }

    public AnimationBlendingComponent(int boneCount)
    {
        PreviousStateId = -1;
        TransitionTargetStateId = -1;
        TransitionProgress = 1.0f;
        TransitionDuration = 0.0f;
        PreviousPoseBuffer = new BoneTransform[boneCount];
        TargetPoseBuffer = new BoneTransform[boneCount];
        FinalPoseBuffer = new BoneTransform[boneCount];
    }
}

// Composant pour gérer la machine d'états
public struct AnimationStateMachineComponent : IComponent
{
    public int CurrentStateId { get; set; }
    public int PreviousStateId { get; set; }
    public float TransitionProgress { get; set; }
    public int TransitionTargetStateId { get; set; }
    public float TransitionDuration { get; set; }
    public float StateEnterTime { get; set; }

    public AnimationStateMachineComponent(int initialStateId)
    {
        CurrentStateId = initialStateId;
        PreviousStateId = -1;
        TransitionProgress = 1.0f;
        TransitionTargetStateId = -1;
        TransitionDuration = 0.0f;
        StateEnterTime = 0.0f;
    }
}

// --- SYSTÈMES ---

public class AnimationSystem : ISystem
{
    private EntityManager _entityManager;
    private Dictionary<Entity, Queue<AnimationEvent>> _eventQueues = new(); // Pool de queues par entité
    private Queue<Queue<AnimationEvent>> _eventQueuePool = new(); // Pool de Queue<AnimationEvent> pour éviter les allocations

    public AnimationSystem(EntityManager entityManager) { _entityManager = entityManager; }

    public void Initialize() { }

    public void Update(float deltaTime)
    {
        // Récupérer les entités animées
        // var animatedEntities = _entityManager.GetAllEntitiesWithComponent<AnimationPlaybackComponent>();

        // foreach (var entity in animatedEntities)
        // {
        //     var playback = _entityManager.GetComponent<AnimationPlaybackComponent>(entity);
        //     var stateMachine = _entityManager.GetComponent<AnimationStateMachineComponent>(entity);
        //     var blending = _entityManager.GetComponent<AnimationBlendingComponent>(entity);
        //     var skeleton = GetEntitySkeleton(entity); // Méthode à implémenter
        //     if (skeleton == null) continue;

        //     if (playback.IsPlaying)
        //     {
        //         playback.CurrentTime += deltaTime * playback.PlaybackSpeed;

        //         // Gestion des événements
        //         var clip = GetClipForState(playback.CurrentStateId); // Méthode à implémenter via Asset
        //         if (clip != null)
        //         {
        //             foreach (var evt in clip.Events)
        //             {
        //                 if (evt.TriggerTime >= playback.CurrentTime - deltaTime && evt.TriggerTime <= playback.CurrentTime)
        //                 {
        //                     // Récupérer ou créer une queue depuis le pool
        //                     if (!_eventQueues.ContainsKey(entity))
        //                     {
        //                         Queue<AnimationEvent> newQueue = _eventQueuePool.Count > 0 ? _eventQueuePool.Dequeue() : new Queue<AnimationEvent>();
        //                         _eventQueues[entity] = newQueue;
        //                     }
        //                     _eventQueues[entity].Enqueue(evt);
        //                 }
        //             }
        //         }

        //         // Gestion du bouclage
        //         if (clip != null && playback.CurrentTime >= clip.Duration)
        //         {
        //             if (playback.Loop) playback.CurrentTime %= clip.Duration;
        //             else playback.IsPlaying = false;
        //         }

        //         // Mise à jour de la machine d'états
        //         UpdateStateMachine(entity, deltaTime, ref stateMachine, ref playback);

        //         // Mise à jour du blending
        //         if (stateMachine.TransitionProgress < 1.0f)
        //         {
        //             UpdateBlending(entity, stateMachine, ref blending, skeleton);
        //         }
        //         else
        //         {
        //             var currentClip = GetClipForState(stateMachine.CurrentStateId);
        //             currentClip?.GetInterpolatedFrame(playback.CurrentTime, blending.FinalPoseBuffer);
        //         }

        //         _entityManager.AddComponent(entity, playback);
        //         _entityManager.AddComponent(entity, stateMachine);
        //         _entityManager.AddComponent(entity, blending);
        //     }
        // }

        // // Traitement des événements
        // foreach (var kvp in _eventQueues)
        // {
        //     var entity = kvp.Key;
        //     var queue = kvp.Value;
        //     while (queue.Count > 0)
        //     {
        //         var evt = queue.Dequeue();
        //         evt.Callback(entity);
        //     }
        //     // Réinitialiser la queue et la remettre dans le pool
        //     queue.Clear();
        //     _eventQueuePool.Enqueue(queue);
        // }
        // _eventQueues.Clear(); // Réinitialiser le dictionnaire des queues actives
    }

    // private void UpdateStateMachine(Entity entity, float deltaTime, ref AnimationStateMachineComponent stateMachine, ref AnimationPlaybackComponent playback)
    // {
    //     if (stateMachine.TransitionProgress < 1.0f)
    //     {
    //         stateMachine.TransitionProgress += deltaTime / stateMachine.TransitionDuration;
    //         if (stateMachine.TransitionProgress >= 1.0f)
    //         {
    //             stateMachine.PreviousStateId = stateMachine.CurrentStateId;
    //             stateMachine.CurrentStateId = stateMachine.TransitionTargetStateId;
    //             stateMachine.TransitionTargetStateId = -1;
    //             stateMachine.TransitionProgress = 0.0f;
    //         }
    //     }
    //     else
    //     {
    //         var currentState = GetStateForId(stateMachine.CurrentStateId); // Via Asset
    //         if (currentState != null)
    //         {
    //             foreach (var transition in currentState.Transitions)
    //             {
    //                 if (transition.Condition(entity))
    //                 {
    //                     stateMachine.PreviousStateId = stateMachine.CurrentStateId;
    //                     stateMachine.TransitionTargetStateId = transition.DestinationStateId;
    //                     stateMachine.TransitionDuration = transition.TransitionDuration; // Mémoriser la durée
    //                     stateMachine.TransitionProgress = 0.0f;
    //                     break;
    //                 }
    //             }
    //         }
    //     }
    //     playback.CurrentStateId = stateMachine.CurrentStateId;
    // }

    // private void UpdateBlending(Entity entity, AnimationStateMachineComponent stateMachine, ref AnimationBlendingComponent blending, Skeleton skeleton)
    // {
    //     var prevClip = GetClipForState(stateMachine.PreviousStateId);
    //     var targetClip = GetClipForState(stateMachine.TransitionTargetStateId);

    //     if (prevClip != null && targetClip != null && prevClip.Skeleton == targetClip.Skeleton)
    //     {
    //         var playback = _entityManager.GetComponent<AnimationPlaybackComponent>(entity);
    //         prevClip.GetInterpolatedFrame(playback.CurrentTime, blending.PreviousPoseBuffer);
    //         targetClip.GetInterpolatedFrame(playback.CurrentTime, blending.TargetPoseBuffer);

    //         float t = stateMachine.TransitionProgress;
    //         for (int i = 0; i < skeleton.BoneCount; i++)
    //         {
    //             blending.FinalPoseBuffer[i] = new BoneTransform(
    //                 blending.PreviousPoseBuffer[i].Position + (blending.TargetPoseBuffer[i].Position - blending.PreviousPoseBuffer[i].Position) * t,
    //                 MathUtils.AngleHelper.LerpAngle(blending.PreviousPoseBuffer[i].Rotation, blending.TargetPoseBuffer[i].Rotation, t)
    //             );
    //         }
    //     }
    // }

    public void Shutdown() { }
}