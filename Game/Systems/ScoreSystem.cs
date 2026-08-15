// /Game/Systems/ScoreSystem.cs
using Snake2000.Core;

namespace Snake2000.Systems
{
    public class ScoreSystem
    {
        private int _score;
        private int _highScore;
        private int _lives;

        public int Score => _score;
        public int HighScore => _highScore;
        public int Lives => _lives;

        public ScoreSystem(int initialLives)
        {
            _score = 0;
            _highScore = Properties.Settings.Default.BestScore; // Charger le best score
            _lives = initialLives;
        }

        public void UpdateScore(int points)
        {
            _score += points;
            if (_score > GameConfig.MaxScore) _score = GameConfig.MaxScore; // Limite de score
            if (_score > _highScore) _highScore = _score;
        }

        public void LoseLife()
        {
            _lives--;
        }

        public bool IsGameOver()
        {
            return _lives <= 0;
        }

        public void ResetScore()
        {
            _score = 0;
        }

        public void ResetLives(int initialLives)
        {
            _lives = initialLives;
        }

        public void SetHighScore(int newHighScore)
        {
            _highScore = newHighScore;
        }
    }
}