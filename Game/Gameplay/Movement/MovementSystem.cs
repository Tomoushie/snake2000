// /Game/Systems/MovementSystem.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using Snake2000.Entities;
using Snake2000.Core;
using Snake2000.Gameplay;

namespace Snake2000.Systems
{
    public class MovementSystem
    {
        private readonly IGame _game;

        public MovementSystem(IGame game)
        {
            _game = game;
        }

        public Point MoveHead(Point head, Direction dir)
        {
            Point newHead = head;
            switch (dir)
            {
                case Direction.Up: newHead.Y--; break;
                case Direction.Down: newHead.Y++; break;
                case Direction.Left: newHead.X--; break;
                case Direction.Right: newHead.X++; break;
            }
            return newHead;
        }

        public void MoveSnakeBody(List<Point> body, Point newHead, bool ateApple)
        {
            body.Insert(0, newHead);
            if (!ateApple)
            {
                body.RemoveAt(body.Count - 1); // Retirer la queue
            }
            // Si ateApple est true, le serpent a grandi, la queue n'est pas retirée
        }

        public Point ApplyGravity(Point currentHead, List<Point> obstacles, List<Point> snakeBody)
        {
            Point below = new Point(currentHead.X, currentHead.Y + 1);
            if (IsPositionEmpty(below, obstacles, snakeBody))
            {
                return below;
            }
            return currentHead; // Ne bouge pas si bloqué
        }

        private bool IsPositionEmpty(Point pos, List<Point> obstacles, List<Point> snakeBody)
        {
            return GameConfig.IsInside(pos) && !obstacles.Contains(pos) && !snakeBody.Contains(pos);
        }
    }
}