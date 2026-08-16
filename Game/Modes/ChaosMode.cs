// /Game/Modes/ChaosMode.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using Snake2000.Core;
using Snake2000.Gameplay;

namespace Snake2000.Modes
{
    public class ChaosMode : IGameMode
    {
        public string Name => "Chaos";
        public GameMode Type => GameMode.Chaos;

        private readonly Random _rand = new();
        private readonly List<Action<SnakeGameState>> _chaosEffects = new();
        private float _chaosTimer;
        private int _chaosLevel;
        private readonly IEventBus _eventBus;

        public ChaosMode(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _chaosEffects.Add(ActivateGravityFlip);
            _chaosEffects.Add(ActivateMirrorWorld);
            _chaosEffects.Add(ActivateTemporalStutter);
            _chaosEffects.Add(ActivateRealityDistortion);
            _chaosEffects.Add(ActivateParticleStorm);
        }

        public void Initialize()
        {
            _chaosLevel = 1;
            _chaosTimer = 0f;
        }

        public void Start() { }

        public void ApplyRules(ref SnakeGameState state)
        {
            _chaosTimer += 16f; // Delta time approximatif
            if (_chaosTimer >= 5000f) // Un effet toutes les 5 secondes
            {
                _chaosTimer = 0f;
                _chaosLevel = Math.Min(5, _chaosLevel + 1);
                var effect = _chaosEffects[_rand.Next(_chaosEffects.Count)];
                effect?.Invoke(state);
            }
        }

        private void ActivateGravityFlip(SnakeGameState state)
        {
            state.GravityEnabled = !state.GravityEnabled; // Inverse la gravité
            _eventBus.Publish(new ChaosEffectActivatedEvent { EffectName = "Gravity Flip" });
        }

        private void ActivateMirrorWorld(SnakeGameState state)
        {
            state.MirrorWorld = !state.MirrorWorld; // Active le monde miroir
            _eventBus.Publish(new ChaosEffectActivatedEvent { EffectName = "Mirror World" });
        }

        private void ActivateTemporalStutter(SnakeGameState state)
        {
            // Simule un hitstop via le système dédié (SnakeGame devra le gérer)
            _eventBus.Publish(new HitStopTriggeredEvent { DurationMs = 100, Type = HitStopType.Chaos });
            _eventBus.Publish(new ChaosEffectActivatedEvent { EffectName = "Temporal Stutter" });
        }

        private void ActivateRealityDistortion(SnakeGameState state)
        {
            state.SpeedMultiplier *= 0.8f; // Ralentit le jeu
            _eventBus.Publish(new ChaosEffectActivatedEvent { EffectName = "Reality Distortion" });
        }

        private void ActivateParticleStorm(SnakeGameState state)
        {
            for (int i = 0; i < 20; i++)
            {
                _eventBus.Publish(new ParticleSpawnEvent
                {
                    Position = new Point(_rand.Next(0, 60), _rand.Next(0, 40)),
                    Color = Color.FromArgb(_rand.Next(100, 255), _rand.Next(255), _rand.Next(255), _rand.Next(255)),
                    Lifetime = _rand.Next(30, 90),
                    Type = ParticleType.Spark
                });
            }
            _eventBus.Publish(new ChaosEffectActivatedEvent { EffectName = "Particle Storm" });
        }

        public void End() { }
    }

    // Structure pour représenter l'état du jeu modifiable par les modes
    public struct SnakeGameState
    {
        public float SpeedMultiplier;
        public bool GravityEnabled;
        public bool MirrorWorld;
        public int Lives;
        // Ajouter d'autres champs modifiables par les modes
    }
}