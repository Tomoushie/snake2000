// Snake2000.Properties — la classe que Visual Studio GENERE normalement a partir
// d'un fichier .resx et d'un .settings, et que ce depot n'a jamais eu.
//
// Quatre fichiers l'appellent : SoundLibrary (cinq sons), ScoreSystem, SnakeGame
// et HUD (le meilleur score). Neuf CS0103, une seule cause.
//
// Les membres sont exactement ceux qui sont appeles, et rien de plus :
//   Resources.Die, Eat, MenuSelect, PowerUp, Win — passes a `new SoundPlayer(...)`,
//   dont la surcharge utilisee prend un Stream.
//   Settings.Default.BestScore, lu et ecrit, puis Save().
//
// Les sons rendent `null` : SoundPlayer l'accepte et reste muet. Le jour ou les
// .wav existeront, c'est ICI qu'on les chargera — pas dans les quatre appelants.

using System.IO;

namespace Snake2000.Properties
{
    /// <summary>Sons du jeu. Rendent null tant qu'aucun .wav n'est embarque.</summary>
    internal static class Resources
    {
        public static Stream Die => null;
        public static Stream Eat => null;
        public static Stream MenuSelect => null;
        public static Stream PowerUp => null;
        public static Stream Win => null;
    }

    /// <summary>
    /// Reglages persistants. `Save()` ne fait rien pour l'instant : le meilleur
    /// score ne survit donc pas a la fermeture, et c'est un manque nomme plutot
    /// qu'une persistance inventee.
    /// </summary>
    internal sealed class Settings
    {
        private static Settings _instance;

        public static Settings Default => _instance ??= new Settings();

        public int BestScore { get; set; }

        public void Save() { }
    }
}
