// /Game/Modes/IGameMode.cs
using Snake2000.Gameplay;

namespace Snake2000.Modes
{
    public interface IGameMode
    {
        string Name { get; }
        GameMode Type { get; }
        void Initialize();
        void Start();
        void ApplyRules(ref SnakeGameState gameState); // Passe l'état du jeu par référence pour le modifier
        void End();
    }
}