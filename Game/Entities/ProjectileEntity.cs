// /Game/Entities/ProjectileEntity.cs
using System.Drawing;

namespace Snake2000.Entities
{
    public class ProjectileEntity
    {
        public Point Position { get; set; }
        public Direction Direction { get; set; }
        public Color Color { get; set; }
        public int Damage { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public int Lifetime { get; set; } = 30; // Ticks avant disparition

        public ProjectileEntity(Point position, Direction dir, Color color)
        {
            Position = position;
            Direction = dir;
            Color = color;
        }

        public void Update()
        {
            switch (Direction)
            {
                case Direction.Up: Position = new Point(Position.X, Position.Y - 1); break;
                case Direction.Down: Position = new Point(Position.X, Position.Y + 1); break;
                case Direction.Left: Position = new Point(Position.X - 1, Position.Y); break;
                case Direction.Right: Position = new Point(Position.X + 1, Position.Y); break;
            }
            Lifetime--;
        }
    }
}