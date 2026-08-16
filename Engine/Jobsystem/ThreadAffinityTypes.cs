using System;
using System.Collections.Generic;

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
    }

    public class AffinityPreset
    {
    }

    public class CategoryAffinityProfile
    {
    }

    public class CriticalThreadIsolationData
    {
    }

    public interface IAffinityPolicy
    {
    }

    public interface INativeAffinityProvider
    {
    }

    public interface IThreadAffinityExtension
    {
    }

    public class JobAffinityHistory
    {
    }

    public class LoadBalancingHistory
    {
    }

    public class NUMALocalityHistory
    {
    }

    public class ReservedThreadInfo
    {
    }

    public class ThreadAffinityHistory
    {
    }

    public class ThreadLoadDistribution
    {
    }
}