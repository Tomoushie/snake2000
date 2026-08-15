// /Game/Entities/Food.cs
// Alias pour FoodEntity, pour compatibilité avec SnakeGame.cs existant
using System.Drawing;

namespace Snake2000.Entities
{
    public class Food : FoodEntity
    {
        public Food(Point position) : base(position) {}
    }
}