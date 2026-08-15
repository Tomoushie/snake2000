// /Game/Entities/Snake.cs
// Alias pour SnakeEntity, pour compatibilité avec SnakeGame.cs existant
using System.Drawing;

namespace Snake2000.Entities
{
    public class Snake : SnakeEntity
    {
        public Snake(Point startPosition, Color color) : base(startPosition, color) {}
    }
}