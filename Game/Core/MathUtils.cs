// /Game/Core/MathUtils.cs
using System;
using System.Drawing;

namespace Snake2000.Core
{
    public static class MathUtils
    {
        /// <summary>
        /// Calcule la distance euclidienne entre deux points (arrondie à l'entier le plus proche).
        /// </summary>
        public static int Distance(Point a, Point b) => (int)Math.Round(Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)));

        /// <summary>
        /// Calcule le carré de la distance (pour éviter les racines carrées lors des comparaisons).
        /// </summary>
        public static int DistanceSquared(Point a, Point b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

        /// <summary>
        /// Clamp une valeur entre un minimum et un maximum.
        /// </summary>
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        /// <summary>
        /// Clamp un point dans les limites du plateau (BoardWidth x BoardHeight).
        /// </summary>
        public static Point Clamp(Point p, int width, int height) =>
            new Point(Clamp(p.X, 0, width - 1), Clamp(p.Y, 0, height - 1));

        /// <summary>
        /// Wrap-around (toroidal) : si le point sort d’un côté, il réapparaît de l’autre.
        /// </summary>
        public static Point Wrap(Point p, int width, int height) =>
            new Point((p.X + width) % width, (p.Y + height) % height);
    }
}