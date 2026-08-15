// /Game/Systems/ParticleSystem.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Snake2000.Core;

namespace Snake2000.Systems
{
    public class ParticleSystem
    {
        private readonly List<Particle> _particles = new();
        private readonly Random _rand = new();
        private readonly IEventBus _eventBus;

        public ParticleSystem(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<ParticleSpawnEvent>(e => Spawn(e.Position, e.Color, e.Lifetime, e.Type));
        }

        public void Spawn(Point position, Color color, int lifetime, ParticleType type)
        {
            _particles.Add(new Particle
            {
                Position = position,
                Color = color,
                Lifetime = lifetime,
                Alpha = 255,
                FadeRate = 255f / lifetime, // Calcul du taux de fade
                Velocity = new Point(
                    _rand.Next(-2, 3),
                    _rand.Next(-2, 3)
                ),
                Type = type
            });
        }

        public void Update(float deltaTime)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Lifetime -= (int)deltaTime;
                if (p.Lifetime <= 0)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                p.Position = new Point(
                    p.Position.X + (int)(p.Velocity.X * deltaTime),
                    p.Position.Y + (int)(p.Velocity.Y * deltaTime)
                );

                p.Alpha = Math.Max(0, p.Alpha - p.FadeRate * deltaTime);
            }
        }

        public void Render(Graphics g)
        {
            foreach (var p in _particles)
            {
                using var brush = new SolidBrush(Color.FromArgb((int)p.Alpha, p.Color));
                g.FillEllipse(brush, p.Position.X, p.Position.Y, 4, 4);
            }
        }

        public int GetParticleCount() => _particles.Count;

        public struct Particle
        {
            public Point Position;
            public Color Color;
            public int Lifetime;
            public float Alpha;
            public float FadeRate;
            public Point Velocity;
            public ParticleType Type;
        }
    }
}