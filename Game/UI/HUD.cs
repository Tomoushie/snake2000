// /Game/UI/HUD.cs
using System.Drawing;
using System.Windows.Forms;
using Snake2000.Core;
using Snake2000.Gameplay;

namespace Snake2000.UI
{
    public class HUD
    {
        private readonly Font _font;
        private readonly Brush _brush;
        private readonly Pen _pen;
        private readonly GameConfig _config;

        public HUD(Font font, Brush brush, Pen pen, GameConfig config)
        {
            _font = font;
            _brush = brush;
            _pen = pen;
            _config = config;
        }

        public void Draw(Graphics g, int score, int lives, string bannerText, int level, float timer)
        {
            // Score & Lives
            string scoreText = $"SCORE {score:D6}";
            string livesText = $"LIVES {lives:D1}";
            string levelText = $"LVL {level:D2}";
            string timerText = $"TIME {MathUtils.FormatTime(TimeSpan.FromMilliseconds(timer))}";

            g.DrawString(scoreText, _font, _brush, 6, 8);
            g.DrawString(livesText, _font, _brush, 120, 8);
            g.DrawString(levelText, _font, _brush, 240, 8);
            g.DrawString(timerText, _font, _brush, 360, 8);

            // Best Score
            g.DrawString($"BEST {Properties.Settings.Default.BestScore:D6}", _font, _brush, _config.ScreenWidth - 100, 8);

            // Banner
            if (!string.IsNullOrEmpty(bannerText))
            {
                using var bannerFont = new Font(_font.FontFamily, 16, FontStyle.Bold);
                var size = g.MeasureString(bannerText, bannerFont);
                float x = (_config.ScreenWidth - size.Width) / 2;
                float y = (_config.TopBarHeight - size.Height) / 2 + 5;
                g.DrawString(bannerText, bannerFont, Brushes.Yellow, x, y);
            }
        }
    }
}