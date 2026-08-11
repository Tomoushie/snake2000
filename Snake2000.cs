using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class SnakeGame : Form
{
    // --- Configuration: small grid, chunky pixels, monochrome LCD palette ---
    // (this is what gave 2000s phone Snake its distinctive look)
    private const int BaseGridWidth = 20;
    private const int BaseGridHeight = 20;
    private const int MaxGridSize = 30;
    private const int CellSize = 20;
    private const int TopBarHeight = 30;
    private const int BaseIntervalMs = 150;
    private const int MinIntervalMs = 60;
    private const int MaxNameLength = 10;
    private const int ScoreboardRows = 8;

    // --- Progressive levels: eat apples to level up, which speeds the snake
    // up and, every few levels, grows the playfield - like later phone Snake games ---
    private const int FoodsPerLevel = 4;
    private const int SpeedStepMs = 8;
    private const int LevelsPerGridGrowth = 3;
    private const int GridGrowthStep = 2;
    private const int BannerDurationTicks = 10;

    // --- Bonus / malus items: a special item occasionally appears alongside
    // the normal food and vanishes again after a while if not eaten ---
    private const int SpecialSpawnEveryFoods = 3;
    private const int SpecialLifetimeTicks = 45;
    private const int SpeedBonusChancePercent = 60; // vs. trap
    private const int SpeedBonusPoints = 2;
    private const int SpeedBoostTicks = 25;
    private const int SpeedBoostIntervalMs = 45;
    private const int TrapShrinkAmount = 3;
    private const int MinSnakeLength = 1;

    // --- Boss fight / procedural generation ---
    private const int BossInitialHealth = 10;
    private const int BossMoveIntervalTicks = 8;
    private const int ProceduralObstacleBaseCount = 8;
    private const int ProceduralObstaclePerLevel = 2;
    private const int MaxProceduralObstacles = 45;

    // --- Animation / sound ---
    private const int AnimationIntervalMs = 40; // ~25 fps redraw clock, independent of game speed
    private const int DeathFlashTicks = 12;      // how long the collision flash pulses for

    // --- Duel (local or online) ---
    private const int DuelGridSize = 22;
    private const int DuelPort = 7788;
    private const int MaxIpLength = 45;

    // Classic Nokia 3310-style LCD colors: pale green screen, dark green "ink"
    private static readonly Color BackgroundColor = Color.FromArgb(196, 217, 161);
    private static readonly Color GridLineColor = Color.FromArgb(180, 201, 146);
    private static readonly Color InkColor = Color.FromArgb(52, 71, 45);

    private static readonly string ProfilesDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snake2000");
    private static readonly string ProfilesPath = Path.Combine(ProfilesDirectory, "profiles.txt");

    private static readonly string GlobalScoresDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Snake2000");
    private static readonly string GlobalScoresPath = Path.Combine(GlobalScoresDirectory, "global_scores.txt");

    private static readonly string[] SnakeColorNames = new[] { "CLASSIC", "NEON", "CORAL", "CYAN", "VIOLET" };
    private static readonly string[] SnakeShapeNames = new[] { "ROUNDED", "BLOCK", "SLIM", "SPIKED" };
    private static readonly string[] SnakeThemeNames = new[] { "CLASSIC", "JUNGLE", "CITY", "SPACE" };

    private enum GameState
    {
        NameEntry, Ready, Playing, Paused, GameOver, Scoreboard, ProfileHistory,
        ModeSelect, OnlineHostWait, OnlineJoinEntry, OnlineConnecting, CustomizeSnake, Achievements
    }
    private enum Direction { Up, Down, Left, Right }
    private enum SnakeColorTheme { Classic, Neon, Coral, Cyan, Violet }
    private enum SnakeShapeStyle { Rounded, Block, Slim, Spiked }
    private enum BoardTheme { Classic, Jungle, City, Space }
    private enum SpecialKind { None, Speed, Shield, Trap }
    private enum GameMode { Solo, DuelLocal, AIDuel, BossFight, Procedural, Zen, DuelHost, DuelGuest }
    private enum DuelWinner { None, Player1, Player2, Draw }

    private class SnakeAppearance
    {
        public SnakeColorTheme ColorTheme;
        public SnakeShapeStyle ShapeStyle;
        public BoardTheme Theme;

        public SnakeAppearance()
        {
            ColorTheme = SnakeColorTheme.Classic;
            ShapeStyle = SnakeShapeStyle.Rounded;
            Theme = BoardTheme.Classic;
        }
    }

    private class GameHistoryEntry
    {
        public int Score;
        public TimeSpan Time;
        public DateTime PlayedAt;

        public GameHistoryEntry(int score, TimeSpan time, DateTime playedAt)
        {
            Score = score;
            Time = time;
            PlayedAt = playedAt;
        }
    }

    private class PlayerProfile
    {
        public readonly string Name;
        public int BestScore;
        public TimeSpan BestTime;
        public int GamesPlayed;
        public SnakeAppearance Appearance;
        public List<GameHistoryEntry> History;

        // Lifetime stats used to unlock achievements (see AchievementDef/Achievements below).
        public int TotalApplesEaten;
        public int MaxLevelReached;
        public int MaxSnakeLength;
        public int LongestSurvivalSeconds;
        public int BossesDefeated;
        public bool WonBossFightWithoutShield;
        public HashSet<string> UnlockedAchievements;

        public PlayerProfile(string name)
        {
            Name = name;
            Appearance = new SnakeAppearance();
            History = new List<GameHistoryEntry>();
            UnlockedAchievements = new HashSet<string>();
        }
    }

    // A single achievement definition: a stable Id (persisted), display text, and the
    // condition - evaluated against a profile's lifetime stats - that unlocks it.
    private class AchievementDef
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        public readonly Func<PlayerProfile, bool> IsUnlocked;

        public AchievementDef(string id, string title, string description, Func<PlayerProfile, bool> isUnlocked)
        {
            Id = id;
            Title = title;
            Description = description;
            IsUnlocked = isUnlocked;
        }
    }

    private static readonly AchievementDef[] Achievements = new[]
    {
        new AchievementDef("FIRST_BITE", "FIRST BITE", "EAT YOUR FIRST APPLE", p => p.TotalApplesEaten >= 1),
        new AchievementDef("GLUTTON_50", "GLUTTON", "EAT 50 APPLES TOTAL", p => p.TotalApplesEaten >= 50),
        new AchievementDef("GLUTTON_200", "GOURMAND", "EAT 200 APPLES TOTAL", p => p.TotalApplesEaten >= 200),
        new AchievementDef("HIGH_SCORE_25", "HIGH SCORE", "SCORE 25 IN ONE GAME", p => p.BestScore >= 25),
        new AchievementDef("LEVEL_10", "VETERAN", "REACH LEVEL 10", p => p.MaxLevelReached >= 10),
        new AchievementDef("SURVIVOR_5MIN", "MARATHONER", "SURVIVE 5 MINUTES", p => p.LongestSurvivalSeconds >= 300),
        new AchievementDef("BOSS_SLAYER", "BOSS SLAYER", "DEFEAT THE BOSS", p => p.BossesDefeated >= 1),
        new AchievementDef("BOSS_NO_SHIELD", "NO SAFETY NET", "BEAT THE BOSS WITHOUT A SHIELD", p => p.WonBossFightWithoutShield),
        new AchievementDef("LONG_BOI", "ANACONDA", "REACH 30 SEGMENTS LONG", p => p.MaxSnakeLength >= 30),
        new AchievementDef("GAMES_25", "REGULAR", "PLAY 25 GAMES", p => p.GamesPlayed >= 25),
    };

    private class GlobalScoreEntry
    {
        public string Name;
        public int Score;
        public TimeSpan Time;
        public DateTime SubmittedAt;

        public GlobalScoreEntry(string name)
        {
            Name = name;
        }
    }

    // A single beeper note; sequences of these make up the sound effects
    private struct Note
    {
        public readonly int Frequency;
        public readonly int DurationMs;

        public Note(int frequency, int durationMs)
        {
            Frequency = frequency;
            DurationMs = durationMs;
        }
    }

    // Plays a short sequence of beeps on a background thread so the UI never stalls
    private static void PlayJingle(params Note[] notes)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            foreach (Note n in notes)
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                        Console.Beep(n.Frequency, n.DurationMs);
                }
                catch (Exception) { }
            }
        });
    }

    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer animationTimer;
    private readonly Stopwatch stopwatch = new Stopwatch();
    private readonly List<Point> snake = new List<Point>();
    private readonly List<Point> snake2 = new List<Point>();
    private readonly List<Point> obstacles = new List<Point>();
    private readonly HashSet<Point> obstaclePositions = new HashSet<Point>();
    private readonly HashSet<Point> snakePositions = new HashSet<Point>();
    private readonly HashSet<Point> snake2Positions = new HashSet<Point>();
    private Point bossPosition = Point.Empty;
    private Direction bossDirection = Direction.Left;
    private int bossHealth;
    private int bossMoveTicksLeft;
    private bool bossActive;
    private readonly Random random = new Random();
    private readonly List<PlayerProfile> profiles;
    private readonly List<GlobalScoreEntry> globalScores;

    private GameMode mode = GameMode.Solo;
    private int gridWidth = BaseGridWidth;
    private int gridHeight = BaseGridHeight;
    private Direction direction;
    private Direction pendingDirection;
    private Direction direction2;
    private Direction pendingDirection2;
    private int score2;
    private string player2Name = "PLAYER 2";
    private DuelWinner duelWinner = DuelWinner.None;
    private Point food;
    private SpecialKind specialKind;
    private Point specialPosition;
    private int specialTicksLeft;
    private int foodsSinceSpecial;
    private int speedBoostTicksLeft;
    private bool shieldActive;
    private GameState state;
    private int score;
    private int level;
    private string bannerText = "";
    private int bannerTicksLeft;
    private int currentInterval;
    private bool won;
    private bool isNewBest;
    private string nameInput = "";
    private PlayerProfile? currentProfile;
    private SnakeAppearance localAppearance = new SnakeAppearance();
    private int deathFlashTicksLeft;

    // Per-game achievement tracking: whether the shield ever saved the player this
    // game (needed for the "no shield" boss achievement), and the titles of any
    // achievements unlocked at the end of the current game (shown on the Game Over screen).
    private bool shieldUsedThisGame;
    private readonly List<string> newlyUnlockedThisGame = new List<string>();

    // Networking (duel host/guest)
    private TcpListener? hostListener;
    private TcpClient? duelClient;
    private StreamReader? netReader;
    private StreamWriter? netWriter;
    private bool netConnected;
    private string joinIpInput = "";
    private string netStatusMessage = "";

    public SnakeGame()
    {
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = InkColor;
        Text = "Snake";
        KeyPreview = true;

        timer = new System.Windows.Forms.Timer();
        timer.Tick += OnTimerTick;

        // Runs continuously (independent of game speed) so sprites can pulse,
        // eyes can blink and menus can breathe even while the snake itself is still.
        animationTimer = new System.Windows.Forms.Timer();
        animationTimer.Interval = AnimationIntervalMs;
        animationTimer.Tick += OnAnimationTick;
        animationTimer.Start();

        profiles = LoadProfiles();
        globalScores = LoadGlobalScores();
        ResetGame();
        state = GameState.NameEntry;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            animationTimer.Dispose();
            CloseNetworking();
        }
        base.Dispose(disposing);
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (deathFlashTicksLeft > 0)
            deathFlashTicksLeft--;
        Invalidate();
    }

    // ------------------------------------------------------------------
    // Game lifecycle
    // ------------------------------------------------------------------

    private void ResetGame()
    {
        gridWidth = BaseGridWidth;
        gridHeight = BaseGridHeight;
        ClientSize = new Size(gridWidth * CellSize, gridHeight * CellSize + TopBarHeight);

        snake.Clear();
        snakePositions.Clear();
        // Single-snake modes never touch snake2, but it can hold stale data from a
        // previous duel (e.g. switching from Local Duel to Zen) - clear it so it
        // doesn't get drawn or block food/special spawns via IsOccupied.
        snake2.Clear();
        snake2Positions.Clear();
        obstacles.Clear();
        obstaclePositions.Clear();

        int startX = gridWidth / 2;
        int startY = gridHeight / 2;
        for (int i = 2; i >= 0; i--)
        {
            Point segment = new Point(startX - i, startY);
            snake.Add(segment);
            snakePositions.Add(segment);
        }

        direction = Direction.Right;
        pendingDirection = Direction.Right;
        score = 0;
        level = 1;
        bannerText = "";
        bannerTicksLeft = 0;
        specialKind = SpecialKind.None;
        specialTicksLeft = 0;
        foodsSinceSpecial = 0;
        speedBoostTicksLeft = 0;
        deathFlashTicksLeft = 0;
        won = false;
        isNewBest = false;
        bossActive = false;
        bossHealth = 0;
        bossMoveTicksLeft = BossMoveIntervalTicks;
        currentInterval = BaseIntervalMs;
        shieldUsedThisGame = false;
        newlyUnlockedThisGame.Clear();
        stopwatch.Reset();

        if (mode == GameMode.BossFight || mode == GameMode.Procedural)
            GenerateProceduralObstacles();

        if (mode == GameMode.BossFight)
        {
            bossActive = true;
            bossHealth = BossInitialHealth;
            bossMoveTicksLeft = BossMoveIntervalTicks;
            bossDirection = Direction.Left;
            bossPosition = FindFreePoint();
        }

        PlaceFood();
    }

    // Called when enough apples have been eaten to cross a level threshold.
    // Levels always speed the snake up, and every few levels also grow the board.
    private void LevelUp(int newLevel)
    {
        level = newLevel;
        currentInterval = Math.Max(MinIntervalMs, BaseIntervalMs - (level - 1) * SpeedStepMs);

        // Don't clobber an active speed-boost interval; it will fall back to
        // currentInterval on its own once the boost runs out.
        if (speedBoostTicksLeft <= 0)
            timer.Interval = currentInterval;

        if (level % LevelsPerGridGrowth == 0 && (gridWidth < MaxGridSize || gridHeight < MaxGridSize))
            GrowBoard();

        ShowBanner("LEVEL " + level);
        PlayJingle(new Note(1400, 70), new Note(1800, 70), new Note(2200, 90));
    }

    private void GrowBoard()
    {
        gridWidth = Math.Min(MaxGridSize, gridWidth + GridGrowthStep);
        gridHeight = Math.Min(MaxGridSize, gridHeight + GridGrowthStep);
        ClientSize = new Size(gridWidth * CellSize, gridHeight * CellSize + TopBarHeight);
    }

    private void CheckLevelUp()
    {
        int newLevel = 1 + score / FoodsPerLevel;
        if (newLevel > level)
            LevelUp(newLevel);
    }

    private void ShowBanner(string text)
    {
        bannerText = text;
        bannerTicksLeft = BannerDurationTicks;
    }

    // Occasionally drops a bonus (speed fruit) or malus (trap) next to the normal food.
    private void SpawnSpecial()
    {
        int roll = random.Next(100);
        if (roll < SpeedBonusChancePercent)
            specialKind = SpecialKind.Speed;
        else if (roll < SpeedBonusChancePercent + 20)
            specialKind = SpecialKind.Shield;
        else
            specialKind = SpecialKind.Trap;

        Point candidate;
        int attempts = 0;
        do
        {
            candidate = new Point(random.Next(gridWidth), random.Next(gridHeight));
            attempts++;
        } while ((IsOccupied(candidate) || candidate == food || (bossActive && candidate == bossPosition)) && attempts < 200);

        specialPosition = candidate;
        specialTicksLeft = SpecialLifetimeTicks;
    }

    private bool IsOccupied(Point p)
    {
        return snake.Contains(p)
            || (mode != GameMode.Solo && snake2.Contains(p))
            || obstaclePositions.Contains(p)
            || (bossActive && bossPosition == p);
    }

    // Speed fruit: grants a short burst of extra speed on top of the current level speed.
    private void ApplySpeedBoost()
    {
        speedBoostTicksLeft = SpeedBoostTicks;
        timer.Interval = SpeedBoostIntervalMs;
    }

    private void ApplyShield()
    {
        shieldActive = true;
        ShowBanner("SHIELD READY");
    }

    // Trap: shrinks the snake instead of letting it grow, down to a minimum length.
    private void ApplyTrap()
    {
        ApplyTrapTo(snake);
    }

    private static void ApplyTrapTo(List<Point> body)
    {
        int removeCount = Math.Min(TrapShrinkAmount + 1, body.Count - MinSnakeLength);
        if (removeCount > 0)
            body.RemoveRange(0, removeCount);
    }

    private void StartGame()
    {
        ResetGame();
        state = GameState.Playing;
        timer.Interval = currentInterval;
        timer.Start();
        stopwatch.Start();
        Invalidate();
    }

    private void StartLocalDuelSetup()
    {
        mode = GameMode.DuelLocal;
        player2Name = "PLAYER 2";
        state = GameState.Ready;
        Invalidate();
    }

    private void StartAIDuelSetup()
    {
        mode = GameMode.AIDuel;
        player2Name = "COMPUTER";
        state = GameState.Ready;
        Invalidate();
    }

    private void StartBossFightSetup()
    {
        mode = GameMode.BossFight;
        player2Name = "BOSS";
        state = GameState.Ready;
        Invalidate();
    }

    private void StartProceduralSetup()
    {
        mode = GameMode.Procedural;
        state = GameState.Ready;
        Invalidate();
    }

    private void StartZenSetup()
    {
        mode = GameMode.Zen;
        state = GameState.Ready;
        Invalidate();
    }

    private Point FindFreePoint()
    {
        Point candidate = new Point(0, 0);
        int attempts = 0;
        do
        {
            candidate = new Point(random.Next(gridWidth), random.Next(gridHeight));
            attempts++;
        } while ((IsOccupied(candidate) || candidate == food) && attempts < 1000);

        return candidate;
    }

    private void ClearObstacles()
    {
        obstacles.Clear();
        obstaclePositions.Clear();
    }

    private void GenerateProceduralObstacles()
    {
        ClearObstacles();
        int count = Math.Min(MaxProceduralObstacles, ProceduralObstacleBaseCount + (level - 1) * ProceduralObstaclePerLevel);
        int attempts = 0;

        while (obstacles.Count < count && attempts < count * 20)
        {
            Point candidate = new Point(random.Next(gridWidth), random.Next(gridHeight));
            if (!IsOccupied(candidate))
            {
                obstacles.Add(candidate);
                obstaclePositions.Add(candidate);
            }
            attempts++;
        }
    }

    private void UpdateBossMovement()
    {
        if (!bossActive)
            return;

        bossMoveTicksLeft--;
        if (bossMoveTicksLeft > 0)
            return;

        bossMoveTicksLeft = BossMoveIntervalTicks;
        List<Direction> candidates = new List<Direction>();
        foreach (Direction candidate in new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
        {
            Point next = MoveHead(bossPosition, candidate);
            if (IsOutOfBounds(next) || obstaclePositions.Contains(next) || snakePositions.Contains(next) || next == food || next == specialPosition)
                continue;

            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return;

        bossDirection = candidates[random.Next(candidates.Count)];
        bossPosition = MoveHead(bossPosition, bossDirection);
    }

    private void ResetDuel()
    {
        gridWidth = DuelGridSize;
        gridHeight = DuelGridSize;
        ClientSize = new Size(gridWidth * CellSize, gridHeight * CellSize + TopBarHeight);

        snake.Clear();
        snake2.Clear();
        snakePositions.Clear();
        snake2Positions.Clear();
        obstacles.Clear();
        obstaclePositions.Clear();

        int y1 = gridHeight / 3;
        int y2 = gridHeight - gridHeight / 3 - 1;
        for (int i = 2; i >= 0; i--)
        {
            Point p1 = new Point(3 + i, y1);
            snake.Add(p1);
            snakePositions.Add(p1);
        }
        for (int i = 2; i >= 0; i--)
        {
            Point p2 = new Point(gridWidth - 4 - i, y2);
            snake2.Add(p2);
            snake2Positions.Add(p2);
        }

        direction = Direction.Right;
        pendingDirection = Direction.Right;
        direction2 = Direction.Left;
        pendingDirection2 = Direction.Left;

        score = 0;
        score2 = 0;
        level = 1;
        bannerText = "";
        bannerTicksLeft = 0;
        specialKind = SpecialKind.None;
        specialTicksLeft = 0;
        foodsSinceSpecial = 0;
        speedBoostTicksLeft = 0;
        deathFlashTicksLeft = 0;
        duelWinner = DuelWinner.None;
        bossActive = false;
        bossHealth = 0;
        bossMoveTicksLeft = BossMoveIntervalTicks;
        currentInterval = BaseIntervalMs;
        stopwatch.Reset();
        PlaceFood();
    }

    private void StartDuel()
    {
        ResetDuel();
        state = GameState.Playing;
        timer.Interval = currentInterval;
        stopwatch.Start();

        // The guest doesn't run its own simulation - it just renders whatever
        // the host streams to it and sends its own key presses back.
        if (mode != GameMode.DuelGuest)
            timer.Start();

        if (mode == GameMode.DuelHost)
            SendLine("RESTART");

        Invalidate();
    }

    private static Point MoveHead(Point head, Direction dir)
    {
        Point p = head;
        switch (dir)
        {
            case Direction.Up: p.Y--; break;
            case Direction.Down: p.Y++; break;
            case Direction.Left: p.X--; break;
            case Direction.Right: p.X++; break;
        }
        return p;
    }

    private bool IsOutOfBounds(Point p)
    {
        return p.X < 0 || p.X >= gridWidth || p.Y < 0 || p.Y >= gridHeight;
    }

    // Zen mode: instead of dying at the edge, the snake re-enters from the opposite side.
    private Point WrapPoint(Point p)
    {
        int x = ((p.X % gridWidth) + gridWidth) % gridWidth;
        int y = ((p.Y % gridHeight) + gridHeight) % gridHeight;
        return new Point(x, y);
    }

    private static Direction Opposite(Direction d)
    {
        switch (d)
        {
            case Direction.Up: return Direction.Down;
            case Direction.Down: return Direction.Up;
            case Direction.Left: return Direction.Right;
            default: return Direction.Left;
        }
    }

    private void PlaceFood()
    {
        if (snake.Count + snake2.Count >= gridWidth * gridHeight)
            return; // board full: handled as a win in OnTimerTick (solo only)

        Point candidate;
        do
        {
            candidate = new Point(random.Next(gridWidth), random.Next(gridHeight));
        } while (IsOccupied(candidate) || candidate == specialPosition);

        food = candidate;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (mode == GameMode.DuelLocal || mode == GameMode.AIDuel || mode == GameMode.DuelHost || mode == GameMode.DuelGuest)
        {
            OnDuelTimerTick();
            return;
        }

        if (mode == GameMode.BossFight)
            UpdateBossMovement();

        direction = pendingDirection;
        Point head = snake[snake.Count - 1];
        Point newHead = head;

        switch (direction)
        {
            case Direction.Up: newHead.Y--; break;
            case Direction.Down: newHead.Y++; break;
            case Direction.Left: newHead.X--; break;
            case Direction.Right: newHead.X++; break;
        }

        // Hitting a wall ends the game outright, no wrap-around - just like the phone original.
        // Zen mode is the one exception: the snake re-enters from the opposite edge instead.
        if (newHead.X < 0 || newHead.X >= gridWidth || newHead.Y < 0 || newHead.Y >= gridHeight)
        {
            if (mode == GameMode.Zen)
            {
                newHead = WrapPoint(newHead);
            }
            else if (shieldActive)
            {
                shieldActive = false;
                specialKind = SpecialKind.None;
                shieldUsedThisGame = true;
                ShowBanner("SHIELD BLOCKED");
                newHead = head;
            }
            else
            {
                FinishGame(false);
                return;
            }
        }

        bool willEat = newHead == food;
        bool willEatSpecial = specialKind != SpecialKind.None && newHead == specialPosition;
        bool hitObstacle = obstaclePositions.Contains(newHead);
        bool hitBoss = bossActive && newHead == bossPosition;
        bool growThisTick = hitBoss || willEat || willEatSpecial;

        if (hitBoss)
        {
            bossHealth--;
            ShowBanner("BOSS HIT " + Math.Max(0, bossHealth) + " HP");
            PlayJingle(new Note(1200, 80), new Note(1500, 80));
            if (bossHealth <= 0)
            {
                FinishGame(true);
                return;
            }
            bossPosition = FindFreePoint();
        }

        if (hitObstacle)
        {
            if (shieldActive)
            {
                shieldActive = false;
                specialKind = SpecialKind.None;
                shieldUsedThisGame = true;
                ShowBanner("SHIELD BLOCKED");
            }
            else
            {
                FinishGame(false);
                return;
            }
        }

        // Ignore the tail cell in the self-collision check, since it moves away
        // this tick unless the snake is growing.
        int bodyToCheck = (growThisTick || willEat || willEatSpecial) ? snake.Count : snake.Count - 1;
        for (int i = 0; i < bodyToCheck; i++)
        {
            if (snake[i] == newHead)
            {
                if (shieldActive)
                {
                    shieldActive = false;
                    specialKind = SpecialKind.None;
                    shieldUsedThisGame = true;
                    ShowBanner("SHIELD BLOCKED");
                    newHead = head;
                    break;
                }
                FinishGame(false);
                return;
            }
        }

        snake.Add(newHead);
        snakePositions.Add(newHead);

        if (willEatSpecial && specialKind == SpecialKind.Trap)
        {
            ApplyTrap();
            specialKind = SpecialKind.None;
            ShowBanner("TRAP! -" + TrapShrinkAmount);
            PlayJingle(new Note(220, 90), new Note(140, 140));
        }
        else if (willEatSpecial && specialKind == SpecialKind.Speed)
        {
            score += SpeedBonusPoints;
            ApplySpeedBoost();
            specialKind = SpecialKind.None;
            ShowBanner("SPEED BOOST!");
            PlayJingle(new Note(1800, 55), new Note(2200, 55), new Note(2600, 70));
            CheckLevelUp();
        }
        else if (willEatSpecial && specialKind == SpecialKind.Shield)
        {
            ApplyShield();
            specialKind = SpecialKind.None;
            PlayJingle(new Note(1000, 55), new Note(1400, 55));
        }
        else if (willEat)
        {
            score++;
            if (currentProfile != null)
                currentProfile.TotalApplesEaten++;
            PlayJingle(new Note(1000, 40));
            foodsSinceSpecial++;

            // Every few apples, level up: the snake gets faster and, every few
            // levels, the board itself grows (and the window resizes with it).
            CheckLevelUp();

            if (snake.Count >= gridWidth * gridHeight)
            {
                FinishGame(true);
                return;
            }

            PlaceFood();

            if (specialKind == SpecialKind.None && foodsSinceSpecial >= SpecialSpawnEveryFoods)
            {
                SpawnSpecial();
                foodsSinceSpecial = 0;
            }
        }
        else
        {
            if (!growThisTick)
            {
                snakePositions.Remove(snake[0]);
                snake.RemoveAt(0);
            }
        }

        if (bannerTicksLeft > 0)
            bannerTicksLeft--;

        if (speedBoostTicksLeft > 0)
        {
            speedBoostTicksLeft--;
            if (speedBoostTicksLeft == 0)
                timer.Interval = currentInterval;
        }

        if (specialKind != SpecialKind.None)
        {
            specialTicksLeft--;
            if (specialTicksLeft <= 0)
                specialKind = SpecialKind.None;
        }

        Invalidate();
    }

    private void FinishGame(bool playerWon)
    {
        timer.Stop();
        stopwatch.Stop();
        state = GameState.GameOver;
        won = playerWon;

        isNewBest = currentProfile != null && score > currentProfile.BestScore;
        if (currentProfile != null && isNewBest)
        {
            currentProfile.BestScore = score;
            currentProfile.BestTime = stopwatch.Elapsed;
        }
        if (currentProfile != null)
        {
            currentProfile.GamesPlayed++;
            currentProfile.History.Insert(0, new GameHistoryEntry(score, stopwatch.Elapsed, DateTime.Now));
            if (currentProfile.History.Count > 6)
                currentProfile.History.RemoveAt(currentProfile.History.Count - 1);

            // Update lifetime stats and unlock any achievements they now qualify for.
            if (level > currentProfile.MaxLevelReached)
                currentProfile.MaxLevelReached = level;
            if (snake.Count > currentProfile.MaxSnakeLength)
                currentProfile.MaxSnakeLength = snake.Count;
            int survivedSeconds = (int)stopwatch.Elapsed.TotalSeconds;
            if (survivedSeconds > currentProfile.LongestSurvivalSeconds)
                currentProfile.LongestSurvivalSeconds = survivedSeconds;
            if (mode == GameMode.BossFight && playerWon)
            {
                currentProfile.BossesDefeated++;
                if (!shieldUsedThisGame)
                    currentProfile.WonBossFightWithoutShield = true;
            }
            CheckAchievements();

            SaveProfiles();
            if (mode == GameMode.Solo)
                SubmitGlobalScore(currentProfile.Name, score, stopwatch.Elapsed);
        }

        if (playerWon)
        {
            PlayJingle(new Note(1200, 80), new Note(1500, 80), new Note(1800, 80), new Note(2400, 160));
        }
        else
        {
            deathFlashTicksLeft = DeathFlashTicks;
            PlayJingle(new Note(400, 90), new Note(300, 90), new Note(200, 160));
        }

        Invalidate();
    }

    // Compares the profile's lifetime stats (just updated by FinishGame) against every
    // achievement's condition, unlocking and persisting any newly-met ones. The titles
    // of whatever got unlocked this game are kept in newlyUnlockedThisGame so the
    // Game Over screen can announce them.
    private void CheckAchievements()
    {
        if (currentProfile == null)
            return;

        foreach (AchievementDef def in Achievements)
        {
            if (!currentProfile.UnlockedAchievements.Contains(def.Id) && def.IsUnlocked(currentProfile))
            {
                currentProfile.UnlockedAchievements.Add(def.Id);
                newlyUnlockedThisGame.Add(def.Title);
            }
        }

        if (newlyUnlockedThisGame.Count > 0)
            PlayJingle(new Note(1500, 60), new Note(1900, 60), new Note(2300, 90));
    }

    // Runs the duel simulation for both snakes at once (local duel and duel host only -
    // the guest never calls this, it just renders whatever the host sends it).
    private void OnDuelTimerTick()
    {
        direction = pendingDirection;
        if (mode == GameMode.AIDuel)
            UpdateAIDirection();
        direction2 = pendingDirection2;

        Point newHead1 = MoveHead(snake[snake.Count - 1], direction);
        Point newHead2 = MoveHead(snake2[snake2.Count - 1], direction2);

        bool eat1 = newHead1 == food;
        bool eat2 = !eat1 && newHead2 == food;
        bool eatSpecial1 = specialKind != SpecialKind.None && newHead1 == specialPosition;
        bool eatSpecial2 = !eatSpecial1 && specialKind != SpecialKind.None && newHead2 == specialPosition;

        bool wall1 = IsOutOfBounds(newHead1);
        bool wall2 = IsOutOfBounds(newHead2);
        bool headOn = newHead1 == newHead2;

        bool self1 = false, hit2 = false, self2 = false, hit1 = false;

        int bodyToCheck1 = (eat1 || eatSpecial1) ? snake.Count : snake.Count - 1;
        for (int i = 0; i < bodyToCheck1; i++)
            if (snake[i] == newHead1) { self1 = true; break; }

        int bodyToCheck2 = (eat2 || eatSpecial2) ? snake2.Count : snake2.Count - 1;
        for (int i = 0; i < bodyToCheck2; i++)
            if (snake2[i] == newHead2) { self2 = true; break; }

        for (int i = 0; i < snake2.Count; i++)
            if (snake2[i] == newHead1) { hit2 = true; break; }
        for (int i = 0; i < snake.Count; i++)
            if (snake[i] == newHead2) { hit1 = true; break; }

        bool dead1 = wall1 || self1 || hit2 || headOn;
        bool dead2 = wall2 || self2 || hit1 || headOn;

        if (dead1 || dead2)
        {
            DuelWinner winner = (dead1 && dead2) ? DuelWinner.Draw : (dead1 ? DuelWinner.Player2 : DuelWinner.Player1);
            FinishDuel(winner);
            return;
        }

        snake.Add(newHead1);
        snake2.Add(newHead2);

        if (eatSpecial1 && specialKind == SpecialKind.Trap)
        {
            ApplyTrapTo(snake);
            specialKind = SpecialKind.None;
            ShowBanner("P1 HIT A TRAP!");
            PlayJingle(new Note(220, 90), new Note(140, 140));
        }
        else if (eatSpecial1 && specialKind == SpecialKind.Speed)
        {
            score += SpeedBonusPoints;
            ApplySpeedBoost();
            specialKind = SpecialKind.None;
            ShowBanner("P1 SPEED BOOST!");
            PlayJingle(new Note(1800, 55), new Note(2200, 55), new Note(2600, 70));
            CheckLevelUp();
        }
        else if (eat1)
        {
            score++;
            PlayJingle(new Note(1000, 40));
            foodsSinceSpecial++;
            CheckLevelUp();
            PlaceFood();
        }
        else
        {
            snake.RemoveAt(0);
        }

        if (eatSpecial2 && specialKind == SpecialKind.Trap)
        {
            ApplyTrapTo(snake2);
            specialKind = SpecialKind.None;
            ShowBanner("P2 HIT A TRAP!");
            PlayJingle(new Note(220, 90), new Note(140, 140));
        }
        else if (eatSpecial2 && specialKind == SpecialKind.Speed)
        {
            score2 += SpeedBonusPoints;
            ApplySpeedBoost();
            specialKind = SpecialKind.None;
            ShowBanner("P2 SPEED BOOST!");
            PlayJingle(new Note(1800, 55), new Note(2200, 55), new Note(2600, 70));
            CheckLevelUp();
        }
        else if (eat2)
        {
            score2++;
            PlayJingle(new Note(900, 40));
            foodsSinceSpecial++;
            CheckLevelUp();
            PlaceFood();
        }
        else
        {
            snake2.RemoveAt(0);
        }

        if (specialKind == SpecialKind.None && foodsSinceSpecial >= SpecialSpawnEveryFoods && (eat1 || eat2))
        {
            SpawnSpecial();
            foodsSinceSpecial = 0;
        }

        if (bannerTicksLeft > 0)
            bannerTicksLeft--;

        if (speedBoostTicksLeft > 0)
        {
            speedBoostTicksLeft--;
            if (speedBoostTicksLeft == 0)
                timer.Interval = currentInterval;
        }

        if (specialKind != SpecialKind.None)
        {
            specialTicksLeft--;
            if (specialTicksLeft <= 0)
                specialKind = SpecialKind.None;
        }

        if (mode == GameMode.DuelHost)
            SendLine(BuildStateMessage());

        Invalidate();
    }

    private void FinishDuel(DuelWinner winner)
    {
        timer.Stop();
        stopwatch.Stop();
        state = GameState.GameOver;
        duelWinner = winner;
        deathFlashTicksLeft = DeathFlashTicks;

        if (mode == GameMode.DuelHost)
        {
            string code = winner == DuelWinner.Player1 ? "1" : winner == DuelWinner.Player2 ? "2" : "0";
            SendLine("GAMEOVER:" + code);
        }

        PlayDuelResultJingle(winner);
        Invalidate();
    }

    private static void PlayDuelResultJingle(DuelWinner winner)
    {
        if (winner == DuelWinner.Draw)
            PlayJingle(new Note(500, 100), new Note(500, 100), new Note(500, 160));
        else
            PlayJingle(new Note(1200, 80), new Note(1500, 80), new Note(1800, 80), new Note(2400, 160));
    }

    // ------------------------------------------------------------------
    // Profiles / scoreboard persistence
    // ------------------------------------------------------------------

    private void SelectProfile(string rawName)
    {
        string name = rawName.Trim().ToUpperInvariant();
        if (name.Length == 0)
            return;

        PlayerProfile? found = null;
        foreach (PlayerProfile p in profiles)
        {
            if (p.Name == name)
            {
                found = p;
                break;
            }
        }

        if (found == null)
        {
            found = new PlayerProfile(name);
            profiles.Add(found);
            SaveProfiles();
        }

        currentProfile = found;
    }

    private static List<PlayerProfile> LoadProfiles()
    {
        List<PlayerProfile> result = new List<PlayerProfile>();
        try
        {
            if (File.Exists(ProfilesPath))
            {
                foreach (string line in File.ReadAllLines(ProfilesPath))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 4 || parts[0].Length == 0)
                        continue;

                    int bestScore, bestTimeSeconds, gamesPlayed;
                    if (!int.TryParse(parts[1], out bestScore)) continue;
                    if (!int.TryParse(parts[2], out bestTimeSeconds)) continue;
                    if (!int.TryParse(parts[3], out gamesPlayed)) continue;

                    PlayerProfile profile = new PlayerProfile(parts[0])
                    {
                        BestScore = bestScore,
                        BestTime = TimeSpan.FromSeconds(bestTimeSeconds),
                        GamesPlayed = gamesPlayed
                    };

                    if (parts.Length >= 7)
                    {
                        int colorIndex;
                        int shapeIndex;
                        int themeIndex;
                        if (int.TryParse(parts[4], out colorIndex) && int.TryParse(parts[5], out shapeIndex) && int.TryParse(parts[6], out themeIndex))
                        {
                            profile.Appearance.ColorTheme = (SnakeColorTheme)Math.Min(Math.Max(colorIndex, 0), SnakeColorNames.Length - 1);
                            profile.Appearance.ShapeStyle = (SnakeShapeStyle)Math.Min(Math.Max(shapeIndex, 0), SnakeShapeNames.Length - 1);
                            profile.Appearance.Theme = (BoardTheme)Math.Min(Math.Max(themeIndex, 0), SnakeThemeNames.Length - 1);
                        }
                    }

                    if (parts.Length >= 8 && parts[7].Length > 0)
                    {
                        profile.History = DeserializeHistory(parts[7]);
                    }

                    if (parts.Length >= 13)
                    {
                        int totalApples, maxLevel, maxLength, longestSurvival, bossesDefeated, noShieldFlag;
                        if (int.TryParse(parts[8], out totalApples)) profile.TotalApplesEaten = totalApples;
                        if (int.TryParse(parts[9], out maxLevel)) profile.MaxLevelReached = maxLevel;
                        if (int.TryParse(parts[10], out maxLength)) profile.MaxSnakeLength = maxLength;
                        if (int.TryParse(parts[11], out longestSurvival)) profile.LongestSurvivalSeconds = longestSurvival;
                        if (int.TryParse(parts[12], out bossesDefeated)) profile.BossesDefeated = bossesDefeated;
                        if (parts.Length >= 14 && int.TryParse(parts[13], out noShieldFlag)) profile.WonBossFightWithoutShield = noShieldFlag != 0;
                    }

                    if (parts.Length >= 15 && parts[14].Length > 0)
                    {
                        foreach (string id in parts[14].Split(';'))
                            if (id.Length > 0)
                                profile.UnlockedAchievements.Add(id);
                    }

                    result.Add(profile);
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return result;
    }

    private void SaveProfiles()
    {
        try
        {
            Directory.CreateDirectory(ProfilesDirectory);
            List<string> lines = new List<string>();
            foreach (PlayerProfile p in profiles)
                lines.Add(p.Name + "|" + p.BestScore + "|" + (int)p.BestTime.TotalSeconds + "|" + p.GamesPlayed + "|" + (int)p.Appearance.ColorTheme + "|" + (int)p.Appearance.ShapeStyle + "|" + (int)p.Appearance.Theme + "|" + SerializeHistory(p.History)
                    + "|" + p.TotalApplesEaten + "|" + p.MaxLevelReached + "|" + p.MaxSnakeLength + "|" + p.LongestSurvivalSeconds + "|" + p.BossesDefeated + "|" + (p.WonBossFightWithoutShield ? 1 : 0)
                    + "|" + string.Join(";", p.UnlockedAchievements));
            File.WriteAllLines(ProfilesPath, lines);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string SerializeHistory(List<GameHistoryEntry> history)
    {
        if (history == null || history.Count == 0)
            return string.Empty;

        List<string> entries = new List<string>();
        foreach (GameHistoryEntry item in history)
            entries.Add(string.Format("{0},{1},{2}", item.Score, (int)item.Time.TotalSeconds, item.PlayedAt.ToFileTimeUtc()));

        return string.Join(";", entries);
    }

    private static List<GameHistoryEntry> DeserializeHistory(string data)
    {
        List<GameHistoryEntry> result = new List<GameHistoryEntry>();
        if (string.IsNullOrEmpty(data))
            return result;

        foreach (string entry in data.Split(';'))
        {
            string[] parts = entry.Split(',');
            if (parts.Length != 3)
                continue;

            int score;
            int seconds;
            long fileTime;
            if (!int.TryParse(parts[0], out score))
                continue;
            if (!int.TryParse(parts[1], out seconds))
                continue;
            if (!long.TryParse(parts[2], out fileTime))
                continue;

            result.Add(new GameHistoryEntry(score, TimeSpan.FromSeconds(seconds), DateTime.FromFileTimeUtc(fileTime)));
        }

        return result;
    }

    private static List<GlobalScoreEntry> LoadGlobalScores()
    {
        List<GlobalScoreEntry> result = new List<GlobalScoreEntry>();
        try
        {
            if (File.Exists(GlobalScoresPath))
            {
                foreach (string line in File.ReadAllLines(GlobalScoresPath))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 4 || parts[0].Length == 0)
                        continue;

                    int score, timeSeconds;
                    if (!int.TryParse(parts[1], out score)) continue;
                    if (!int.TryParse(parts[2], out timeSeconds)) continue;

                    DateTime submittedAt = DateTime.UtcNow;
                    if (parts.Length >= 4 && !DateTime.TryParse(parts[3], out submittedAt))
                        submittedAt = DateTime.UtcNow;

                    result.Add(new GlobalScoreEntry(parts[0].ToUpperInvariant())
                    {
                        Score = score,
                        Time = TimeSpan.FromSeconds(timeSeconds),
                        SubmittedAt = submittedAt
                    });
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return result;
    }

    private void SubmitGlobalScore(string name, int score, TimeSpan elapsed)
    {
        string normalizedName = (name ?? "").Trim().ToUpperInvariant();
        if (normalizedName.Length == 0 || score <= 0)
            return;

        GlobalScoreEntry? existing = null;
        foreach (GlobalScoreEntry entry in globalScores)
        {
            if (entry.Name == normalizedName)
            {
                existing = entry;
                break;
            }
        }

        if (existing == null)
        {
            existing = new GlobalScoreEntry(normalizedName);
            globalScores.Add(existing);
        }

        bool isBetter = score > existing.Score || (score == existing.Score && elapsed < existing.Time);
        if (isBetter)
        {
            existing.Score = score;
            existing.Time = elapsed;
            existing.SubmittedAt = DateTime.UtcNow;
        }

        globalScores.Sort(delegate (GlobalScoreEntry a, GlobalScoreEntry b)
        {
            int scoreCompare = b.Score.CompareTo(a.Score);
            if (scoreCompare != 0)
                return scoreCompare;
            return a.Time.CompareTo(b.Time);
        });

        while (globalScores.Count > 20)
            globalScores.RemoveAt(globalScores.Count - 1);

        SaveGlobalScores();
    }

    private void SaveGlobalScores()
    {
        try
        {
            Directory.CreateDirectory(GlobalScoresDirectory);
            List<string> lines = new List<string>();
            foreach (GlobalScoreEntry entry in globalScores)
                lines.Add(entry.Name + "|" + entry.Score + "|" + (int)entry.Time.TotalSeconds + "|" + entry.SubmittedAt.ToUniversalTime().ToString("u"));
            File.WriteAllLines(GlobalScoresPath, lines);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ------------------------------------------------------------------
    // Networking (duel host / guest) - host is authoritative: it runs the
    // real simulation for both snakes and streams the resulting state to
    // the guest, which only renders it and sends back its key presses.
    // ------------------------------------------------------------------

    private void HostDuel()
    {
        CloseNetworking();
        mode = GameMode.DuelHost;
        netStatusMessage = "";
        state = GameState.OnlineHostWait;
        Invalidate();

        Thread listenThread = new Thread(HostListenThreadProc);
        listenThread.IsBackground = true;
        listenThread.Start();
    }

    private void HostListenThreadProc()
    {
        try
        {
            hostListener = new TcpListener(IPAddress.Any, DuelPort);
            hostListener.Start();
            TcpClient client = hostListener.AcceptTcpClient();
            AttachConnection(client);

            if (currentProfile != null && netWriter != null && netReader != null)
            {
                netWriter.WriteLine("NAME:" + currentProfile.Name);
                string? line = netReader.ReadLine();
                string guestName = ParseName(line);

                BeginInvoke(new MethodInvoker(delegate
                {
                    player2Name = guestName.Length > 0 ? guestName : "GUEST";
                    netConnected = true;
                    state = GameState.Ready;
                    Invalidate();
                }));
            }

            HostReadLoop();
        }
        catch (Exception)
        {
            BeginInvoke(new MethodInvoker(delegate
            {
                netStatusMessage = "COULD NOT START HOSTING";
                Invalidate();
            }));
        }
    }

    private void HostReadLoop()
    {
        try
        {
            string? line;
            while (netReader != null && (line = netReader.ReadLine()) != null)
            {
                string msg = line;
                BeginInvoke(new MethodInvoker(delegate { HandleHostMessage(msg); }));
            }
        }
        catch (Exception) { }

        try { BeginInvoke(new MethodInvoker(OnOpponentDisconnected)); }
        catch (Exception) { }
    }

    private void HandleHostMessage(string line)
    {
        if (line.StartsWith("DIR:"))
        {
            Direction d;
            if (Enum.TryParse(line.Substring(4), out d) && d != Opposite(direction2))
                pendingDirection2 = d;
        }
        else if (line == "PAUSEREQ")
        {
            if (state == GameState.Playing)
            {
                state = GameState.Paused;
                timer.Stop();
                stopwatch.Stop();
                SendLine("PAUSED");
                Invalidate();
            }
            else if (state == GameState.Paused)
            {
                state = GameState.Playing;
                timer.Start();
                stopwatch.Start();
                SendLine("RESUMED");
                Invalidate();
            }
        }
    }

    private void JoinDuel(string ip)
    {
        CloseNetworking();
        mode = GameMode.DuelGuest;
        netStatusMessage = "";
        state = GameState.OnlineConnecting;
        Invalidate();

        Thread connectThread = new Thread(delegate () { JoinConnectThreadProc(ip); });
        connectThread.IsBackground = true;
        connectThread.Start();
    }

    private void JoinConnectThreadProc(string ip)
    {
        try
        {
            TcpClient client = new TcpClient();
            duelClient = client; // let Escape (CloseNetworking) abort a pending connect attempt
            client.Connect(ip, DuelPort);
            AttachConnection(client);

            if (currentProfile != null && netWriter != null && netReader != null)
            {
                netWriter.WriteLine("NAME:" + currentProfile.Name);
                string? line = netReader.ReadLine();
                string hostName = ParseName(line);

                BeginInvoke(new MethodInvoker(delegate
                {
                    player2Name = hostName.Length > 0 ? hostName : "HOST";
                    netConnected = true;
                    state = GameState.Ready;
                    Invalidate();
                }));

                GuestReadLoop();
            }
        }
        catch (Exception)
        {
            BeginInvoke(new MethodInvoker(delegate
            {
                mode = GameMode.DuelGuest;
                state = GameState.OnlineJoinEntry;
                netStatusMessage = "COULD NOT CONNECT";
                Invalidate();
            }));
        }
    }

    private void GuestReadLoop()
    {
        try
        {
            string? line;
            while (netReader != null && (line = netReader.ReadLine()) != null)
            {
                string msg = line;
                BeginInvoke(new MethodInvoker(delegate { HandleGuestMessage(msg); }));
            }
        }
        catch (Exception) { }

        try { BeginInvoke(new MethodInvoker(OnOpponentDisconnected)); }
        catch (Exception) { }
    }

    private void HandleGuestMessage(string line)
    {
        if (line.StartsWith("STATE:"))
        {
            ApplyState(line.Substring(6));
            Invalidate();
        }
        else if (line.StartsWith("GAMEOVER:"))
        {
            ApplyGameOver(line.Substring(9));
        }
        else if (line == "PAUSED")
        {
            state = GameState.Paused;
            Invalidate();
        }
        else if (line == "RESUMED")
        {
            state = GameState.Playing;
            Invalidate();
        }
        else if (line == "RESTART")
        {
            state = GameState.Playing;
            won = false;
            duelWinner = DuelWinner.None;
            stopwatch.Reset();
            stopwatch.Start();
            Invalidate();
        }
    }

    private void OnOpponentDisconnected()
    {
        if (mode != GameMode.DuelHost && mode != GameMode.DuelGuest)
            return;

        netConnected = false;
        timer.Stop();
        stopwatch.Stop();
        state = GameState.GameOver;
        duelWinner = DuelWinner.None;
        netStatusMessage = "OPPONENT DISCONNECTED";
        Invalidate();
    }

    private void AttachConnection(TcpClient client)
    {
        duelClient = client;
        NetworkStream stream = client.GetStream();
        netReader = new StreamReader(stream);
        netWriter = new StreamWriter(stream);
        netWriter.AutoFlush = true;
    }

    private void SendLine(string line)
    {
        if (!netConnected || netWriter == null)
            return;
        try { netWriter.WriteLine(line); }
        catch (Exception) { OnOpponentDisconnected(); }
    }

    private void CloseNetworking()
    {
        netConnected = false;
        try { if (hostListener != null) hostListener.Stop(); } catch (Exception) { }
        try { if (duelClient != null) duelClient.Close(); } catch (Exception) { }
        hostListener = null;
        duelClient = null;
        netReader = null;
        netWriter = null;
    }

    private static string ParseName(string? line)
    {
        if (!string.IsNullOrEmpty(line) && line.StartsWith("NAME:"))
            return Truncate(line.Substring(5).Trim(), MaxNameLength);
        return "";
    }

    private static string GetLocalIpHint()
    {
        try
        {
            IPHostEntry entry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress addr in entry.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    return addr.ToString();
            }
        }
        catch (Exception) { }
        return "UNKNOWN";
    }

    // Serializes the authoritative game state the host sends to the guest each tick.
    private string BuildStateMessage()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("STATE:");
        sb.Append(score).Append('|');
        sb.Append(score2).Append('|');
        sb.Append(level).Append('|');
        sb.Append(food.X).Append(',').Append(food.Y).Append('|');
        sb.Append((int)specialKind).Append(',').Append(specialPosition.X).Append(',').Append(specialPosition.Y).Append('|');
        sb.Append((int)direction).Append('|');
        sb.Append((int)direction2).Append('|');
        AppendBody(sb, snake);
        sb.Append('|');
        AppendBody(sb, snake2);
        return sb.ToString();
    }

    private static void AppendBody(StringBuilder sb, List<Point> body)
    {
        for (int i = 0; i < body.Count; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(body[i].X).Append(',').Append(body[i].Y);
        }
    }

    private void ApplyState(string payload)
    {
        string[] parts = payload.Split('|');
        if (parts.Length < 8)
            return;

        score = ParseInt(parts[0]);
        score2 = ParseInt(parts[1]);
        level = ParseInt(parts[2]);

        string[] foodParts = parts[3].Split(',');
        if (foodParts.Length == 2)
            food = new Point(ParseInt(foodParts[0]), ParseInt(foodParts[1]));

        string[] specialParts = parts[4].Split(',');
        if (specialParts.Length == 3)
        {
            specialKind = (SpecialKind)ParseInt(specialParts[0]);
            specialPosition = new Point(ParseInt(specialParts[1]), ParseInt(specialParts[2]));
        }

        direction = (Direction)ParseInt(parts[5]);
        direction2 = (Direction)ParseInt(parts[6]);

        ParseBody(parts[7], snake);
        if (parts.Length > 8)
            ParseBody(parts[8], snake2);
    }

    private void ApplyGameOver(string code)
    {
        timer.Stop();
        stopwatch.Stop();
        state = GameState.GameOver;
        deathFlashTicksLeft = DeathFlashTicks;

        if (code == "1") duelWinner = DuelWinner.Player1;
        else if (code == "2") duelWinner = DuelWinner.Player2;
        else duelWinner = DuelWinner.Draw;

        PlayDuelResultJingle(duelWinner);
        Invalidate();
    }

    private static int ParseInt(string s)
    {
        int v;
        int.TryParse(s, out v);
        return v;
    }

    private static void ParseBody(string s, List<Point> body)
    {
        body.Clear();
        if (s.Length == 0)
            return;

        string[] points = s.Split(';');
        foreach (string p in points)
        {
            string[] xy = p.Split(',');
            if (xy.Length == 2)
                body.Add(new Point(ParseInt(xy[0]), ParseInt(xy[1])));
        }
    }

    // ------------------------------------------------------------------
    // Rendering
    // ------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        DrawTopBar(g);
        DrawPlayfield(g);
        DrawDeathFlash(g);
        DrawBanner(g);

        switch (state)
        {
            case GameState.NameEntry:
                DrawNameEntry(g);
                break;
            case GameState.CustomizeSnake:
                DrawCustomizeSnake(g);
                break;
            case GameState.ProfileHistory:
                DrawProfileHistory(g);
                break;
            case GameState.Achievements:
                DrawAchievements(g);
                break;
            case GameState.ModeSelect:
                DrawModeSelect(g);
                break;
            case GameState.OnlineHostWait:
                DrawHostWait(g);
                break;
            case GameState.OnlineJoinEntry:
                DrawJoinEntry(g);
                break;
            case GameState.OnlineConnecting:
                DrawOverlay(g, "CONNECTING" + Ellipsis(), "ESC: CANCEL");
                break;
            case GameState.Ready:
                DrawReadyOverlay(g);
                break;
            case GameState.Paused:
                DrawOverlay(g, "PAUSED", mode == GameMode.DuelGuest ? "P: REQUEST RESUME" : "P: RESUME", "M: MENU");
                break;
            case GameState.GameOver:
                DrawGameOverOverlay(g);
                break;
            case GameState.Scoreboard:
                DrawScoreboard(g);
                break;
        }
    }

    private static string Ellipsis()
    {
        return new string('.', (Environment.TickCount / 400) % 4);
    }

    private void DrawReadyOverlay(Graphics g)
    {
        if (mode == GameMode.Solo && currentProfile == null)
            return;

        if (mode == GameMode.Solo)
        {
            string bestText = "BEST " + currentProfile!.BestScore.ToString("000") + "   GAMES " + currentProfile!.GamesPlayed;
            string timeLabel = currentProfile!.BestTime.TotalSeconds > 0 ? "TIME " + FormatTime(currentProfile!.BestTime) : "TIME --:--";
            string themeLabel = "THEME " + SnakeThemeNames[(int)currentProfile!.Appearance.Theme];
            DrawOverlay(g, "SNAKE", "PLAYER " + currentProfile!.Name,
                bestText + "   " + timeLabel,
                themeLabel,
                "SPACE: PLAY   L: SCORES   N: NAME   C: STYLE",
                "H: HISTORY   T: TROPHIES",
                "LEVELS SPEED UP + GROW THE BOARD");
        }
        else if (mode == GameMode.DuelLocal)
        {
            DrawOverlay(g, "LOCAL DUEL", "P1: ARROW KEYS      P2: WASD",
                "SPACE: PLAY   M: MENU");
        }
        else if (mode == GameMode.AIDuel)
        {
            DrawOverlay(g, "AI DUEL", "VS COMPUTER", "SPACE: PLAY   M: MENU");
        }
        else if (mode == GameMode.BossFight)
        {
            DrawOverlay(g, "BOSS FIGHT", "DEFEAT THE BOSS", "SPACE: PLAY   M: MENU",
                "AVOID OBSTACLES   COLLECT BOOSTS");
        }
        else if (mode == GameMode.Procedural)
        {
            DrawOverlay(g, "PROCEDURAL MODE", "OBSTACLES GROW EACH LEVEL", "SPACE: PLAY   M: MENU",
                "SURVIVE AS LONG AS POSSIBLE");
        }
        else if (mode == GameMode.Zen)
        {
            DrawOverlay(g, "ZEN MODE", "WALLS WRAP AROUND", "SPACE: PLAY   M: MENU",
                "ONLY YOUR OWN TAIL CAN STOP YOU");
        }
        else if (mode == GameMode.DuelGuest)
        {
            DrawOverlay(g, "ONLINE DUEL", "VS " + player2Name, "SPACE: READY   M: MENU");
        }
        else
        {
            DrawOverlay(g, "ONLINE DUEL", "VS " + player2Name, "SPACE: PLAY   M: MENU");
        }
    }

    private void DrawModeSelect(Graphics g)
    {
        DrawOverlay(g, "PLAY MODE",
            "1: SOLO",
            "2: LOCAL DUEL (SAME PC)",
            "3: AI DUEL",
            "4: BOSS FIGHT",
            "5: PROCEDURAL MODE",
            "6: ZEN MODE",
            "7: HOST ONLINE DUEL",
            "8: JOIN ONLINE DUEL",
            "ESC: BACK");
    }

    private void DrawHostWait(Graphics g)
    {
        DrawOverlay(g, "HOSTING",
            "YOUR IP: " + GetLocalIpHint(),
            "PORT: " + DuelPort,
            "WAITING FOR OPPONENT" + Ellipsis(),
            "ESC: CANCEL");
    }

    private void DrawJoinEntry(Graphics g)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);

        using (Brush overlayBrush = new SolidBrush(BackgroundColor))
            g.FillRectangle(overlayBrush, field);

        using (Font titleFont = new Font("Consolas", 15f, FontStyle.Bold))
        using (Font inputFont = new Font("Consolas", 13f, FontStyle.Bold))
        using (Font hintFont = new Font("Consolas", 9f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            string title = "HOST'S IP ADDRESS";
            SizeF titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, textBrush, field.Left + (field.Width - titleSize.Width) / 2, field.Top + 80);

            bool showCursor = (Environment.TickCount / 400) % 2 == 0;
            string shown = joinIpInput + (showCursor ? "_" : " ");
            SizeF inputSize = g.MeasureString(shown, inputFont);
            g.DrawString(shown, inputFont, textBrush, field.Left + (field.Width - inputSize.Width) / 2, field.Top + 120);

            string hint = "ENTER: CONNECT   ESC: BACK";
            SizeF hintSize = g.MeasureString(hint, hintFont);
            g.DrawString(hint, hintFont, textBrush, field.Left + (field.Width - hintSize.Width) / 2, field.Top + 165);

            if (netStatusMessage.Length > 0)
            {
                SizeF errSize = g.MeasureString(netStatusMessage, hintFont);
                g.DrawString(netStatusMessage, hintFont, textBrush, field.Left + (field.Width - errSize.Width) / 2, field.Top + 190);
            }
        }
    }

    private void DrawTopBar(Graphics g)
    {
        using (Brush barBrush = new SolidBrush(InkColor))
            g.FillRectangle(barBrush, 0, 0, ClientSize.Width, TopBarHeight);

        if (state == GameState.NameEntry || state == GameState.ModeSelect || state == GameState.OnlineHostWait ||
            state == GameState.OnlineJoinEntry || state == GameState.OnlineConnecting || state == GameState.CustomizeSnake)
            return;

        using (Font hudFont = new Font("Consolas", 9.5f, FontStyle.Bold))
        using (Brush hudBrush = new SolidBrush(BackgroundColor))
        {
            if (mode == GameMode.Solo || mode == GameMode.BossFight || mode == GameMode.Procedural || mode == GameMode.Zen)
            {
                string scoreText = "SCORE " + score.ToString("000");
                string levelText = "LVL " + level;
                string timeText = FormatTime(stopwatch.Elapsed);
                string bestText = "BEST " + (currentProfile != null ? currentProfile.BestScore.ToString("000") : "000");

                g.DrawString(scoreText, hudFont, hudBrush, 6, 8);
                SizeF scoreSize = g.MeasureString(scoreText, hudFont);
                g.DrawString(levelText, hudFont, hudBrush, 6 + scoreSize.Width + 12, 8);

                SizeF timeSize = g.MeasureString(timeText, hudFont);
                g.DrawString(timeText, hudFont, hudBrush, (ClientSize.Width - timeSize.Width) / 2, 8);

                SizeF bestSize = g.MeasureString(bestText, hudFont);
                g.DrawString(bestText, hudFont, hudBrush, ClientSize.Width - bestSize.Width - 6, 8);

                if (mode == GameMode.BossFight && bossActive)
                {
                    string bossText = "BOSS " + bossHealth + " HP";
                    SizeF bossSize = g.MeasureString(bossText, hudFont);
                    g.DrawString(bossText, hudFont, hudBrush, (ClientSize.Width - bossSize.Width) / 2, 8 + timeSize.Height + 1);
                }

                if (shieldActive)
                {
                    string shieldText = "SHIELD";
                    SizeF shieldSize = g.MeasureString(shieldText, hudFont);
                    g.DrawString(shieldText, hudFont, hudBrush, ClientSize.Width - bestSize.Width - shieldSize.Width - 12, 8);
                }
            }
            else
            {
                string p1Text = "P1 " + score.ToString("000");
                string p2Text = "P2 " + score2.ToString("000");
                string midText = "LVL " + level + "  " + FormatTime(stopwatch.Elapsed);

                g.DrawString(p1Text, hudFont, hudBrush, 6, 8);

                SizeF p2Size = g.MeasureString(p2Text, hudFont);
                g.DrawString(p2Text, hudFont, hudBrush, ClientSize.Width - p2Size.Width - 6, 8);

                SizeF midSize = g.MeasureString(midText, hudFont);
                g.DrawString(midText, hudFont, hudBrush, (ClientSize.Width - midSize.Width) / 2, 8);
            }
        }
    }

    // Small non-blocking banner shown for a moment after leveling up or picking up a special item
    private void DrawBanner(Graphics g)
    {
        if (bannerTicksLeft <= 0 || state != GameState.Playing)
            return;

        using (Font bannerFont = new Font("Consolas", 12f, FontStyle.Bold))
        using (Brush bg = new SolidBrush(Color.FromArgb(225, InkColor)))
        using (Brush fg = new SolidBrush(BackgroundColor))
        {
            SizeF textSize = g.MeasureString(bannerText, bannerFont);
            float bannerWidth = textSize.Width + 24;
            float bannerHeight = textSize.Height + 8;
            float x = (ClientSize.Width - bannerWidth) / 2f;
            float y = TopBarHeight + 8;

            g.FillRectangle(bg, x, y, bannerWidth, bannerHeight);
            g.DrawString(bannerText, bannerFont, fg, x + 12, y + 4);
        }
    }

    private void DrawPlayfield(Graphics g)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);
        using (Brush bg = new SolidBrush(GetBoardBackgroundColor(currentProfile != null ? currentProfile.Appearance.Theme : BoardTheme.Classic)))
            g.FillRectangle(bg, field);

        using (Pen gridPen = new Pen(GetBoardGridLineColor(currentProfile != null ? currentProfile.Appearance.Theme : BoardTheme.Classic)))
        {
            for (int x = 0; x <= gridWidth; x++)
                g.DrawLine(gridPen, x * CellSize, TopBarHeight, x * CellSize, TopBarHeight + gridHeight * CellSize);
            for (int y = 0; y <= gridHeight; y++)
                g.DrawLine(gridPen, 0, TopBarHeight + y * CellSize, gridWidth * CellSize, TopBarHeight + y * CellSize);
        }

        using (Brush inkBrush = new SolidBrush(InkColor))
        {
            DrawFood(g, inkBrush);
            DrawSpecialItem(g, inkBrush);
            DrawObstacles(g, inkBrush);
            if (mode == GameMode.BossFight && bossActive)
                DrawBoss(g);
            DrawSnakeBody(g, inkBrush, snake, direction, true, GetSnakeAppearance(true));
            if (mode != GameMode.Solo)
                DrawSnakeBody(g, inkBrush, snake2, direction2, false, GetSnakeAppearance(false));
        }

        using (Pen borderPen = new Pen(InkColor, 3))
            g.DrawRectangle(borderPen, 1, TopBarHeight + 1, gridWidth * CellSize - 2, gridHeight * CellSize - 2);
    }

    private void DrawProfileHistory(Graphics g)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);

        using (Brush overlayBrush = new SolidBrush(BackgroundColor))
            g.FillRectangle(overlayBrush, field);

        using (Font titleFont = new Font("Consolas", 14f, FontStyle.Bold))
        using (Font rowFont = new Font("Consolas", 9f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            string title = "PROFILE HISTORY";
            SizeF titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, textBrush, field.Left + (field.Width - titleSize.Width) / 2, field.Top + 12);

            float y = field.Top + 46;
            if (currentProfile == null || currentProfile.History.Count == 0)
            {
                string empty = "NO GAMES PLAYED YET";
                SizeF emptySize = g.MeasureString(empty, rowFont);
                g.DrawString(empty, rowFont, textBrush, field.Left + (field.Width - emptySize.Width) / 2, y);
            }
            else
            {
                foreach (GameHistoryEntry entry in currentProfile.History)
                {
                    string line = string.Format("{0:MM/dd HH:mm}  {1,3}  {2}", entry.PlayedAt, entry.Score, FormatTime(entry.Time));
                    g.DrawString(line, rowFont, textBrush, field.Left + 18, y);
                    y += 18;
                }
            }

            string hint = "SPACE: BACK";
            SizeF hintSize = g.MeasureString(hint, rowFont);
            g.DrawString(hint, rowFont, textBrush, field.Left + (field.Width - hintSize.Width) / 2, field.Bottom - 22);
        }
    }

    private void DrawAchievements(Graphics g)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);

        using (Brush overlayBrush = new SolidBrush(BackgroundColor))
            g.FillRectangle(overlayBrush, field);

        using (Font titleFont = new Font("Consolas", 14f, FontStyle.Bold))
        using (Font rowFont = new Font("Consolas", 8.5f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            string title = "TROPHIES";
            SizeF titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, textBrush, field.Left + (field.Width - titleSize.Width) / 2, field.Top + 10);

            int unlockedCount = 0;
            if (currentProfile != null)
            {
                foreach (AchievementDef def in Achievements)
                    if (currentProfile.UnlockedAchievements.Contains(def.Id))
                        unlockedCount++;
            }
            string progress = unlockedCount + " / " + Achievements.Length;
            SizeF progressSize = g.MeasureString(progress, rowFont);
            g.DrawString(progress, rowFont, textBrush, field.Left + (field.Width - progressSize.Width) / 2, field.Top + 32);

            float y = field.Top + 56;
            foreach (AchievementDef def in Achievements)
            {
                bool unlocked = currentProfile != null && currentProfile.UnlockedAchievements.Contains(def.Id);
                string mark = unlocked ? "[X]" : "[ ]";
                string line = mark + " " + def.Title + " - " + def.Description;
                using (Brush rowBrush = new SolidBrush(unlocked ? InkColor : Color.FromArgb(120, InkColor)))
                    g.DrawString(line, rowFont, rowBrush, field.Left + 14, y);
                y += 19;
            }

            string hint = "SPACE: BACK";
            SizeF hintSize = g.MeasureString(hint, rowFont);
            g.DrawString(hint, rowFont, textBrush, field.Left + (field.Width - hintSize.Width) / 2, field.Bottom - 20);
        }
    }

    // Draws a snake as smooth, slightly banded rounded segments, tapered at the tail,
    // with a distinct head that has eyes (and an occasional tongue flick) facing the
    // direction of travel. Player 1 is drawn solid; player 2 is drawn as an outline
    // so two snakes stay easy to tell apart on the monochrome LCD palette.
    private void DrawSnakeBody(Graphics g, Brush inkBrush, List<Point> body, Direction dir, bool filled, SnakeAppearance appearance)
    {
        DrawSnakeBody(g, inkBrush, body, dir, filled, appearance, CellSize, TopBarHeight);
    }

    private void DrawSnakeBody(Graphics g, Brush inkBrush, List<Point> body, Direction dir, bool filled, SnakeAppearance appearance, int cellSize, int topOffset)
    {
        if (body.Count == 0)
            return;

        Color bodyColor = GetSnakeBodyColor(appearance);
        Color accentColor = GetSnakeAccentColor(appearance);

        using (Brush bodyBrush = new SolidBrush(bodyColor))
        using (Brush accentBrush = new SolidBrush(accentColor))
        using (Pen outlinePen = new Pen(InkColor, 1.6f))
        {
            for (int i = 0; i < body.Count - 1; i++)
            {
                Point p = body[i];
                bool isTail = i == 0;
                float inset = isTail ? 5f : 2f;
                if (appearance.ShapeStyle == SnakeShapeStyle.Slim)
                    inset += 1.5f;
                else if (appearance.ShapeStyle == SnakeShapeStyle.Block)
                    inset -= 0.5f;

                RectangleF r = new RectangleF(
                    p.X * cellSize + inset,
                    topOffset + p.Y * cellSize + inset,
                    cellSize - inset * 2,
                    cellSize - inset * 2);

                if (filled)
                {
                    Brush segmentBrush = (i % 2 == 0) ? bodyBrush : accentBrush;
                    DrawSegmentShape(g, segmentBrush, r, appearance.ShapeStyle, 5f, true);
                }
                else
                {
                    DrawSegmentShape(g, outlinePen, r, appearance.ShapeStyle, 5f, false);
                }
            }
        }

        DrawSnakeHead(g, inkBrush, body, dir, filled, appearance, cellSize, topOffset);
    }

    private void DrawSnakeHead(Graphics g, Brush inkBrush, List<Point> body, Direction dir, bool filled, SnakeAppearance appearance, int cellSize, int topOffset)
    {
        Point head = body[body.Count - 1];
        RectangleF r = new RectangleF(
            head.X * cellSize + 1,
            topOffset + head.Y * cellSize + 1,
            cellSize - 2,
            cellSize - 2);

        Color bodyColor = GetSnakeBodyColor(appearance);
        using (Brush headBrush = new SolidBrush(bodyColor))
        {
            if (filled)
            {
                DrawSegmentShape(g, headBrush, r, appearance.ShapeStyle, 6f, true);
            }
            else
            {
                using (Pen headPen = new Pen(InkColor, 1.8f))
                    DrawSegmentShape(g, headPen, r, appearance.ShapeStyle, 6f, false);
            }
        }

        DrawEyes(g, r, dir, filled);
        DrawTongue(g, r, dir);
    }

    private void DrawSegmentShape(Graphics g, Brush brush, RectangleF r, SnakeShapeStyle style, float radius, bool filled)
    {
        switch (style)
        {
            case SnakeShapeStyle.Block:
                if (filled)
                    g.FillRectangle(brush, r);
                else
                    g.DrawRectangle(new Pen(brush), r.X, r.Y, r.Width, r.Height);
                break;
            case SnakeShapeStyle.Slim:
                RectangleF slimRect = new RectangleF(r.X + 3f, r.Y + 4f, r.Width - 6f, r.Height - 8f);
                if (filled)
                    g.FillRectangle(brush, slimRect);
                else
                    g.DrawRectangle(new Pen(brush), slimRect.X, slimRect.Y, slimRect.Width, slimRect.Height);
                break;
            case SnakeShapeStyle.Spiked:
                PointF[] points = new[]
                {
                    new PointF(r.X + r.Width / 2f, r.Y + 1f),
                    new PointF(r.Right - 2f, r.Y + r.Height / 2f),
                    new PointF(r.X + r.Width / 2f, r.Bottom - 1f),
                    new PointF(r.X + 2f, r.Y + r.Height / 2f)
                };
                if (filled)
                    g.FillPolygon(brush, points);
                else
                    g.DrawPolygon(new Pen(brush), points);
                break;
            default:
                if (filled)
                    FillRoundedRect(g, brush, r, radius);
                else
                    DrawRoundedRect(g, new Pen(brush), r, radius);
                break;
        }
    }

    private void DrawSegmentShape(Graphics g, Pen pen, RectangleF r, SnakeShapeStyle style, float radius, bool filled)
    {
        if (filled)
        {
            DrawSegmentShape(g, (Brush)new SolidBrush(pen.Color), r, style, radius, true);
            return;
        }

        switch (style)
        {
            case SnakeShapeStyle.Block:
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                break;
            case SnakeShapeStyle.Slim:
                RectangleF slimRect = new RectangleF(r.X + 3f, r.Y + 4f, r.Width - 6f, r.Height - 8f);
                g.DrawRectangle(pen, slimRect.X, slimRect.Y, slimRect.Width, slimRect.Height);
                break;
            case SnakeShapeStyle.Spiked:
                PointF[] points = new[]
                {
                    new PointF(r.X + r.Width / 2f, r.Y + 1f),
                    new PointF(r.Right - 2f, r.Y + r.Height / 2f),
                    new PointF(r.X + r.Width / 2f, r.Bottom - 1f),
                    new PointF(r.X + 2f, r.Y + r.Height / 2f)
                };
                g.DrawPolygon(pen, points);
                break;
            default:
                DrawRoundedRect(g, pen, r, radius);
                break;
        }
    }

    private static Color GetSnakeBodyColor(SnakeAppearance appearance)
    {
        switch (appearance.ColorTheme)
        {
            case SnakeColorTheme.Neon: return Color.FromArgb(0, 210, 120);
            case SnakeColorTheme.Coral: return Color.FromArgb(220, 90, 80);
            case SnakeColorTheme.Cyan: return Color.FromArgb(60, 170, 220);
            case SnakeColorTheme.Violet: return Color.FromArgb(160, 90, 220);
            default: return InkColor;
        }
    }

    private static Color GetSnakeAccentColor(SnakeAppearance appearance)
    {
        switch (appearance.ColorTheme)
        {
            case SnakeColorTheme.Neon: return Color.FromArgb(255, 255, 120);
            case SnakeColorTheme.Coral: return Color.FromArgb(255, 190, 120);
            case SnakeColorTheme.Cyan: return Color.FromArgb(120, 240, 255);
            case SnakeColorTheme.Violet: return Color.FromArgb(220, 160, 255);
            default: return Color.FromArgb(95, 110, 80);
        }
    }

    private static Color GetBoardBackgroundColor(BoardTheme theme)
    {
        switch (theme)
        {
            case BoardTheme.Jungle: return Color.FromArgb(180, 210, 160);
            case BoardTheme.City: return Color.FromArgb(220, 220, 210);
            case BoardTheme.Space: return Color.FromArgb(18, 26, 65);
            default: return BackgroundColor;
        }
    }

    private static Color GetBoardGridLineColor(BoardTheme theme)
    {
        switch (theme)
        {
            case BoardTheme.Jungle: return Color.FromArgb(155, 190, 120);
            case BoardTheme.City: return Color.FromArgb(190, 190, 180);
            case BoardTheme.Space: return Color.FromArgb(75, 95, 145);
            default: return GridLineColor;
        }
    }

    private static Color GetThemeAccentColor(BoardTheme theme)
    {
        switch (theme)
        {
            case BoardTheme.Jungle: return Color.FromArgb(95, 145, 85);
            case BoardTheme.City: return Color.FromArgb(140, 140, 160);
            case BoardTheme.Space: return Color.FromArgb(150, 190, 255);
            default: return Color.FromArgb(110, 130, 95);
        }
    }

    private SnakeAppearance GetSnakeAppearance(bool isPlayer1)
    {
        if (isPlayer1)
            return currentProfile != null ? currentProfile.Appearance : localAppearance;

        SnakeAppearance alt = new SnakeAppearance();
        alt.ColorTheme = SnakeColorTheme.Cyan;
        alt.ShapeStyle = SnakeShapeStyle.Block;
        return alt;
    }

    private void DrawEyes(Graphics g, RectangleF r, Direction dir, bool filled)
    {
        float eyeR = 1.6f;
        float inset = r.Width * 0.24f;
        PointF eye1, eye2;

        switch (dir)
        {
            case Direction.Up:
                eye1 = new PointF(r.X + inset, r.Y + inset);
                eye2 = new PointF(r.Right - inset, r.Y + inset);
                break;
            case Direction.Down:
                eye1 = new PointF(r.X + inset, r.Bottom - inset);
                eye2 = new PointF(r.Right - inset, r.Bottom - inset);
                break;
            case Direction.Left:
                eye1 = new PointF(r.X + inset, r.Y + inset);
                eye2 = new PointF(r.X + inset, r.Bottom - inset);
                break;
            default: // Right
                eye1 = new PointF(r.Right - inset, r.Y + inset);
                eye2 = new PointF(r.Right - inset, r.Bottom - inset);
                break;
        }

        Color eyeColor = filled ? BackgroundColor : InkColor;
        using (Brush eyeBrush = new SolidBrush(eyeColor))
        {
            g.FillEllipse(eyeBrush, eye1.X - eyeR, eye1.Y - eyeR, eyeR * 2, eyeR * 2);
            g.FillEllipse(eyeBrush, eye2.X - eyeR, eye2.Y - eyeR, eyeR * 2, eyeR * 2);
        }
    }

    private void DrawTongue(Graphics g, RectangleF r, Direction dir)
    {
        if (state != GameState.Playing)
            return;
        if ((Environment.TickCount / 260) % 3 != 0)
            return; // flicks briefly, periodically

        float cx = r.X + r.Width / 2f;
        float cy = r.Y + r.Height / 2f;
        float dx = 0, dy = 0;
        switch (dir)
        {
            case Direction.Up: dy = -1; break;
            case Direction.Down: dy = 1; break;
            case Direction.Left: dx = -1; break;
            case Direction.Right: dx = 1; break;
        }

        float startX = cx + dx * r.Width / 2f;
        float startY = cy + dy * r.Height / 2f;

        using (Pen tonguePen = new Pen(BackgroundColor, 1.3f))
            g.DrawLine(tonguePen, startX, startY, startX + dx * 6f, startY + dy * 6f);
    }

    // The food is drawn as a small apple: a pulsing round body, a stem and a leaf.
    private void DrawFood(Graphics g, Brush inkBrush)
    {
        double t = Environment.TickCount / 180.0;
        float pulse = 1f + 0.10f * (float)Math.Sin(t);

        float cx = food.X * CellSize + CellSize / 2f;
        float cyBase = TopBarHeight + food.Y * CellSize + CellSize / 2f;
        float radius = (CellSize / 2f - 4f) * pulse;

        using (Pen outline = new Pen(BackgroundColor, 1.4f))
        {
            RectangleF body = new RectangleF(cx - radius, cyBase - radius + 2, radius * 2, radius * 2);
            g.FillEllipse(inkBrush, body);

            RectangleF leaf = new RectangleF(cx + 1, cyBase - radius - 1, 6, 4);
            g.FillEllipse(inkBrush, leaf);
            g.DrawEllipse(outline, leaf);

            g.DrawLine(outline, cx, cyBase - radius + 1, cx, cyBase - radius - 5);
        }
    }

    // A brief pulsing flash over the playfield right after a fatal collision
    private void DrawDeathFlash(Graphics g)
    {
        if (deathFlashTicksLeft <= 0)
            return;
        if (deathFlashTicksLeft % 4 >= 2)
            return;

        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);
        using (Brush flashBrush = new SolidBrush(Color.FromArgb(120, InkColor)))
            g.FillRectangle(flashBrush, field);
    }

    private static void FillRoundedRect(Graphics g, Brush brush, RectangleF r, float radius)
    {
        using (GraphicsPath path = RoundedRectPath(r, radius))
            g.FillPath(brush, path);
    }

    private static void DrawRoundedRect(Graphics g, Pen pen, RectangleF r, float radius)
    {
        using (GraphicsPath path = RoundedRectPath(r, radius))
            g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectPath(RectangleF r, float radius)
    {
        float d = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Bonus (speed fruit) is a diamond; malus (trap) is a boxed cross - shape-coded so it
    // still reads clearly on the monochrome LCD palette. Both flash right before they expire.
    private void DrawSpecialItem(Graphics g, Brush inkBrush)
    {
        if (specialKind == SpecialKind.None)
            return;

        bool nearExpiry = specialTicksLeft <= SpecialLifetimeTicks / 3;
        if (nearExpiry && (specialTicksLeft / 3) % 2 == 0)
            return;

        double t = Environment.TickCount / 150.0;
        float pulse = 1f + 0.12f * (float)Math.Sin(t);

        float cx = specialPosition.X * CellSize + CellSize / 2f;
        float cy = TopBarHeight + specialPosition.Y * CellSize + CellSize / 2f;
        float half = (CellSize / 2f - 3f) * pulse;

        if (specialKind == SpecialKind.Speed)
        {
            PointF[] diamond =
            {
                new PointF(cx, cy - half),
                new PointF(cx + half, cy),
                new PointF(cx, cy + half),
                new PointF(cx - half, cy)
            };
            g.FillPolygon(inkBrush, diamond);

            // Motion lines trailing behind the diamond sell the "speed" theme
            using (Pen speedPen = new Pen(InkColor, 1.4f))
            {
                for (int i = 1; i <= 3; i++)
                {
                    float lx = cx - half - i * 4f;
                    g.DrawLine(speedPen, lx, cy - 2, lx + 3, cy - 2);
                    g.DrawLine(speedPen, lx, cy + 2, lx + 3, cy + 2);
                }
            }
        }
        else
        {
            RectangleF box = new RectangleF(cx - half, cy - half, half * 2, half * 2);
            g.FillRectangle(inkBrush, box);

            using (Pen crossPen = new Pen(BackgroundColor, 2f))
            {
                g.DrawLine(crossPen, box.Left + 2, box.Top + 2, box.Right - 2, box.Bottom - 2);
                g.DrawLine(crossPen, box.Right - 2, box.Top + 2, box.Left + 2, box.Bottom - 2);
            }

            // Small hazard spikes at the corners
            float s = 3f;
            g.FillPolygon(inkBrush, new[] { new PointF(box.Left, box.Top), new PointF(box.Left - s, box.Top - s), new PointF(box.Left + s, box.Top) });
            g.FillPolygon(inkBrush, new[] { new PointF(box.Right, box.Top), new PointF(box.Right + s, box.Top - s), new PointF(box.Right - s, box.Top) });
            g.FillPolygon(inkBrush, new[] { new PointF(box.Left, box.Bottom), new PointF(box.Left - s, box.Bottom + s), new PointF(box.Left + s, box.Bottom) });
            g.FillPolygon(inkBrush, new[] { new PointF(box.Right, box.Bottom), new PointF(box.Right + s, box.Bottom + s), new PointF(box.Right - s, box.Bottom) });
        }
    }

    private void DrawObstacles(Graphics g, Brush inkBrush)
    {
        foreach (Point obstacle in obstacles)
        {
            RectangleF box = new RectangleF(obstacle.X * CellSize + 4, TopBarHeight + obstacle.Y * CellSize + 4,
                CellSize - 8, CellSize - 8);
            g.FillRectangle(inkBrush, box);
        }
    }

    private void DrawBoss(Graphics g)
    {
        float cx = bossPosition.X * CellSize + CellSize / 2f;
        float cy = TopBarHeight + bossPosition.Y * CellSize + CellSize / 2f;
        float radius = CellSize * 0.45f;

        using (Brush bossBrush = new SolidBrush(Color.FromArgb(120, 30, 30)))
            g.FillEllipse(bossBrush, cx - radius, cy - radius, radius * 2, radius * 2);

        using (Pen bossPen = new Pen(BackgroundColor, 2f))
            g.DrawEllipse(bossPen, cx - radius, cy - radius, radius * 2, radius * 2);

        float healthWidth = Math.Max(4, radius * 1.5f * bossHealth / BossInitialHealth);
        RectangleF healthBar = new RectangleF(cx - radius, cy + radius + 2, healthWidth, 4);
        using (Brush healthBrush = new SolidBrush(Color.FromArgb(220, 60, 60)))
            g.FillRectangle(healthBrush, healthBar);
    }

    private void DrawNameEntry(Graphics g)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);

        using (Brush overlayBrush = new SolidBrush(BackgroundColor))
            g.FillRectangle(overlayBrush, field);

        using (Font titleFont = new Font("Consolas", 15f, FontStyle.Bold))
        using (Font inputFont = new Font("Consolas", 14f, FontStyle.Bold))
        using (Font hintFont = new Font("Consolas", 9f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            string title = "ENTER YOUR NAME";
            SizeF titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, textBrush, field.Left + (field.Width - titleSize.Width) / 2, field.Top + 90);

            bool showCursor = (Environment.TickCount / 400) % 2 == 0;
            string shown = nameInput + (showCursor ? "_" : " ");
            SizeF inputSize = g.MeasureString(shown, inputFont);
            g.DrawString(shown, inputFont, textBrush, field.Left + (field.Width - inputSize.Width) / 2, field.Top + 130);

            string hint = "TYPE A-Z 0-9   ENTER: CONFIRM";
            SizeF hintSize = g.MeasureString(hint, hintFont);
            g.DrawString(hint, hintFont, textBrush, field.Left + (field.Width - hintSize.Width) / 2, field.Top + 180);
        }
    }

    private void DrawCustomizeSnake(Graphics g)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);

        using (Brush overlayBrush = new SolidBrush(BackgroundColor))
            g.FillRectangle(overlayBrush, field);

        if (currentProfile == null)
            return;

        using (Font titleFont = new Font("Consolas", 13f, FontStyle.Bold))
        using (Font hintFont = new Font("Consolas", 8.5f, FontStyle.Bold))
        using (Font previewFont = new Font("Consolas", 9f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            g.DrawString("CUSTOMIZE SNAKE", titleFont, textBrush, field.Left + 22, field.Top + 18);

            string colorLine = "COLOR: " + SnakeColorNames[(int)currentProfile.Appearance.ColorTheme];
            string shapeLine = "SHAPE: " + SnakeShapeNames[(int)currentProfile.Appearance.ShapeStyle];
            g.DrawString(colorLine, hintFont, textBrush, field.Left + 24, field.Top + 58);
            g.DrawString(shapeLine, hintFont, textBrush, field.Left + 24, field.Top + 78);
            string themeLine = "THEME: " + SnakeThemeNames[(int)currentProfile.Appearance.Theme];
            g.DrawString(themeLine, hintFont, textBrush, field.Left + 24, field.Top + 98);

            g.DrawString("LEFT/RIGHT: COLOR", previewFont, textBrush, field.Left + 24, field.Top + 128);
            g.DrawString("UP/DOWN: SHAPE", previewFont, textBrush, field.Left + 24, field.Top + 148);
            g.DrawString("PG UP/DOWN: THEME", previewFont, textBrush, field.Left + 24, field.Top + 168);
            g.DrawString("ENTER: SAVE + RETURN", previewFont, textBrush, field.Left + 24, field.Top + 188);
            g.DrawString("ESC: BACK", previewFont, textBrush, field.Left + 24, field.Top + 208);

            Rectangle previewRect = new Rectangle(field.Left + 24, field.Top + 245, 140, 90);
            using (Brush previewBrush = new SolidBrush(GetSnakeBodyColor(currentProfile.Appearance)))
            using (Brush accentBrush = new SolidBrush(GetSnakeAccentColor(currentProfile.Appearance)))
            {
                DrawSegmentShape(g, previewBrush, new RectangleF(previewRect.Left + 16, previewRect.Top + 14, 20, 20), currentProfile.Appearance.ShapeStyle, 5f, true);
                DrawSegmentShape(g, accentBrush, new RectangleF(previewRect.Left + 40, previewRect.Top + 14, 20, 20), currentProfile.Appearance.ShapeStyle, 5f, true);
                DrawSegmentShape(g, previewBrush, new RectangleF(previewRect.Left + 64, previewRect.Top + 14, 20, 20), currentProfile.Appearance.ShapeStyle, 5f, true);
                DrawSegmentShape(g, accentBrush, new RectangleF(previewRect.Left + 88, previewRect.Top + 14, 20, 20), currentProfile.Appearance.ShapeStyle, 5f, true);
                DrawSnakeHead(g, accentBrush, new List<Point> { new Point(3, 0) }, Direction.Right, true, currentProfile.Appearance, 24, previewRect.Top + 44);
                using (Brush themeBrush = new SolidBrush(GetThemeAccentColor(currentProfile.Appearance.Theme)))
                {
                    Rectangle themeSample = new Rectangle(previewRect.Left + 16, previewRect.Top + 50, 112, 24);
                    g.FillRectangle(themeBrush, themeSample);
                    using (Pen borderPen = new Pen(InkColor, 1))
                        g.DrawRectangle(borderPen, themeSample);
                }
            }
        }
    }

    // One display line summarizing whatever achievements were just unlocked this game,
    // or null if none were. Used by the Game Over overlay.
    private string? AchievementUnlockLine()
    {
        if (newlyUnlockedThisGame.Count == 0)
            return null;
        if (newlyUnlockedThisGame.Count == 1)
            return "UNLOCKED: " + newlyUnlockedThisGame[0];
        return newlyUnlockedThisGame.Count + " ACHIEVEMENTS UNLOCKED!";
    }

    private void DrawGameOverOverlay(Graphics g)
    {
        if (mode == GameMode.Solo && currentProfile == null)
            return;

        if (mode == GameMode.Solo)
        {
            string title = won ? "YOU WIN!" : "GAME OVER";
            string resultLine = "SCORE " + score.ToString("000") + "   LVL " + level + "   TIME " + FormatTime(stopwatch.Elapsed);
            string bestLine = isNewBest ? "NEW BEST SCORE!" : "BEST " + currentProfile!.BestScore.ToString("000");
            List<string> lines = new List<string> { resultLine, bestLine };
            string? achLine = AchievementUnlockLine();
            if (achLine != null)
                lines.Add(achLine);
            lines.Add("SPACE: RETRY   L: SCORES   M: MENU");
            DrawOverlay(g, title, lines.ToArray());
            return;
        }

        if (mode == GameMode.Zen)
        {
            string resultLine = "SCORE " + score.ToString("000") + "   LVL " + level + "   TIME " + FormatTime(stopwatch.Elapsed);
            List<string> lines = new List<string> { resultLine };
            if (currentProfile != null)
                lines.Add(isNewBest ? "NEW BEST SCORE!" : "BEST " + currentProfile.BestScore.ToString("000"));
            string? achLine = AchievementUnlockLine();
            if (achLine != null)
                lines.Add(achLine);
            lines.Add("SPACE: RETRY   M: MENU");
            DrawOverlay(g, "GAME OVER", lines.ToArray());
            return;
        }

        string duelTitle;
        if (netStatusMessage.Length > 0)
        {
            duelTitle = "CONNECTION LOST";
        }
        else if (duelWinner == DuelWinner.Draw)
        {
            duelTitle = "DRAW!";
        }
        else if (mode == GameMode.DuelLocal)
        {
            duelTitle = duelWinner == DuelWinner.Player1 ? "PLAYER 1 WINS!" : "PLAYER 2 WINS!";
        }
        else
        {
            bool localIsPlayer1 = mode == GameMode.DuelHost;
            bool localWon = (localIsPlayer1 && duelWinner == DuelWinner.Player1) ||
                             (!localIsPlayer1 && duelWinner == DuelWinner.Player2);
            duelTitle = localWon ? "YOU WIN!" : "YOU LOSE";
        }

        string duelResult = "P1 " + score.ToString("000") + "   P2 " + score2.ToString("000") + "   LVL " + level;

        if (netStatusMessage.Length > 0)
            DrawOverlay(g, duelTitle, duelResult, netStatusMessage, "M: MENU");
        else if (mode == GameMode.DuelGuest)
            DrawOverlay(g, duelTitle, duelResult, "WAITING FOR HOST...", "M: MENU");
        else
            DrawOverlay(g, duelTitle, duelResult, "SPACE: REPLAY   M: MENU");
    }

    private void DrawOverlay(Graphics g, string title, params string[] subtitleLines)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);

        using (Brush overlayBrush = new SolidBrush(Color.FromArgb(210, BackgroundColor)))
            g.FillRectangle(overlayBrush, field);

        using (Font titleFont = new Font("Consolas", 18f, FontStyle.Bold))
        using (Font subFont = new Font("Consolas", 9f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            SizeF titleSize = g.MeasureString(title, titleFont);
            float titleY = field.Top + field.Height / 2f - titleSize.Height - subtitleLines.Length * 7f;
            g.DrawString(title, titleFont, textBrush, field.Left + (field.Width - titleSize.Width) / 2, titleY);

            float lineY = titleY + titleSize.Height + 8;
            for (int i = 0; i < subtitleLines.Length; i++)
            {
                string line = subtitleLines[i];
                bool isLast = i == subtitleLines.Length - 1;
                SizeF lineSize = g.MeasureString(line, subFont);
                float lineX = field.Left + (field.Width - lineSize.Width) / 2;

                if (isLast)
                {
                    // Gently pulse the call-to-action line, like a "press start" prompt
                    double t = Environment.TickCount / 260.0;
                    int alpha = 140 + (int)(115 * (0.5 + 0.5 * Math.Sin(t)));
                    using (Brush pulseBrush = new SolidBrush(Color.FromArgb(alpha, InkColor)))
                        g.DrawString(line, subFont, pulseBrush, lineX, lineY);
                }
                else
                {
                    g.DrawString(line, subFont, textBrush, lineX, lineY);
                }

                lineY += 16;
            }
        }
    }

    private void DrawScoreboard(Graphics g)
    {
        Rectangle field = new Rectangle(0, TopBarHeight, gridWidth * CellSize, gridHeight * CellSize);

        using (Brush overlayBrush = new SolidBrush(BackgroundColor))
            g.FillRectangle(overlayBrush, field);

        List<GlobalScoreEntry> ranked = new List<GlobalScoreEntry>(globalScores);
        ranked.Sort(delegate (GlobalScoreEntry a, GlobalScoreEntry b)
        {
            int scoreCompare = b.Score.CompareTo(a.Score);
            if (scoreCompare != 0)
                return scoreCompare;
            return a.Time.CompareTo(b.Time);
        });

        using (Font titleFont = new Font("Consolas", 14f, FontStyle.Bold))
        using (Font rowFont = new Font("Consolas", 9f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            string title = "GLOBAL RECORDS";
            SizeF titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, textBrush, field.Left + (field.Width - titleSize.Width) / 2, field.Top + 8);

            float y = field.Top + 42;
            int rowCount = Math.Min(ranked.Count, ScoreboardRows);
            for (int i = 0; i < rowCount; i++)
            {
                GlobalScoreEntry p = ranked[i];
                string marker = currentProfile != null && p.Name == currentProfile.Name.ToUpperInvariant() ? ">" : " ";
                string row = string.Format("{0}{1,2}.{2,-10}{3,4}  {4}",
                    marker, i + 1, Truncate(p.Name, MaxNameLength), p.Score, FormatTime(p.Time));
                g.DrawString(row, rowFont, textBrush, field.Left + 12, y);
                y += 18;
            }

            if (ranked.Count == 0)
            {
                string empty = "NO SCORES YET";
                SizeF emptySize = g.MeasureString(empty, rowFont);
                g.DrawString(empty, rowFont, textBrush, field.Left + (field.Width - emptySize.Width) / 2, y);
            }

            string hint = "SPACE: BACK";
            SizeF hintSize = g.MeasureString(hint, rowFont);
            g.DrawString(hint, rowFont, textBrush, field.Left + (field.Width - hintSize.Width) / 2, field.Bottom - 22);
        }
    }

    private static string FormatTime(TimeSpan t)
    {
        return string.Format("{0:00}:{1:00}", (int)t.TotalMinutes, t.Seconds);
    }

    private static string Truncate(string s, int max)
    {
        return s.Length <= max ? s : s.Substring(0, max);
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (state)
        {
            case GameState.NameEntry:
                HandleNameEntryKey(e);
                return;

            case GameState.OnlineJoinEntry:
                HandleIpEntryKey(e);
                return;

            case GameState.ModeSelect:
                if (e.KeyCode == Keys.D1) { mode = GameMode.Solo; state = GameState.Ready; Invalidate(); return; }
                else if (e.KeyCode == Keys.D2) { StartLocalDuelSetup(); return; }
                else if (e.KeyCode == Keys.D3) { StartAIDuelSetup(); return; }
                else if (e.KeyCode == Keys.D4) { StartBossFightSetup(); return; }
                else if (e.KeyCode == Keys.D5) { StartProceduralSetup(); return; }
                else if (e.KeyCode == Keys.D6) { StartZenSetup(); return; }
                else if (e.KeyCode == Keys.D7) { HostDuel(); return; }
                else if (e.KeyCode == Keys.D8) { joinIpInput = ""; netStatusMessage = ""; state = GameState.OnlineJoinEntry; Invalidate(); return; }
                break;
            case GameState.OnlineHostWait:
            case GameState.OnlineConnecting:
                if (e.KeyCode == Keys.Escape)
                {
                    CloseNetworking();
                    mode = GameMode.Solo;
                    state = GameState.ModeSelect;
                    Invalidate();
                }
                return;

            case GameState.Scoreboard:
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Escape)
                {
                    state = GameState.Ready;
                    Invalidate();
                }
                return;

            case GameState.Ready:
            case GameState.GameOver:
                if (e.KeyCode == Keys.Space)
                {
                    if (mode == GameMode.Solo || mode == GameMode.BossFight || mode == GameMode.Procedural || mode == GameMode.Zen)
                        StartGame(); // single-snake modes: reset via StartGame, not the duel-grid StartDuel
                    else if (mode == GameMode.DuelGuest)
                        SendLine("READY"); // host controls the actual restart
                    else
                        StartDuel();
                }
                else if (mode == GameMode.Solo && e.KeyCode == Keys.L)
                {
                    state = GameState.Scoreboard;
                    Invalidate();
                }
                else if (mode == GameMode.Solo && e.KeyCode == Keys.N)
                {
                    nameInput = "";
                    state = GameState.NameEntry;
                    Invalidate();
                }
                else if (mode == GameMode.Solo && e.KeyCode == Keys.H)
                {
                    if (currentProfile != null)
                    {
                        state = GameState.ProfileHistory;
                        Invalidate();
                    }
                }
                else if (mode == GameMode.Solo && e.KeyCode == Keys.T)
                {
                    if (currentProfile != null)
                    {
                        state = GameState.Achievements;
                        Invalidate();
                    }
                }
                else if (mode == GameMode.Solo && e.KeyCode == Keys.C)
                {
                    if (currentProfile != null)
                    {
                        localAppearance = currentProfile.Appearance;
                        state = GameState.CustomizeSnake;
                        Invalidate();
                    }
                }
                else if (e.KeyCode == Keys.M)
                {
                    CloseNetworking();
                    mode = GameMode.Solo;
                    state = GameState.ModeSelect;
                    Invalidate();
                }
                return;

            case GameState.CustomizeSnake:
                if (e.KeyCode == Keys.Escape)
                {
                    state = GameState.Ready;
                    Invalidate();
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    if (currentProfile != null)
                    {
                        SaveProfiles();
                        ShowBanner("STYLE SAVED");
                    }
                    state = GameState.Ready;
                    Invalidate();
                }
                else if (e.KeyCode == Keys.Left)
                {
                    if (currentProfile != null)
                    {
                        int next = ((int)currentProfile.Appearance.ColorTheme + SnakeColorNames.Length - 1) % SnakeColorNames.Length;
                        currentProfile.Appearance.ColorTheme = (SnakeColorTheme)next;
                        Invalidate();
                    }
                }
                else if (e.KeyCode == Keys.Right)
                {
                    if (currentProfile != null)
                    {
                        int next = ((int)currentProfile.Appearance.ColorTheme + 1) % SnakeColorNames.Length;
                        currentProfile.Appearance.ColorTheme = (SnakeColorTheme)next;
                        Invalidate();
                    }
                }
                else if (e.KeyCode == Keys.Up)
                {
                    if (currentProfile != null)
                    {
                        int next = ((int)currentProfile.Appearance.ShapeStyle + SnakeShapeNames.Length - 1) % SnakeShapeNames.Length;
                        currentProfile.Appearance.ShapeStyle = (SnakeShapeStyle)next;
                        Invalidate();
                    }
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (currentProfile != null)
                    {
                        int next = ((int)currentProfile.Appearance.ShapeStyle + 1) % SnakeShapeNames.Length;
                        currentProfile.Appearance.ShapeStyle = (SnakeShapeStyle)next;
                        Invalidate();
                    }
                }
                else if (e.KeyCode == Keys.PageUp)
                {
                    if (currentProfile != null)
                    {
                        int next = ((int)currentProfile.Appearance.Theme + 1) % SnakeThemeNames.Length;
                        currentProfile.Appearance.Theme = (BoardTheme)next;
                        Invalidate();
                    }
                }
                else if (e.KeyCode == Keys.PageDown)
                {
                    if (currentProfile != null)
                    {
                        int next = ((int)currentProfile.Appearance.Theme + SnakeThemeNames.Length - 1) % SnakeThemeNames.Length;
                        currentProfile.Appearance.Theme = (BoardTheme)next;
                        Invalidate();
                    }
                }
                return;

            case GameState.ProfileHistory:
            case GameState.Achievements:
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Escape)
                {
                    state = GameState.Ready;
                    Invalidate();
                }
                return;

            case GameState.Paused:
                if (e.KeyCode == Keys.P)
                {
                    if (mode == GameMode.DuelGuest)
                    {
                        SendLine("PAUSEREQ");
                    }
                    else
                    {
                        state = GameState.Playing;
                        timer.Start();
                        stopwatch.Start();
                        if (mode == GameMode.DuelHost)
                            SendLine("RESUMED");
                        Invalidate();
                    }
                }
                else if (e.KeyCode == Keys.M)
                {
                    CloseNetworking();
                    mode = GameMode.Solo;
                    timer.Stop();
                    state = GameState.ModeSelect;
                    Invalidate();
                }
                return;
        }

        switch (e.KeyCode)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.Left:
            case Keys.Right:
                HandleDirectionKey(e.KeyCode, true);
                break;
            case Keys.W:
            case Keys.A:
            case Keys.S:
            case Keys.D:
                HandleDirectionKey(e.KeyCode, false);
                break;
            case Keys.P:
                if (mode == GameMode.DuelGuest)
                {
                    SendLine("PAUSEREQ");
                }
                else
                {
                    state = GameState.Paused;
                    timer.Stop();
                    stopwatch.Stop();
                    if (mode == GameMode.DuelHost)
                        SendLine("PAUSED");
                    Invalidate();
                }
                break;
        }
    }

    // Arrow keys and WASD both drive the local player, except in local duel where
    // arrows control player 1 and WASD controls player 2 on the same keyboard.
    private void HandleDirectionKey(Keys key, bool isArrowKey)
    {
        Direction requested = KeyToDirection(key);

        if (mode == GameMode.DuelLocal)
        {
            if (isArrowKey)
                SetPendingDirection1(requested);
            else
                SetPendingDirection2(requested);
            return;
        }

        if (mode == GameMode.DuelGuest)
        {
            SendLine("DIR:" + requested);
            return;
        }

        SetPendingDirection1(requested);
    }

    private static Direction KeyToDirection(Keys key)
    {
        switch (key)
        {
            case Keys.Up:
            case Keys.W: return Direction.Up;
            case Keys.Down:
            case Keys.S: return Direction.Down;
            case Keys.Left:
            case Keys.A: return Direction.Left;
            default: return Direction.Right;
        }
    }

    private void SetPendingDirection1(Direction d)
    {
        if (d != Opposite(direction))
            pendingDirection = d;
    }

    private void SetPendingDirection2(Direction d)
    {
        if (d != Opposite(direction2))
            pendingDirection2 = d;
    }

    private void UpdateAIDirection()
    {
        Point head = snake2[snake2.Count - 1];
        Point goal = specialKind != SpecialKind.None ? specialPosition : food;
        Direction best = direction2;
        int bestScore = int.MinValue;

        foreach (Direction candidate in new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
        {
            if (candidate == Opposite(direction2))
                continue;

            Point next = MoveHead(head, candidate);
            if (IsOutOfBounds(next))
                continue;

            bool collision = false;
            int bodyCount = snake2.Count - 1;
            for (int i = 0; i < bodyCount; i++)
            {
                if (snake2[i] == next)
                {
                    collision = true;
                    break;
                }
            }
            if (collision)
                continue;

            int score = -(Math.Abs(next.X - goal.X) + Math.Abs(next.Y - goal.Y));
            if (next == goal)
                score += 100;
            if (next == food && specialKind != SpecialKind.None)
                score += 30;
            if (next == specialPosition && specialKind == SpecialKind.Trap)
                score -= 80;

            Point head1 = snake[snake.Count - 1];
            if (next == head1)
                score -= 50;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        pendingDirection2 = best;
    }

    private void HandleNameEntryKey(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (nameInput.Trim().Length == 0)
                return;
            SelectProfile(nameInput);
            state = GameState.Ready;
            Invalidate();
            return;
        }

        if (e.KeyCode == Keys.Back)
        {
            if (nameInput.Length > 0)
                nameInput = nameInput.Substring(0, nameInput.Length - 1);
            Invalidate();
            return;
        }

        bool isLetter = e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z;
        bool isDigit = e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9;
        if ((isLetter || isDigit) && nameInput.Length < MaxNameLength)
        {
            nameInput += (char)e.KeyCode;
            Invalidate();
        }
    }

    private void HandleIpEntryKey(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            string ip = joinIpInput.Trim();
            if (ip.Length == 0)
                return;
            JoinDuel(ip);
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            state = GameState.ModeSelect;
            Invalidate();
            return;
        }

        if (e.KeyCode == Keys.Back)
        {
            if (joinIpInput.Length > 0)
                joinIpInput = joinIpInput.Substring(0, joinIpInput.Length - 1);
            Invalidate();
            return;
        }

        bool isDigit = e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9;
        bool isDot = e.KeyCode == Keys.OemPeriod || e.KeyCode == Keys.Decimal;
        bool isLetter = e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z; // allows hostnames like "localhost"
        if ((isDigit || isDot || isLetter) && joinIpInput.Length < MaxIpLength)
        {
            joinIpInput += isDot ? '.' : (char)e.KeyCode;
            Invalidate();
        }
    }

}
