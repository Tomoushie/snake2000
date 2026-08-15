// /Game/Modes/PuzzleMode.cs
using Snake2000.Gameplay;
using Snake2000.Entities;

namespace Snake2000.Modes
{
    public class PuzzleMode : IGameMode
    {
        public string Name => "Puzzle";
        public GameMode Type => GameMode.Puzzle;

        public void Initialize() { }
        public void Start() { }
        public void Update() { }
        public void End() { }
    }
}