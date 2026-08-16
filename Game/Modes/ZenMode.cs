// /Game/Modes/ZenMode.cs
using Snake2000.Gameplay;
using Snake2000.Entities;

namespace Snake2000.Modes
{
    public class ZenMode : IGameMode
    {
        public string Name => "Zen";
        public GameMode Type => GameMode.Zen;

        public void Initialize() { }
        public void Start() { }
        public void Update() { }
        public void End() { }

        // Contrat porte par IGameMode et appele par SnakeGame : le mode
        // modifie l'etat partage. Vide pour l'instant, comme l'etait Update().
        public void ApplyRules(ref SnakeGameState state) { }
    }
}