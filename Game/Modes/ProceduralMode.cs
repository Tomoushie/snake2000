// /Game/Modes/ProceduralMode.cs
using Snake2000.Gameplay;
using Snake2000.Entities;

namespace Snake2000.Modes
{
    public class ProceduralMode : IGameMode
    {
        public string Name => "Procedural";
        public GameMode Type => GameMode.Procedural;

        public void Initialize() { }
        public void Start() { }
        public void Update() { }
        public void End() { }
    }
}