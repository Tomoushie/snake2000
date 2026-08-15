// /Game/Entities/HazardEntity.cs
using System.Drawing;

namespace Snake2000.Entities
{
    public class HazardEntity
    {
        public Point Position { get; set; }
        public HazardType Type { get; set; }
        public int Damage { get; set; } = 1;
        public Color Color { get; set; }
        public bool IsActive { get; set; } = true;

        public HazardEntity(Point position, HazardType type)
        {
            Position = position;
            Type = type;
            Color = type switch
            {
                HazardType.Spike => Color.DarkRed,
                HazardType.Fire => Color.Orange,
                HazardType.Ice => Color.LightBlue,
                HazardType.Poison => Color.Green,
                _ => Color.Gray
            };
        }
    }

    public enum HazardType
    {
        Spike,   // Tue instantanément
        Fire,    // Inflige des dégâts
        Ice,     // Ralentit
        Poison   // Affaiblit progressivement
    }
}