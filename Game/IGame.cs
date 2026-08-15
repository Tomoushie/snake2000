// /Game/IGame.cs
using System.Drawing;
using System.Windows.Forms;

namespace Snake2000.Gameplay
{
    public interface IGame
    {
        // Propriétés de base
        int Score { get; }
        int HighScore { get; }
        int Lives { get; }
        bool Alive { get; }
        bool GameRunning { get; }
        string BannerText { get; }
        Color SnakeColor { get; set; }
        GameState State { get; }
        GameMode Mode { get; }

        // Méthodes de cycle de vie
        void Initialize();
        void StartGame(GameMode mode);
        void PauseGame();
        void ResumeGame();
        void StopGame();
        void Update(float deltaTime);
        void Render(Graphics g);

        // Méthodes d'entrée
        void HandleInput(KeyEventArgs e);

        // Méthodes de persistance
        void SaveHighScore();
    }
}