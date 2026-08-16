// /Game/Entities/FoodEntity.cs
using System.Drawing;
using Snake2000.Gameplay;

namespace Snake2000.Entities
{
    public class FoodEntity
    {
        public Point Position { get; set; }
        public int Value { get; set; } = 10;
        public SpecialKind Kind { get; set; } = SpecialKind.None;
        public Color Color { get; set; }
        public bool IsActive { get; set; } = true;

        public FoodEntity(Point position, SpecialKind kind = SpecialKind.None)
        {
            Position = position;
            Kind = kind;
            Color = kind switch
            {
                SpecialKind.Speed => Color.Yellow,
                SpecialKind.Shield => Color.Blue,
                SpecialKind.Ghost => Color.Cyan,
                SpecialKind.Multiplier => Color.Magenta,
                _ => Color.Red
            };
        }
    }
}