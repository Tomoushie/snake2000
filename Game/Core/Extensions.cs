// /Game/Core/Extensions.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Snake2000.Core
{
    public static class Extensions
    {
        /// <summary>
        /// Retourne une copie profonde d'une liste de points.
        /// </summary>
        public static List<Point> DeepCopy(this List<Point> list) =>
            list?.Select(p => new Point(p.X, p.Y)).ToList() ?? new List<Point>();

        /// <summary>
        /// Vérifie si deux listes de points sont égales (ordre inclus).
        /// </summary>
        public static bool SequenceEqual(this List<Point> a, List<Point> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Formate un temps en secondes au format mm:ss.
        /// </summary>
        public static string FormatTime(TimeSpan ts)
        {
            int totalSeconds = (int)ts.TotalSeconds;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }

        /// <summary>
        /// Ajoute un élément à une liste sans doublon (utilise Equals).
        /// </summary>
        public static void AddUnique<T>(this List<T> list, T item) where T : IEquatable<T>
        {
            if (!list.Contains(item)) list.Add(item);
        }

        /// <summary>
        /// Convertit une chaîne en enum, avec fallback sur la valeur par défaut.
        /// </summary>
        public static T ParseEnum<T>(string value, T defaultValue) where T : struct, Enum
        {
            return Enum.TryParse<T>(value, true, out var result) ? result : defaultValue;
        }
    }
}