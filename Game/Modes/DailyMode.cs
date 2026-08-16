// /Game/Modes/DailyMode.cs
using Snake2000.Gameplay;
using Snake2000.Entities;

namespace Snake2000.Modes
{
    public class DailyMode : IGameMode
    {
        public string Name => "Daily Challenge";
        public GameMode Type => GameMode.Daily;

        public void Initialize() { }
        public void Start() { }
        public void Update() { }
        public void End() { }

        // Contrat porte par IGameMode et appele par SnakeGame : le mode
        // modifie l'etat partage. Vide pour l'instant, comme l'etait Update().
        public void ApplyRules(ref SnakeGameState state) { }
    }
}