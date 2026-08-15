// /Game/UI/DebugOverlay.cs
using System;
using System.Drawing;
using Snake2000.Gameplay;
using Snake2000.Systems;
using Snake2000.Core;

namespace Snake2000.UI
{
    public class DebugOverlay
    {
        private readonly Font _font;
        private readonly Brush _brush;
        private readonly Pen _pen;
        private readonly HitStopSystem _hitStop;
        private readonly CameraShakeSystem _shake;
        private readonly ParticleSystem _particle;
        private readonly AchievementSystem _achievement;
        private int _frameCount;
        private float _lastFpsUpdate;
        private float _fps;

        public DebugOverlay(
            HitStopSystem hitStop,
            CameraShakeSystem shake,
            ParticleSystem particle,
            AchievementSystem achievement)
        {
            _font = new Font("Consolas", 10);
            _brush = Brushes.LimeGreen;
            _pen = Pens.LimeGreen;
            _hitStop = hitStop;
            _shake = shake;
            _particle = particle;
            _achievement = achievement;
        }

        public void Update(float deltaTime)
        {
            _frameCount++;
            _lastFpsUpdate += deltaTime;
            if (_lastFpsUpdate >= 1000f) // Toutes les 1000ms
            {
                _fps = _frameCount;
                _frameCount = 0;
                _lastFpsUpdate = 0f;
            }
        }

        public void Draw(Graphics g, IGame game)
        {
            const int x = 10, y0 = 10;
            int y = y0;

            g.DrawString($"FPS: {(int)_fps}", _font, _brush, x, y); y += 16;
            g.DrawString($"STATE: {game.State}", _font, _brush, x, y); y += 16;
            g.DrawString($"MODE: {game.Mode}", _font, _brush, x, y); y += 16;
            g.DrawString($"SCORE: {game.Score}", _font, _brush, x, y); y += 16;
            g.DrawString($"LIVES: {game.Lives}", _font, _brush, x, y); y += 16;
            g.DrawString($"HITSTOP: {_hitStop.IsActive}", _font, _brush, x, y); y += 16;
            g.DrawString($"SHAKE: ({_shake.GetOffset().X}, {_shake.GetOffset().Y})", _font, _brush, x, y); y += 16;
            g.DrawString($"PARTICLES: {_particle.GetParticleCount()}", _font, _brush, x, y); y += 16;

            // Afficher les achievements récents (ex: les 3 derniers)
            int count = 0;
            foreach (var a in _achievement.GetUnlocked())
            {
                if (count >= 3) break;
                g.DrawString($"ACH: {a}", _font, Brushes.Yellow, x, y); y += 16;
                count++;
            }
        }
    }
}