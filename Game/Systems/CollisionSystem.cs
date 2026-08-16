// /Game/Systems/CollisionSystem.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using Snake2000.Entities;
using Snake2000.Core;
using Snake2000.Gameplay;

namespace Snake2000.Systems
{
    public class CollisionSystem
    {
        private readonly IGame _game;

        public CollisionSystem(IGame game)
        {
            _game = game;
        }

        public bool IsInside(Point pos)
        {
            return pos.X >= 0 && pos.X < GameConfig.BoardWidth && pos.Y >= 0 && pos.Y < GameConfig.BoardHeight;
        }

        public bool CheckSelfCollision(Point head, List<Point> body)
        {
            // Le corps ne contient pas la tête
            for (int i = 0; i < body.Count; i++)
            {
                if (head == body[i])
                {
                    return true;
                }
            }
            return false;
        }

        public bool CheckObstacleCollision(Point head, List<Point> obstacles)
        {
            return obstacles.Contains(head);
        }

        public bool CheckBorderCollision(Point head)
        {
            return !IsInside(head);
        }

        public bool CheckAppleCollision(Point head, Point applePos)
        {
            return head == applePos;
        }

        public bool CheckSpecialCollision(Point head, Dictionary<Point, SpecialKind> specialPositions)
        {
            return specialPositions.ContainsKey(head);
        }
    }
}