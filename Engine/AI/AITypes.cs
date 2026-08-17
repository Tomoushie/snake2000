// Engine/AI/AITypes.cs
//
// Types reclames par AIEngine.cs et declares nulle part ailleurs.
// Genere via l'orchestrateur (Qwen3-Coder) a partir des usages releves dans
// AIEngine.cs, puis relu.
using System;
using System.Collections.Generic;
using Snake2000.Engine.Core;

namespace Snake2000.Engine.AI
{
    /// <summary>
    /// Maillage de navigation.
    ///
    /// ATTENTION : socle non implemente. IsWalkable rend toujours true et
    /// FindPath trace une ligne droite entre depart et arrivee. Aucune
    /// geometrie n'est consultee. A remplacer avant que l'IA ne s'appuie
    /// reellement sur la navigation -- en l'etat, elle traverse les murs.
    /// </summary>
    public class NavMesh
    {
        public bool IsWalkable(Vector2 position) => true;
        public List<Vector2> FindPath(Vector2 depart, Vector2 arrivee) => new List<Vector2> { depart, arrivee };
    }

    public class AIGoal
    {
        public string Name { get; }
        public Func<Entity, float> Scorer { get; }

        public AIGoal(string name, Func<Entity, float> scorer)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name cannot be null or empty", nameof(name));
            if (scorer == null) throw new ArgumentNullException(nameof(scorer));
            Name = name;
            Scorer = scorer;
        }
    }

    public class AIThreatSystem : ISystem
    {
        private readonly EntityManager _entityManager;

        public AIThreatSystem(EntityManager em)
        {
            _entityManager = em;
        }

        public void Update(float deltaTime) { }
    }

    public class AIMemorySystem : ISystem
    {
        private readonly EntityManager _entityManager;

        public AIMemorySystem(EntityManager em)
        {
            _entityManager = em;
        }

        public void Update(float deltaTime) { }
    }

    public class AIStateSystem : ISystem
    {
        private readonly EntityManager _entityManager;

        public AIStateSystem(EntityManager em)
        {
            _entityManager = em;
        }

        public void Update(float deltaTime) { }
    }

    public class AIPerformanceManager
    {
        private readonly EntityManager _entityManager;

        public AIPerformanceManager(EntityManager em)
        {
            _entityManager = em;
        }

        // Quatre membres reclames par AIEngine. ShouldUpdate recoit un Type — le
        // site d'appel ecrit `ShouldUpdate(sys.GetType())` — et repond si le
        // systeme doit tourner sur cette frame.
        public void Initialize() { }
        public void Update(float deltaTime) { }
        public void Shutdown() { }
        public bool ShouldUpdate(Type systemType) => true;
    }

    public class AIParallelJobHandle
    {
        public Action Job { get; }
        public IReadOnlyList<AIParallelJobHandle> Dependencies { get; }

        public AIParallelJobHandle(Action job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            Job = job;
            Dependencies = Array.Empty<AIParallelJobHandle>();
        }

        public AIParallelJobHandle(Action job, List<AIParallelJobHandle> dependencies)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            Job = job;
            Dependencies = dependencies ?? Array.Empty<AIParallelJobHandle>();
        }
    }
}