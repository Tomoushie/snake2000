// /Game/Entities/SnakeEntity.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using Snake2000.Core;

namespace Snake2000.Entities
{
    public class SnakeEntity
    {
        public List<Point> Body { get; private set; }
        public Direction Direction { get; private set; }
        public Direction PendingDirection { get; set; }
        public Color Color { get; set; }
        public bool IsAlive { get; set; } = true;
        public int Length => Body.Count;
        public Point Head => Body[0];
        public Point Tail => Body[^1];

        public SnakeEntity(Point startPosition, Color color)
        {
            Body = new List<Point> { startPosition };
            Direction = Direction.Right;
            PendingDirection = Direction;
            Color = color;
        }

        public void Move(Point newHead, bool ateFood = false)
        {
            Body.Insert(0, newHead);
            if (!ateFood)
                Body.RemoveAt(Body.Count - 1); // Retirer la queue
        }

        public void ChangeDirection(Direction newDir)
        {
            // Empêcher de tourner sur soi-même
            if (newDir == Opposite(Direction))
                return;
            PendingDirection = newDir;
        }

        private Direction Opposite(Direction dir)
        {
            return dir switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                Direction.Right => Direction.Left,
                _ => dir
            };
        }

        public void Reset(Point startPosition)
        {
            Body.Clear();
            Body.Add(startPosition);
            Direction = Direction.Right;
            PendingDirection = Direction;
            IsAlive = true;
        }

        public bool Contains(Point p) => Body.Contains(p);
    }
}