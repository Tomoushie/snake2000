// /Game/Systems/HitStopSystem.cs
using System;
using System.Windows.Forms;
using Snake2000.Core;

namespace Snake2000.Systems
{
    public class HitStopSystem
    {
        // Construit `new Timer { Interval = 16 }` puis pilote par `.Tick`, `.Start()`
        // et `.Stop()` : celui de Forms, contrairement aux deux du moteur.
        private readonly System.Windows.Forms.Timer _hitStopTimer;
        private bool _isHitStopping;
        private float _remainingTimeMs;
        private readonly IEventBus _eventBus;

        public HitStopSystem(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _hitStopTimer = new Timer { Interval = 16 }; // ~60 FPS
            _hitStopTimer.Tick += OnHitStopTick;
        }

        public void Trigger(float durationMs, HitStopType type)
        {
            if (_isHitStopping) return;
            _remainingTimeMs = durationMs;
            _isHitStopping = true;
            _hitStopTimer.Start();
            _eventBus.Publish(new HitStopTriggeredEvent { DurationMs = durationMs, Type = type });
        }

        private void OnHitStopTick(object? sender, EventArgs e)
        {
            if (_remainingTimeMs <= 0)
            {
                _isHitStopping = false;
                _hitStopTimer.Stop();
                return;
            }
            // Simuler un micro-freeze en bloquant le message loop pendant 1 frame
            Application.DoEvents();
            _remainingTimeMs -= 16f;
        }

        public bool IsActive => _isHitStopping;
    }
}