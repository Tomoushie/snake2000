// /Game/UI/MenuMain.cs
using System.Drawing;
using System.Windows.Forms;
using Snake2000.Core;
using Snake2000.Gameplay;

namespace Snake2000.UI
{
    public class MenuMain
    {
        private readonly Font _titleFont;
        private readonly Font _itemFont;
        private readonly Brush _titleBrush;
        private readonly Brush _itemBrush;
        private readonly Brush _selectedBrush;
        private readonly Pen _borderPen;
        private readonly int _selectedMode;

        public MenuMain(Font titleFont, Font itemFont, Brush titleBrush, Brush itemBrush, Brush selectedBrush, Pen borderPen, int selectedMode)
        {
            _titleFont = titleFont;
            _itemFont = itemFont;
            _titleBrush = titleBrush;
            _itemBrush = itemBrush;
            _selectedBrush = selectedBrush;
            _borderPen = borderPen;
            _selectedMode = selectedMode;
        }

        public void Draw(Graphics g, string playerName, int bestScore, string[] modeNames)
        {
            g.FillRectangle(Brushes.Black, 0, 0, g.VisibleClipBounds.Width, g.VisibleClipBounds.Height);

            // Titre
            var titleSize = g.MeasureString("SNAKE 2000", _titleFont);
            g.DrawString("SNAKE 2000", _titleFont, _titleBrush, (g.VisibleClipBounds.Width - titleSize.Width) / 2, 40);

            // Joueur & Best
            g.DrawString($"PLAYER: {playerName}", _itemFont, _itemBrush, 100, 120);
            g.DrawString($"BEST: {bestScore:D6}", _itemFont, _itemBrush, 100, 160);

            // Modes
            for (int i = 0; i < modeNames.Length; i++)
            {
                var brush = i == _selectedMode ? _selectedBrush : _itemBrush;
                g.DrawString(modeNames[i], _itemFont, brush, 100, 200 + i * 40);
            }

            // Instructions
            g.DrawString("SPACE: PLAY | M: MENU | N: NAME | C: STYLE", _itemFont, Brushes.Gray, 100, g.VisibleClipBounds.Height - 40);
        }
    }
}