// /Game/IGameMode.cs
namespace Snake2000.Modes
{
    public interface IGameMode
    {
        string Name { get; }
        GameMode Type { get; }
        void Initialize();
        void Start();
        void Update();
        void End();
    }
}