// Les sept champs de ce fichier etaient declares EN COMMENTAIRE dans
// AnimationEngineStub.cs, lignes 256-264, sous la mention « Exemples de
// declarations pour les champs qui seront definis dans les fichiers separes ».
// Des exemples, avec des `new X(...)` a points de suspension — jamais ecrits
// nulle part, alors que le code qui les utilise, lui, est bien la : 21 CS0103.
//
// Les signatures des quatre types d'appui sont relevees sur ces sites d'usage :
// _memoryPool.Acquire() rend un byte[], GetUsage() est lu par `.current`,
// _frameTimes est pilote par Enqueue/Dequeue/Count, _frameBudgetMs compare a
// un deltaTime.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Engine.Animation
{
    public class AnimationMemoryPool
    {
        public byte[] Acquire() => new byte[1024];
        public void Release(byte[] buffer) { }
    }

    public class AnimationMemoryUsageTracker
    {
        public void ReportAllocation(string nom, int octets) { }
        public void ReportDeallocation(string nom, int octets) { }
        public (long current, long peak) GetUsage() => (0L, 0L);
    }

    public class ThreadMonitor
    {
        public void UpdateActivity(Thread thread) { }
    }

    public class OverloadDetector
    {
        public bool IsOverloaded() => false;
    }

    public partial class AnimationEngineStub
    {
        private readonly AnimationMemoryPool _memoryPool = new AnimationMemoryPool();
        private readonly AnimationMemoryUsageTracker _memoryUsageTracker = new AnimationMemoryUsageTracker();
        private readonly ThreadMonitor _threadMonitor = new ThreadMonitor();
        private readonly OverloadDetector _overloadDetector = new OverloadDetector();
        private readonly Queue<float> _frameTimes = new Queue<float>();
        private readonly object _perfLock = new object();
        private float _frameBudgetMs = 16f;
    }
}