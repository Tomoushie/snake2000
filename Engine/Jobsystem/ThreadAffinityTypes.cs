using System;
using System.Collections.Generic;
using Engine.Profiling;

namespace Engine.Core
{
    public enum ThreadAffinityManagerState
    {
        Uninitialized,
        Initializing,
        Ready,
        Running,
        Rebalancing,
        ShuttingDown,
        Shutdown,
        Error
    }

    /// <summary>
    /// Contrat du gestionnaire d'affinite, releve sur la declaration que
    /// Game/Gameplay/Movement/System/MovementAnimationBridgeSystem.cs portait :
    /// le moteur ne peut pas referencer le jeu, la declaration remonte donc ici.
    /// ThreadAffinityManager doit encore implementer ces deux membres.
    /// </summary>
    public interface IThreadAffinityManager
    {
        void AssignSystemToThread(Snake2000.Engine.Core.ISystem system, int threadIndex);
        System.Threading.Tasks.Task RunOnThread(int threadIndex, System.Action action);
    }

    public class AffinityHints
    {
        public int PreferredThreadIndex { get; set; }
        public bool PinningRequired { get; set; }
    }

    public class AffinityLifecycleData
    {
        public DateTime LastRebalanceTime { get; set; }
    }

    // NE PAS faire heriter de List<int> : le brief ecrivait « CategoryAffinityProfile :
    // List<int> LastAssignedThreads » pour dire « porte ce membre », et le deux-points
    // veut dire « herite de » en C#. Le modele a suivi la lettre.
    public class CategoryAffinityProfile
    {
        public List<int> LastAssignedThreads { get; set; }
    }

    public interface IAffinityPolicy
    {
        void Initialize();
    }

    public interface INativeAffinityProvider
    {
        CPUTopology DetectCPUTopology();
        long GetThreadAffinityMask(int threadIndex);
        int GetThreadCurrentCore(int threadIndex);
        void SetThreadCoreAffinity(int threadIndex, int coreIndex);
    }

    public interface IThreadAffinityExtension
    {
        void Update(float deltaTime);
        void Shutdown();
    }

    public class JobAffinityHistory
    {
        public AffinityHints LastHints { get; set; }
        public int LastPinnedThread { get; set; }
    }

    public enum ThreadReservationReason
    {
        Render,
        Audio,
        MainThread
    }

    public class ReservedThreadInfo
    {
        public int ThreadIndex { get; set; }
        public ThreadReservationReason Reason { get; set; }
    }

    public class ThreadAffinityHistory
    {
        public int LastAssignedCore { get; set; }
    }

    public class ThreadLoadDistribution
    {
        public float AverageLoad { get; set; }
        public float MaxLoad { get; set; }
        public float MinLoad { get; set; }
        public Dictionary<int, float> ThreadLoads { get; set; } = new Dictionary<int, float>();
        public Dictionary<JobCategory, float> CategoryLoads { get; set; } = new Dictionary<JobCategory, float>();

        public void UpdateFromJobSystem(IJobSystem jobSystem)
        {
        }
    }

    // Aucun membre ne leur est reclame par un site d appel : elles restent des reperes.
    public class AffinityPreset
    {
    }

    public class CriticalThreadIsolationData
    {
    }

    public class LoadBalancingHistory
    {
    }

    public class NUMALocalityHistory
    {
    }
}