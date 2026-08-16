// /Game/SnakeGame.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Snake2000.Entities;
using Snake2000.Modes;
using Snake2000.Systems;
using Snake2000.Core;

namespace Snake2000.Gameplay
{
    public class SnakeGame : IGame
    {
        // --- Systèmes injectés ---
        private readonly MovementSystem _movement;
        private readonly CollisionSystem _collision;
        private readonly ScoreSystem _score;
        private readonly InputSystem _input;
        private readonly GameStateManager _stateManager;
        private readonly HitStopSystem _hitStop;
        private readonly CameraShakeSystem _cameraShake;
        private readonly ParticleSystem _particle;
        private readonly AchievementSystem _achievement;
        private readonly IEventBus _eventBus;
        private readonly LoreSystem _lore;

        // --- Mode de jeu actuel ---
        private IGameMode _currentMode;

        // --- États ---
        public GameState State { get; private set; } = GameState.Ready;
        public GameMode Mode { get; private set; } = GameMode.Solo;
        public string BannerText { get; private set; } = "";
        public int BannerTicksLeft { get; private set; } = 0;

        // --- Entités ---
        public SnakeEntity Snake { get; private set; }
        public FoodEntity Food { get; private set; }
        public List<HazardEntity> Hazards { get; private set; } = new();
        public List<ProjectileEntity> Projectiles { get; private set; } = new();
        public BossEntity Boss { get; private set; }

        // --- Autres ---
        public Color SnakeColor { get; set; } = GameConfig.DefaultSnakeColor;
        public int Score => _score.Score;
        public int HighScore => _score.HighScore;
        public int Lives => _score.Lives;
        public bool Alive => _score.Lives > 0;
        public bool GameRunning { get; private set; } = false;
        private float _gameTimerMs = 0f;

        public SnakeGame(
            IEventBus eventBus,
            MovementSystem movement,
            CollisionSystem collision,
            ScoreSystem score,
            InputSystem input,
            GameStateManager stateManager,
            HitStopSystem hitStop,
            CameraShakeSystem cameraShake,
            ParticleSystem particle,
            AchievementSystem achievement,
            LoreSystem lore)
        {
            _eventBus = eventBus;
            _movement = movement;
            _collision = collision;
            _score = score;
            _input = input;
            _stateManager = stateManager;
            _hitStop = hitStop;
            _cameraShake = cameraShake;
            _particle = particle;
            _achievement = achievement;
            _lore = lore;

            InitializeGame();
        }

        private void InitializeGame()
        {
            Snake = new SnakeEntity(new Point(30, 20), SnakeColor);
            GenerateFood();
            Hazards.Clear();
            Projectiles.Clear();
            Boss = null;
            _score.ResetScore();
            _score.ResetLives(GameConfig.InitialLives);
            BannerText = "";
            BannerTicksLeft = 0;
        }

        public void Initialize() { }

        public void StartGame(GameMode mode)
        {
            Mode = mode;
            State = GameState.Playing;
            GameRunning = true;
            InitializeGame();

            // Choisir et initialiser le mode
            _currentMode = mode switch
            {
                GameMode.Chaos => new ChaosMode(_eventBus),
                _ => new SoloMode() // Mode par défaut
            };
            _currentMode.Initialize();
            _currentMode.Start();

            _stateManager.TransitionTo(GameState.Playing);

            // Déclencher une narration introductive
            _lore.DisplayIntro(mode);
        }

        public void PauseGame() => GameRunning = false;
        public void ResumeGame() => GameRunning = true;
        public void StopGame() => GameRunning = false;

        public void Update(float deltaTime)
        {
            if (!GameRunning || !Alive) return;

            _gameTimerMs += deltaTime;

            // Appliquer les règles du mode actuel
            if (_currentMode != null)
            {
                var gameState = new SnakeGameState
                {
                    SpeedMultiplier = 1.0f,
                    GravityEnabled = false,
                    MirrorWorld = false,
                    Lives = Lives
                };
                _currentMode.ApplyRules(ref gameState);
                // Ici, vous pouvez appliquer les changements de gameState à l'état du jeu
                // Ex: if (gameState.GravityEnabled) { ... }
            }

            // Si le HitStop est actif, ne pas mettre à jour le jeu
            if (_hitStop.IsActive) return;

            _input.ApplyPendingDirection();
            Snake.ChangeDirection(_input.PendingDirection);

            var newHead = _movement.MoveHead(Snake.Head, Snake.PendingDirection);
            bool ateFood = _collision.CheckAppleCollision(newHead, Food.Position);

            if (ateFood)
            {
                _score.UpdateScore(Food.Value);
                _hitStop.Trigger(50f, HitStopType.Eat);
                _cameraShake.Trigger(0.5f, 0.2f, ShakeAxis.Both);
                _particle.Spawn(new Point(newHead.X, newHead.Y), Color.Yellow, 30, ParticleType.Impact);
                _achievement.CheckCondition("score_reached", _score.Score);
                GenerateFood();
            }

            _movement.MoveSnakeBody(Snake.Body, newHead, ateFood);

            // Collisions
            if (_collision.CheckBorderCollision(Snake.Head) ||
                _collision.CheckObstacleCollision(Snake.Head, Hazards.Select(h => h.Position).ToList()) ||
                _collision.CheckSelfCollision(Snake.Head, Snake.Body.Skip(1).ToList()))
            {
                _score.LoseLife();
                _hitStop.Trigger(80f, HitStopType.Kill);
                _cameraShake.Trigger(1.0f, 0.4f, ShakeAxis.Both);
                _particle.Spawn(new Point(Snake.Head.X, Snake.Head.Y), Color.Red, 60, ParticleType.Impact);

                if (_score.IsGameOver())
                {
                    BannerText = "GAME OVER!";
                    BannerTicksLeft = GameConfig.BannerDurationTicks;
                    GameRunning = false;
                }
                else
                {
                    BannerText = "LIFE LOST!";
                    BannerTicksLeft = GameConfig.BannerDurationTicks;
                    Snake.Reset(new Point(30, 20));
                }
            }

            // Mise à jour des systèmes
            _cameraShake.Update(deltaTime);
            _particle.Update(deltaTime);
            _achievement.CheckCondition("lives_left", Lives); // Vérifie si un achievement est lié aux vies
        }

        public void Render(Graphics g)
        {
            var offset = _cameraShake.GetOffset();
            g.TranslateTransform(offset.X, offset.Y);

            // Fond
            g.FillRectangle(Brushes.Black, 0, 0, GameConfig.ScreenWidth, GameConfig.ScreenHeight);

            // Grille
            using var gridPen = new Pen(Color.FromArgb(30, 30, 30));
            for (int x = 0; x <= GameConfig.BoardWidth; x++)
                g.DrawLine(gridPen, x * GameConfig.CellSize, GameConfig.TopBarHeight, x * GameConfig.CellSize, GameConfig.TopBarHeight + GameConfig.BoardHeight * GameConfig.CellSize);
            for (int y = 0; y <= GameConfig.BoardHeight; y++)
                g.DrawLine(gridPen, 0, y * GameConfig.CellSize + GameConfig.TopBarHeight, GameConfig.BoardWidth * GameConfig.CellSize, y * GameConfig.CellSize + GameConfig.TopBarHeight);

            // Serpent
            using var snakeBrush = new SolidBrush(SnakeColor);
            foreach (var seg in Snake.Body)
                g.FillRectangle(snakeBrush, seg.X * GameConfig.CellSize, seg.Y * GameConfig.CellSize + GameConfig.TopBarHeight, GameConfig.CellSize, GameConfig.CellSize);

            // Pomme
            using var foodBrush = new SolidBrush(Food.Color);
            g.FillRectangle(foodBrush, Food.Position.X * GameConfig.CellSize, Food.Position.Y * GameConfig.CellSize + GameConfig.TopBarHeight, GameConfig.CellSize, GameConfig.CellSize);

            // Hazards & Projectiles
            foreach (var h in Hazards.Where(h => h.IsActive))
            {
                using var hazardBrush = new SolidBrush(h.Color);
                g.FillRectangle(hazardBrush, h.Position.X * GameConfig.CellSize, h.Position.Y * GameConfig.CellSize + GameConfig.TopBarHeight, GameConfig.CellSize, GameConfig.CellSize);
            }
            foreach (var p in Projectiles.Where(p => p.IsActive))
            {
                using var projBrush = new SolidBrush(p.Color);
                g.FillRectangle(projBrush, p.Position.X * GameConfig.CellSize, p.Position.Y * GameConfig.CellSize + GameConfig.TopBarHeight, GameConfig.CellSize, GameConfig.CellSize);
            }

            // Boss
            Boss?.Render(g);

            // HUD
            using var hudFont = new Font("Arial", 12, FontStyle.Bold);
            using var hudBrush = Brushes.White;
            g.DrawString($"SCORE {Score:D6}", hudFont, hudBrush, 6, 8);
            g.DrawString($"LIVES {Lives:D1}", hudFont, hudBrush, 120, 8);
            g.DrawString($"LVL 1", hudFont, hudBrush, 240, 8);
            g.DrawString($"TIME {MathUtils.FormatTime(TimeSpan.FromMilliseconds(_gameTimerMs))}", hudFont, hudBrush, 360, 8);
            g.DrawString($"BEST {HighScore:D6}", hudFont, hudBrush, GameConfig.ScreenWidth - 100, 8);

            // Banner
            if (!string.IsNullOrEmpty(BannerText) && BannerTicksLeft > 0)
            {
                using var bannerFont = new Font("Arial", 16, FontStyle.Bold);
                var size = g.MeasureString(BannerText, bannerFont);
                float x = (GameConfig.ScreenWidth - size.Width) / 2;
                float y = (GameConfig.TopBarHeight - size.Height) / 2 + 5;
                g.DrawString(BannerText, bannerFont, Brushes.Yellow, x, y);
            }

            g.TranslateTransform(-offset.X, -offset.Y);
            _particle.Render(g);
        }

        public void HandleInput(KeyEventArgs e)
        {
            _input.ProcessKeyDown(e.KeyCode);
        }

        public void SaveHighScore()
        {
            Properties.Settings.Default.BestScore = _score.HighScore;
            Properties.Settings.Default.Save();
        }

        private void GenerateFood()
        {
            Random rand = new Random();
            Point pos;
            do
            {
                pos = new Point(rand.Next(0, GameConfig.BoardWidth), rand.Next(0, GameConfig.BoardHeight));
            } while (Snake.Body.Contains(pos) || Hazards.Any(h => h.Position == pos));
            Food = new FoodEntity(pos);
        }
    }
}