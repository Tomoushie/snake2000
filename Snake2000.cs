// /Game/Snake2000.cs
using System;
using System.Drawing;
using System.Windows.Forms;
using Snake2000.Gameplay;
using Snake2000.UI;
using Snake2000.Audio;
using Snake2000.Systems;
using Snake2000.Core;

// Clarification explicite du Timer pour éviter l'ambiguïté
using Timer = System.Windows.Forms.Timer;

namespace Snake2000
{
    public partial class Snake2000 : Form
    {
        private readonly SnakeGame _game;
        private readonly Timer _gameTimer;
        private readonly HUD _hud;
        private readonly MenuMain _menu;
        private readonly DebugOverlay _debug;
        private readonly IEventBus _eventBus;
        private readonly HitStopSystem _hitStop;
        private readonly CameraShakeSystem _cameraShake;
        private readonly ParticleSystem _particle;
        private readonly AchievementSystem _achievement;
        private readonly LoreSystem _lore;

        public Snake2000()
        {
            InitializeComponent();
            DoubleBuffered = true;
            KeyPreview = true;

            // Initialiser EventBus en premier
            _eventBus = new EventBus();

            // Systèmes
            _hitStop = new HitStopSystem(_eventBus);
            _cameraShake = new CameraShakeSystem(_eventBus);
            _particle = new ParticleSystem(_eventBus);
            _achievement = new AchievementSystem(_eventBus);
            _lore = new LoreSystem();

            // Jeu
            _game = new SnakeGame(
                _eventBus,
                new MovementSystem(_game),
                new CollisionSystem(_game),
                new ScoreSystem(GameConfig.InitialLives),
                new InputSystem(Direction.Right),
                new GameStateManager(_game),
                _hitStop,
                _cameraShake,
                _particle,
                _achievement,
                _lore
            );

            _gameTimer = new Timer { Interval = 16 };
            _gameTimer.Tick += OnGameTick;

            // UI
            _hud = new HUD(new Font("Arial", 12, FontStyle.Bold), Brushes.White, Pens.White, GameConfig.Instance);
            _menu = new MenuMain(new Font("Arial", 24, FontStyle.Bold), new Font("Arial", 16), Brushes.LimeGreen, Brushes.White, Brushes.Yellow, Pens.White, 0);
            _debug = new DebugOverlay(_hitStop, _cameraShake, _particle, _achievement);

            Load += OnLoad;
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            _game.Initialize();
            _game.StartGame(GameMode.Solo);
            _gameTimer.Start();
        }

        private void OnGameTick(object? sender, EventArgs e)
        {
            float deltaTime = 16f; // ~60 FPS
            _game.Update(deltaTime);
            _debug.Update(deltaTime); // Met à jour le FPS
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_game.State == GameState.Playing)
            {
                _game.Render(e.Graphics);
                _hud.Draw(e.Graphics, _game.Score, _game.Lives, _game.BannerText, 1, 0); // Timer temporaire
                _debug.Draw(e.Graphics, _game); // Dessine le debug overlay
            }
            else
            {
                _menu.Draw(e.Graphics, "PLAYER", _game.HighScore, new[] { "SOLO", "CO-OP", "BOSS FIGHT" });
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            _game.HandleInput(e);
        }

        private void Snake2000_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _gameTimer.Stop();
            _game.SaveHighScore();
        }
    }
}