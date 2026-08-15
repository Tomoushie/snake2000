// /Game/Core/ColorUtils.cs
using System.Drawing;

namespace Snake2000.Core
{
    public static class ColorUtils
    {
        /// <summary>
        /// Convertit une couleur en "LCD monochrome" (simule l’écran vert/ambre des Nokia).
        /// </summary>
        public static Color ToLCD(Color c)
        {
            // Moyenne pondérée pour le vert LCD (R:30%, G:59%, B:11%)
            int lum = (int)(c.R * 0.3f + c.G * 0.59f + c.B * 0.11f);
            // Échelle de gris -> vert LCD (0-255 → 0-127)
            int lcd = Math.Min(127, lum / 2);
            return Color.FromArgb(lcd, lcd, 0); // Vert ambré
        }

        /// <summary>
        /// Crée une couleur avec une opacité donnée.
        /// </summary>
        public static Color WithAlpha(Color baseColor, byte alpha) =>
            Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);

        /// <summary>
        /// Mélange deux couleurs avec un facteur de mélange (0.0 = a, 1.0 = b).
        /// </summary>
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t)
            );
        }
    }
}