// /Game/Entities/BossEntity.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using Snake2000.Core;

namespace Snake2000.Entities
{
    public abstract class BossEntity
    {
        public Point Position { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; protected set; }
        public Color Color { get; set; }
        public BossType Type { get; protected set; }
        public int Phase { get; protected set; } = 1;
        public List<Point> Body { get; set; } = new List<Point>();
        public bool IsAlive => Health > 0;

        protected BossEntity(Point position, int maxHealth, Color color, BossType type)
        {
            Position = position;
            MaxHealth = maxHealth;
            Health = maxHealth;
            Color = color;
            Type = type;
            Body.Add(position);
        }

        public abstract void Update();
        public abstract void Render(Graphics g);

        public virtual void TakeDamage(int damage) => Health = Math.Max(0, Health - damage);

        protected virtual void AdvancePhase()
        {
            Phase++;
            if (Phase > 3) Phase = 1;
        }
    }

    public enum BossType
    {
        Wanderer,
        Chaser,
        Shooter,
        Summoner,
        Hydra,
        Inferno,
        Glacier
    }
}