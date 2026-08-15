// /Game/Entities/Obstacle.cs
using System.Drawing;

namespace Snake2000.Entities
{
    public class Obstacle
    {
        public Point Position { get; set; }
        public ObstacleType Type { get; set; } = ObstacleType.Solid;

        public Obstacle(Point position)
        {
            Position = position;
        }
    }

    public enum ObstacleType
    {
        Solid,       // Mur normal
        Breakable,   // Mur cassable
        Portal,      // Téléporte
        Moving       // Se déplace
    }
}