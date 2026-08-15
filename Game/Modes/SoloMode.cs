// /Game/Modes/SoloMode.cs
using Snake2000.Gameplay;

namespace Snake2000.Modes
{
    public class SoloMode : IGameMode
    {
        public string Name => "Solo";
        public GameMode Type => GameMode.Solo;

        public void Initialize() { }
        public void Start() { }
        public void ApplyRules(ref SnakeGameState gameState) { } // Aucune règle spéciale
        public void End() { }
    }
}