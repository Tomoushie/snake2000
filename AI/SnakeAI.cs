// Fragment d'IA ecrit a la main, rendu compilable le 16 aout 2026 sans qu'une
// seule ligne de son contenu ne bouge : il est simplement enveloppe.
//
// Il portait 30 membres au niveau racine, precedes de « Ajouter dans la section
// des variables membres » — un texte destine a etre colle dans Snake2000.cs. La
// classe cible y est declaree `public partial class Snake2000 : Form`, donc
// `partial` fait le travail : ce fichier devient un morceau de la meme classe,
// sans copier-coller et sans deplacer quoi que ce soit.
//
// `: Form` n'est PAS repete ici : entre partiels, la classe de base ne se
// declare qu'une fois, et l'omettre evite d'avoir besoin du type Form.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Snake2000.Gameplay;   // Direction, declare dans Game/Enums.cs

namespace Snake2000
{
    public partial class Snake2000
    {
        // Ajouter dans la section des variables membres
        // (à proximité des autres variables liées à l'IA)
        // --- PERSONNALITÉS IA ---
        public enum AIType
        {
            Neutral,        // Comportement par défaut
            Aggressive,     // Poursuit activement le joueur, cherche le contact
            Defensive,      // Évite les dangers, fuit le joueur
            Opportunist,    // Cherche les power-ups, évite les dangers
            Kamikaze,       // Fonce tête baissée sur le joueur, peu importe les dangers
            Hunter,         // Mémorise la position du joueur, le poursuit activement
            Fleeing,        // Fuit constamment le joueur
            Boss,           // IA spécifique pour les boss (patterns, attaques)
            MiniBoss,       // IA spécifique pour les mini-boss
            ZombieSnake,    // Mouvements erratiques, ralentis
            VampireSnake    // Draine la vie/vitesse du joueur à distance
        }

        private AIType currentAIType = AIType.Neutral; // Personnalité actuelle de l'IA
        // --- VARIABLES SPECIFIQUES À CERTAINES PERSONNALITÉS ---
        private Point aiLastKnownPlayerPos = Point.Empty; // Pour Hunter, Fleeing
        private const int PlayerMemoryTimeout = 30;
        private int aiPlayerMemoryCounter = 0;
        private int aiZombieMoveCounter = 0; // Pour ZombieSnake
        private const int ZombieMoveInterval = 10; // Intervalle pour les mouvements zombies
        private bool aiIsVampiring = false; // Pour VampireSnake
        private const int VampireRange = 3; // Portée d'absorption
        // --- FIN VARIABLES PERSONNALITÉS ---

        // Ajouter dans la section des méthodes

        /// <summary>
        /// Met à jour la direction de l'IA (snake2) en fonction de la position du joueur (snake), de l'environnement et de sa personnalité.
        /// </summary>
        private void UpdateAIBehavior()
        {
            // S'assurer que l'IA est en vie
            if (!alive2 || snake2.Count == 0) return;

            Point head2 = snake2[snake2.Count - 1]; // Tête du serpent IA (en supposant que la tête est le dernier élément)
            Point playerHead = snake[0]; // Tête du joueur (en supposant que la tête est le premier élément)

            // --- GESTION DE LA MÉMOIRE DU JOUEUR (pour Hunter, Fleeing) ---
            int distanceToPlayer = Math.Abs(head2.X - playerHead.X) + Math.Abs(head2.Y - playerHead.Y);
            if (distanceToPlayer <= 5) // Seuil de "vision" (ajustable)
            {
                aiLastKnownPlayerPos = playerHead; // Mettre à jour la position connue
                aiPlayerMemoryCounter = PlayerMemoryTimeout; // Réinitialiser le compteur
            }
            else if (aiPlayerMemoryCounter > 0)
            {
                aiPlayerMemoryCounter--;
                if (aiPlayerMemoryCounter <= 0)
                {
                    aiLastKnownPlayerPos = Point.Empty; // Oublier
                }
            }

            // --- SELECTION DES ACTIONS SELON LA PERSONNALITÉ ---
            switch (currentAIType)
            {
                case AIType.Aggressive:
                case AIType.Kamikaze:
                    UpdateAggressiveBehavior(head2, playerHead);
                    break;
                case AIType.Defensive:
                    UpdateDefensiveBehavior(head2, playerHead);
                    break;
                // --- CORRECTION 7.1 : Appel correct de UpdateFleeingBehavior ---
                case AIType.Fleeing:
                    UpdateFleeingBehavior(head2, playerHead);
                    break;
                // --- FIN CORRECTION 7.1 ---
                case AIType.Opportunist:
                    UpdateOpportunistBehavior(head2);
                    break;
                case AIType.Hunter:
                    UpdateHunterBehavior(head2);
                    break;
                case AIType.ZombieSnake:
                    UpdateZombieBehavior(head2);
                    break;
                case AIType.VampireSnake:
                    UpdateVampireBehavior(head2, playerHead);
                    break;
                // Boss et MiniBoss auraient leurs propres logiques spécifiques
                case AIType.Boss:
                case AIType.MiniBoss:
                    UpdateGenericBossBehavior(head2, playerHead); // Placeholder
                    break;
                case AIType.Neutral:
                default:
                    UpdateNeutralBehavior(head2, playerHead);
                    break;
            }
        }

        // --- FONCTIONS SPÉCIFIQUES POUR CHAQUE PERSONNALITÉ ---

        private void UpdateNeutralBehavior(Point head2, Point playerHead)
        {
            // Comportement de base, similaire à la version précédente
            UpdateBasicAvoidanceAndChase(head2, playerHead, false); // Pacifique
        }

        private void UpdateAggressiveBehavior(Point head2, Point playerHead)
        {
            // Poursuivre le joueur activement
            UpdateBasicAvoidanceAndChase(head2, playerHead, true); // Agressive
        }

        private void UpdateDefensiveBehavior(Point head2, Point playerHead)
        {
            // Éviter les dangers, fuir le joueur si proche
            UpdateBasicAvoidanceAndChase(head2, playerHead, false); // Pacifique, mais priorité à l'évitement
            // On peut ajouter une logique de fuite plus poussée ici
            if (Math.Abs(head2.X - playerHead.X) + Math.Abs(head2.Y - playerHead.Y) <= SafeDistanceToPlayer)
            {
                AttemptToFlee(head2, playerHead);
            }
        }

        private void UpdateOpportunistBehavior(Point head2)
        {
            // Chercher les power-ups, éviter les dangers
            // On peut réutiliser la logique de base pour les dangers
            // Et ajouter une priorisation pour les power-ups
            // Pour simplifier, on utilise la logique de base avec un focus sur les power-ups
            UpdateBasicAvoidanceAndChase(head2, Point.Empty, false); // Pas de poursuite du joueur, mais gestion des dangers
            // Ajouter logique de poursuite des power-ups ici
            Point closestPowerUp = FindClosestPowerUp(head2); // Fonction à implémenter
            if (closestPowerUp != Point.Empty)
            {
                // Calculer la direction vers le power-up
                int dx = closestPowerUp.X - head2.X;
                int dy = closestPowerUp.Y - head2.Y;
                Direction preferredDir = (Math.Abs(dx) > Math.Abs(dy)) ?
                    (dx > 0 ? Direction.Right : Direction.Left) :
                    (dy > 0 ? Direction.Down : Direction.Up);

                // Vérifier si la direction est sûre
                Point targetPos = MovePoint(head2, preferredDir);
                if (IsSafeMove(targetPos)) // Fonction à implémenter
                {
                    pendingDirection2 = preferredDir;
                    return; // Direction prioritaire trouvée
                }
            }
        }

        private void UpdateHunterBehavior(Point head2)
        {
            // Poursuivre la dernière position connue du joueur
            if (aiLastKnownPlayerPos != Point.Empty)
            {
                int dx = aiLastKnownPlayerPos.X - head2.X;
                int dy = aiLastKnownPlayerPos.Y - head2.Y;
                Direction preferredDir = (Math.Abs(dx) > Math.Abs(dy)) ?
                    (dx > 0 ? Direction.Right : Direction.Left) :
                    (dy > 0 ? Direction.Down : Direction.Up);

                Point targetPos = MovePoint(head2, preferredDir);
                if (IsSafeMove(targetPos))
                {
                    pendingDirection2 = preferredDir;
                    return; // Direction prioritaire trouvée
                }
            }
            // Sinon, comportement par défaut
            UpdateBasicAvoidanceAndChase(head2, Point.Empty, false);
        }

        private void UpdateFleeingBehavior(Point head2, Point playerHead)
        {
            // Toujours fuir le joueur
            AttemptToFlee(head2, playerHead);
        }

        private void UpdateZombieBehavior(Point head2)
        {
            // Mouvements erratiques, ralentis
            aiZombieMoveCounter--;
            if (aiZombieMoveCounter > 0) return; // Ne pas bouger ce tick

            aiZombieMoveCounter = ZombieMoveInterval;

            // Choisir une direction aléatoire parmi les possibles
            List<Direction> possibleDirs = new List<Direction> { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
            possibleDirs.RemoveAll(dir => !IsSafeMove(MovePoint(head2, dir)));

            if (possibleDirs.Count > 0)
            {
                pendingDirection2 = possibleDirs[random.Next(possibleDirs.Count)];
            }
            else
            {
                // Aucune direction sûre, choix aléatoire (risqué)
                pendingDirection2 = (Direction)random.Next(4);
            }
        }

        private void UpdateVampireBehavior(Point head2, Point playerHead)
        {
            // Absorber la vie/vitesse du joueur à distance
            int distanceToPlayer = Math.Abs(head2.X - playerHead.X) + Math.Abs(head2.Y - playerHead.Y);
            if (distanceToPlayer <= VampireRange)
            {
                // Activer l'effet vampirique (logique à implémenter)
                aiIsVampiring = true;
                // Peut-être se rapprocher du joueur
                UpdateBasicAvoidanceAndChase(head2, playerHead, true);
            }
            else
            {
                aiIsVampiring = false;
                // Se déplacer vers le joueur ou erratiquement
                UpdateBasicAvoidanceAndChase(head2, playerHead, true);
            }
        }

        private void UpdateGenericBossBehavior(Point head2, Point playerHead)
        {
            // Placeholder pour les comportements spécifiques des boss
            // Ex: patterns de déplacement fixes, attaques programmées
            // Utiliser UpdateBasicAvoidanceAndChase comme base pour éviter les obstacles de base
            UpdateBasicAvoidanceAndChase(head2, playerHead, true); // Exemple : agressif mais évite les murs
            // Ajouter logique spécifique Boss ici (attaques, patterns, etc.)
        }

        // --- FONCTION DE BASE (utilisée par plusieurs personnalités) ---
        // --- CORRIGÉE ---
        private void UpdateBasicAvoidanceAndChase(Point head2, Point playerHead, bool aggressive)
        {
            // 1. DÉTECTION DES DANGERS IMMÉDIATS
            Dictionary<Direction, Point> adjacentMoves = new Dictionary<Direction, Point>
            {
                { Direction.Up, MovePoint(head2, Direction.Up) },
                { Direction.Down, MovePoint(head2, Direction.Down) },
                { Direction.Left, MovePoint(head2, Direction.Left) },
                { Direction.Right, MovePoint(head2, Direction.Right) }
            };

            List<Direction> dangerousMoves = new List<Direction>();
            List<Direction> safeMoves = new List<Direction>();

            foreach (var move in adjacentMoves)
            {
                Point nextPos = move.Value;
                Direction dir = move.Key;

                if (!IsInside(nextPos) || obstaclePositions.Contains(nextPos) || snake2.Contains(nextPos))
                {
                    dangerousMoves.Add(dir);
                    continue;
                }

                // --- CORRECTION 8.1 (du module précédent) : Condition projectiles/mines ---
                if (snake.Contains(nextPos))
                {
                    if (aggressive)
                    {
                        // Pour une IA agressive, la collision avec le joueur est un objectif, pas un danger immédiat ici
                        // On peut ignorer cette case pour la liste des dangers, mais la traiter séparément si nécessaire
                    }
                    else
                    {
                        dangerousMoves.Add(dir); // Pour une IA non-agressive, c'est un danger
                    }
                }
                else if (projectiles.Any(p => p.Position == nextPos) || mines.Any(m => m.Position == nextPos))
                {
                     dangerousMoves.Add(dir);
                     continue; // Passer à la prochaine direction
                }

                // Si la case n'est pas dangereuse, c'est une option sûre
                safeMoves.Add(dir);
            }

            // 2. PRIORISER LES ACTIONS STRATÉGIQUES
            Direction bestDirection = Direction.Right; // Valeur par défaut
            bool hasPriorityTarget = false;

            if (safeMoves.Count > 0)
            {
                // a. Chercher la pomme si elle est dans une case sûre adjacente
                foreach (Direction safeDir in safeMoves)
                {
                    if (adjacentMoves[safeDir] == applePosition)
                    {
                        bestDirection = safeDir;
                        hasPriorityTarget = true;
                        break; // Aller directement vers la pomme si possible
                    }
                }

                // b. Fuir le joueur si très proche (sauf si agressive ou kamikaze)
                if (!hasPriorityTarget && !aggressive)
                {
                    int distanceToPlayer = Math.Abs(head2.X - playerHead.X) + Math.Abs(head2.Y - playerHead.Y);
                    if (distanceToPlayer <= SafeDistanceToPlayer)
                    {
                        // --- CORRECTION 8.2 (du module précédent) : Calcul de fleeDirection ---
                        int dx = playerHead.X - head2.X;
                        int dy = playerHead.Y - head2.Y;
                        Direction fleeDirection = (Math.Abs(dx) > Math.Abs(dy)) ?
                            (dx > 0 ? Direction.Left : Direction.Right) : // Fuir horizontalement
                            (dy > 0 ? Direction.Up : Direction.Down);     // Fuir verticalement

                        if (safeMoves.Contains(fleeDirection))
                        {
                            bestDirection = fleeDirection;
                            hasPriorityTarget = true;
                        }
                        else
                        {
                            // Si la fuite directe n'est pas sûre, choisir une autre direction sûre
                            Direction bestSafeDir = fleeDirection; // Valeur par défaut
                            int furthestDist = -1;
                            foreach (Direction candidateDir in safeMoves)
                            {
                                Point candidatePos = adjacentMoves[candidateDir];
                                int distToPlayer = Math.Abs(candidatePos.X - playerHead.X) + Math.Abs(candidatePos.Y - playerHead.Y);
                                if (distToPlayer > furthestDist)
                                {
                                    furthestDist = distToPlayer;
                                    bestSafeDir = candidateDir;
                                }
                            }
                            bestDirection = bestSafeDir;
                            hasPriorityTarget = true;
                        }
                    }
                }

                // c. Poursuivre le joueur (si agressive)
                if (!hasPriorityTarget && aggressive)
                {
                     int dx = playerHead.X - head2.X;
                     int dy = playerHead.Y - head2.Y;
                     Direction pursueDirection = (Math.Abs(dx) > Math.Abs(dy)) ?
                         (dx > 0 ? Direction.Right : Direction.Left) : // Poursuivre horizontalement
                         (dy > 0 ? Direction.Down : Direction.Up);     // Poursuivre verticalement

                     if (safeMoves.Contains(pursueDirection))
                     {
                         bestDirection = pursueDirection;
                         hasPriorityTarget = true;
                     }
                     // Sinon, continuer avec d'autres heuristiques
                }

                // d. Choix heuristique dans les safeMoves (si pas de cible prioritaire)
                if (!hasPriorityTarget)
                {
                     Direction bestHeuristicDir = safeMoves[0]; // Valeur par défaut
                     int bestScore = int.MinValue;

                     foreach (Direction candidateDir in safeMoves)
                     {
                         Point candidatePos = adjacentMoves[candidateDir];
                         int heuristicScore = EvaluateMoveHeuristic(candidatePos, head2, playerHead, aggressive);

                         if (heuristicScore > bestScore)
                         {
                             bestScore = heuristicScore;
                             bestHeuristicDir = candidateDir;
                         }
                     }
                     bestDirection = bestHeuristicDir;
                }
            }
            else
            {
                // AUCUN MOUVEMENT SÛR : Situation critique
                // --- AMÉLIORATION : Meilleur choix en situation critique ---
                Direction bestUnsafeDir = direction2; // Valeur par défaut (garder la direction actuelle)
                int bestScore = int.MinValue;

                foreach (var move in adjacentMoves)
                {
                    Direction dir = move.Key;
                    Point pos = move.Value;

                    // Calculer un score pour cette direction non sûre
                    int distToPlayer = Math.Abs(pos.X - playerHead.X) + Math.Abs(pos.Y - playerHead.Y);
                    int dangerScore = ComputeDangerScore(pos);
                    // Pondération différente en situation critique
                    int heuristicScore = (aggressive ? -distToPlayer : distToPlayer) * 5 - dangerScore;

                    if (heuristicScore > bestScore)
                    {
                        bestScore = heuristicScore;
                        bestUnsafeDir = dir;
                    }
                }
                bestDirection = bestUnsafeDir;
            }

            // 3. APPLIQUER LA DÉCISION
            pendingDirection2 = bestDirection;
        }

        private void AttemptToFlee(Point head2, Point playerHead)
        {
            // Calculer la direction opposée au joueur
            int dx = playerHead.X - head2.X;
            int dy = playerHead.Y - head2.Y;
            Direction fleeDirection = (Math.Abs(dx) > Math.Abs(dy)) ?
                (dx > 0 ? Direction.Left : Direction.Right) : // Fuir horizontalement
                (dy > 0 ? Direction.Up : Direction.Down);     // Fuir verticalement

            // Vérifier si la direction de fuite est sûre
            Point fleePos = MovePoint(head2, fleeDirection);
            if (IsSafeMove(fleePos))
            {
                pendingDirection2 = fleeDirection;
            }
            else
            {
                // Sinon, choisir la direction sûre la plus éloignée du joueur
                List<Direction> possibleDirs = new List<Direction> { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
                possibleDirs.RemoveAll(dir => !IsSafeMove(MovePoint(head2, dir)));

                if (possibleDirs.Count > 0)
                {
                    Direction bestFleeDir = possibleDirs[0];
                    int bestDist = int.MinValue;
                    foreach (Direction dir in possibleDirs)
                    {
                        Point pos = MovePoint(head2, dir);
                        int distToPlayer = Math.Abs(pos.X - playerHead.X) + Math.Abs(pos.Y - playerHead.Y);
                        if (distToPlayer > bestDist)
                        {
                            bestDist = distToPlayer;
                            bestFleeDir = dir;
                        }
                    }
                    pendingDirection2 = bestFleeDir;
                }
                else
                {
                    // Aucun choix sûr, choix aléatoire (risqué)
                    pendingDirection2 = (Direction)random.Next(4);
                }
            }
        }

        // --- HELPERS ---
        private bool IsSafeMove(Point pos)
        {
            return IsInside(pos) && !obstaclePositions.Contains(pos) && !snake2.Contains(pos) && !snake.Contains(pos) &&
                   !projectiles.Any(p => p.Position == pos) && !mines.Any(m => m.Position == pos);
        }

        // Helper pour évaluer un mouvement selon une heuristique
        private int EvaluateMoveHeuristic(Point pos, Point currentHead, Point playerHead, bool aggressive)
        {
            int score = 0;

            // Heuristique : Distance au joueur (inversée si agressive)
            int distToPlayer = Math.Abs(pos.X - playerHead.X) + Math.Abs(pos.Y - playerHead.Y);
            score += (aggressive ? -distToPlayer : distToPlayer) * 5; // Pondération

            // Heuristique : Distance aux bords
            int distToEdgeX = Math.Min(pos.X, gridWidth - 1 - pos.X);
            int distToEdgeY = Math.Min(pos.Y, gridHeight - 1 - pos.Y);
            int minDistToEdge = Math.Min(distToEdgeX, distToEdgeY);
            score += minDistToEdge * 3; // Pondération

            // Heuristique : Vers la pomme
            if (applePosition != Point.Empty)
            {
                int distToApple = Math.Abs(pos.X - applePosition.X) + Math.Abs(pos.Y - applePosition.Y);
                int currentDistToApple = Math.Abs(currentHead.X - applePosition.X) + Math.Abs(currentHead.Y - applePosition.Y);
                if (distToApple < currentDistToApple) score += 10; // Bonus pour se rapprocher
            }

            // Heuristique : Liberté de mouvement
            int freeSpaces = 0;
            Dictionary<Direction, Point> neighbors = new Dictionary<Direction, Point>
            {
                { Direction.Up, MovePoint(pos, Direction.Up) },
                { Direction.Down, MovePoint(pos, Direction.Down) },
                { Direction.Left, MovePoint(pos, Direction.Left) },
                { Direction.Right, MovePoint(pos, Direction.Right) }
            };
            foreach(var kvp in neighbors)
            {
                Point neighbor = kvp.Value;
                if (IsSafeMove(neighbor))
                {
                    freeSpaces++;
                    // Exploration partielle pour la liberté future
                    int depth = 2;
                    int subSpaceCount = CountAccessibleSpaces(neighbor, depth, new HashSet<Point> { pos });
                    score += subSpaceCount * 1;
                }
            }
            score += freeSpaces * 2;

            // Soustraire le danger
            score -= ComputeDangerScore(pos);

            return score;
        }

        // Helper récursif pour compter les cases accessibles
        private int CountAccessibleSpaces(Point start, int depth, HashSet<Point> visited)
        {
            if (depth <= 0 || !IsSafeMove(start) || visited.Contains(start))
            {
                return 0;
            }

            visited.Add(start);
            int count = 1;

            foreach(Direction dir in Enum.GetValues(typeof(Direction)))
            {
                Point next = MovePoint(start, dir);
                count += CountAccessibleSpaces(next, depth - 1, visited);
            }

            return count;
        }

        // Helper pour calculer un score de danger
        private int ComputeDangerScore(Point pos)
        {
            int score = 0;

            // Danger : Proximité des bords
            int distToEdgeX = Math.Min(pos.X, gridWidth - 1 - pos.X);
            int distToEdgeY = Math.Min(pos.Y, gridHeight - 1 - pos.Y);
            int minDistToEdge = Math.Min(distToEdgeX, distToEdgeY);
            if (minDistToEdge <= 1) score += 100;
            else if (minDistToEdge <= 2) score += 50;

            // Danger : Proximité des obstacles
            int radius = 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    Point checkPos = new Point(pos.X + dx, pos.Y + dy);
                    if (IsInside(checkPos) && obstaclePositions.Contains(checkPos))
                    {
                        int dist = Math.Abs(dx) + Math.Abs(dy);
                        score += (radius + 1 - dist) * 10;
                    }
                }
            }

            // Danger : Proximité de projectiles/mine
            if (projectiles.Any(p => p.Position == pos)) score += 200;
            if (mines.Any(m => m.Position == pos)) score += 150;

            return score;
        }

        // Helper pour obtenir la direction opposée
        private Direction Opposite(Direction dir)
        {
            return dir switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                Direction.Right => Direction.Left,
                _ => dir
            };
        }

        // Helper pour calculer le point suivant
        private Point MovePoint(Point pos, Direction dir)
        {
            return dir switch
            {
                Direction.Up => new Point(pos.X, pos.Y - 1),
                Direction.Down => new Point(pos.X, pos.Y + 1),
                Direction.Left => new Point(pos.X - 1, pos.Y),
                Direction.Right => new Point(pos.X + 1, pos.Y),
                _ => pos
            };
        }

        // Helper pour vérifier si un point est dans les limites
        private bool IsInside(Point pos)
        {
            return pos.X >= 0 && pos.X < gridWidth && pos.Y >= 0 && pos.Y < gridHeight;
        }

        // Helper pour trouver le power-up le plus proche
        // --- CORRECTION 7.2 : Assurez-vous que specialPositions est accessible ---
        private Point FindClosestPowerUp(Point head)
        {
            // Cette fonction dépend de la variable 'specialPositions' qui doit exister dans le moteur de jeu
            // Exemple : Dictionary<Point, SpecialKind> specialPositions = ...
            // Si ce n'est pas le cas, cette IA ne pourra pas utiliser cette fonctionnalité
            // et devra se reposer sur d'autres heuristiques ou comportements.

            // Supposons que specialPositions est un champ de votre classe de jeu
            // Dictionary<Point, SpecialKind> specialPositions = ... // Doit être défini ailleurs

            Point closest = Point.Empty;
            int minDist = int.MaxValue;

            // --- SUPPOSITION : specialPositions est un champ de la classe ---
            // foreach (var kvp in specialPositions) // specialPositions est un champ de votre jeu
            // {
            //     int dist = Math.Abs(kvp.Key.X - head.X) + Math.Abs(kvp.Key.Y - head.Y);
            //     if (dist < minDist)
            //     {
            //         minDist = dist;
            //         closest = kvp.Key;
            //     }
            // }

            // Pour que cette fonction compile et fonctionne, vous devez avoir une structure similaire à :
            // private Dictionary<Point, SpecialKind> specialPositions = new Dictionary<Point, SpecialKind>();
            // Et cette structure doit être mise à jour par le moteur de jeu (apparition/disparition de power-ups).

            // Retourne Point.Empty si aucune cible n'est trouvée ou si la structure n'est pas disponible
            return closest;
        }

        // Constante pour la distance de sécurité
        private const int SafeDistanceToPlayer = 2;
    }
}
