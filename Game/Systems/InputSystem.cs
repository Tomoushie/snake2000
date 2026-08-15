// /Game/Systems/InputSystem.cs
using System.Windows.Forms;
using Snake2000.Entities;

namespace Snake2000.Systems
{
    public class InputSystem
    {
        private Direction _currentDirection;
        private Direction _pendingDirection;

        public Direction CurrentDirection => _currentDirection;
        public Direction PendingDirection => _pendingDirection;

        public InputSystem(Direction initialDirection)
        {
            _currentDirection = initialDirection;
            _pendingDirection = initialDirection;
        }

        public void ProcessKeyDown(Keys keyCode)
        {
            Direction newDir = _pendingDirection; // Valeur par défaut, ne rien changer
            switch (keyCode)
            {
                case Keys.Up:
                    if (_currentDirection != Direction.Down) newDir = Direction.Up;
                    break;
                case Keys.Down:
                    if (_currentDirection != Direction.Up) newDir = Direction.Down;
                    break;
                case Keys.Left:
                    if (_currentDirection != Direction.Right) newDir = Direction.Left;
                    break;
                case Keys.Right:
                    if (_currentDirection != Direction.Up) newDir = Direction.Right;
                    break;
            }
            // Appliquer la nouvelle direction demandée si elle est valide
            if (newDir != _currentDirection) // Empêche les demi-tours
            {
                _pendingDirection = newDir;
            }
        }

        // Appelé par le jeu pour appliquer la direction en attente
        public void ApplyPendingDirection()
        {
            _currentDirection = _pendingDirection;
        }
    }
}