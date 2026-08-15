// /Game/Modes/SnakeFusionMode.cs
using Snake2000.Gameplay;
using Snake2000.Entities;

namespace Snake2000.Modes
{
    public class SnakeFusionMode : IGameMode
    {
        public string Name => "Snake Fusion";
        public GameMode Type => GameMode.SnakeFusion;

        public void Initialize() { }
        public void Start() { }
        public void Update() { }
        public void End() { }
    }
}