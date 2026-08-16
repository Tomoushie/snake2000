// /Game/GameConfig.cs
using System.Drawing;

namespace Snake2000.Gameplay
{
    public static class GameConfig
    {
        // --- CONSTANTES DE JEU ---
        public const int BoardWidth = 60;
        public const int BoardHeight = 40;
        public const int CellSize = 15;
        public const int TopBarHeight = 40;
        public const int TickInterval = 100; // ms
        public const int BannerDurationTicks = 120;
        public const int MaxScore = 999999;
        public const int DefaultStartingLength = 5;
        public const int MovingWallMoveInterval = 30; // Ticks
        public const int BalanceBonusInterval = 60; // Ticks
        public const int MaxSnakeLength = BoardWidth * BoardHeight; // Limite théorique
        public const int MinBodyLengthToSplit = 10; // Ajuster selon la difficulté souhaitée

        // --- DIMENSIONS ---
        public const int ScreenWidth = BoardWidth * CellSize;
        public const int ScreenHeight = (BoardHeight * CellSize) + TopBarHeight;

        // --- COULEURS ---
        public static readonly Color DefaultSnakeColor = Color.LimeGreen;
        public static readonly Color BackgroundColor = Color.Black;
        public static readonly Color GridColor = Color.FromArgb(30, 30, 30);
        public static readonly Color AppleColor = Color.Red;
        public static readonly Color ObstacleColor = Color.Gray;

        // --- FONTS ---
        public static readonly string HudFontFamily = "Arial";
        public static readonly float HudFontSize = 12.0f;
        public static readonly float BannerFontSize = 16.0f;

        // --- AUTRES PARAMÈTRES ---
        public static int InitialLives => 3;

        // Il y avait ici une propriete `Instance` renvoyant un GameConfig, vestige
        // d'une injection par singleton abandonnee quand la classe est devenue
        // statique. Un type static ne peut etre ni instancie ni renvoye (CS0722),
        // et rien ne la referencait. L'acces se fait directement : GameConfig.X.
    }
}