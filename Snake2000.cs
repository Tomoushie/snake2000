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

    private enum GameState
    {
        NameEntry, Ready, Playing, Paused, GameOver, Scoreboard,
        ModeSelect, OnlineHostWait, OnlineJoinEntry, OnlineConnecting
    }
    private enum Direction { Up, Down, Left, Right }
    private enum SpecialKind { None, Speed, Trap }
    private enum GameMode { Solo, DuelLocal, DuelHost, DuelGuest }
    private enum DuelWinner { None, Player1, Player2, Draw }

    private class PlayerProfile
    {
        public readonly string Name;
        public int BestScore;
        public TimeSpan BestTime;
        public int GamesPlayed;

        public PlayerProfile(string name)
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
                try { Console.Beep(n.Frequency, n.DurationMs); }
                catch (Exception) { }
            }
        });
    }

    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer animationTimer;
    private readonly Stopwatch stopwatch = new Stopwatch();
    private readonly List<Point> snake = new List<Point>();
    private readonly List<Point> snake2 = new List<Point>();
    private readonly Random random = new Random();
    private readonly List<PlayerProfile> profiles;

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
    private GameState state;
    private int score;
    private int level;
    private string bannerText = "";
    private int bannerTicksLeft;
    private int currentInterval;
    private bool won;
    private bool isNewBest;
    private string nameInput = "";
    private PlayerProfile currentProfile;
    private int deathFlashTicksLeft;

    // Networking (duel host/guest)
    private TcpListener hostListener;
    private TcpClient duelClient;
    private StreamReader netReader;
    private StreamWriter netWriter;
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

    private void OnAnimationTick(object sender, EventArgs e)
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
        int startX = gridWidth / 2;
        int startY = gridHeight / 2;
        for (int i = 2; i >= 0; i--)
            snake.Add(new Point(startX - i, startY));

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
        currentInterval = BaseIntervalMs;
        stopwatch.Reset();
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
        specialKind = random.Next(100) < SpeedBonusChancePercent ? SpecialKind.Speed : SpecialKind.Trap;

        Point candidate;
        int attempts = 0;
        do
        {
            candidate = new Point(random.Next(gridWidth), random.Next(gridHeight));
            attempts++;
        } while ((IsOccupied(candidate) || candidate == food) && attempts < 200);

        specialPosition = candidate;
        specialTicksLeft = SpecialLifetimeTicks;
    }

    private bool IsOccupied(Point p)
    {
        return snake.Contains(p) || (mode != GameMode.Solo && snake2.Contains(p));
    }

    // Speed fruit: grants a short burst of extra speed on top of the current level speed.
    private void ApplySpeedBoost()
    {
        speedBoostTicksLeft = SpeedBoostTicks;
        timer.Interval = SpeedBoostIntervalMs;
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

    private void ResetDuel()
    {
        gridWidth = DuelGridSize;
        gridHeight = DuelGridSize;
        ClientSize = new Size(gridWidth * CellSize, gridHeight * CellSize + TopBarHeight);

        snake.Clear();
        snake2.Clear();
        int y1 = gridHeight / 3;
        int y2 = gridHeight - gridHeight / 3 - 1;
        for (int i = 2; i >= 0; i--)
            snake.Add(new Point(3 + i, y1));
        for (int i = 2; i >= 0; i--)
            snake2.Add(new Point(gridWidth - 4 - i, y2));

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
        } while (IsOccupied(candidate));

        food = candidate;
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        if (mode != GameMode.Solo)
        {
            OnDuelTimerTick();
            return;
        }

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

        // Hitting a wall ends the game outright, no wrap-around - just like the phone original
        if (newHead.X < 0 || newHead.X >= gridWidth || newHead.Y < 0 || newHead.Y >= gridHeight)
        {
            FinishGame(false);
            return;
        }

        bool willEat = newHead == food;
        bool willEatSpecial = specialKind != SpecialKind.None && newHead == specialPosition;

        // Ignore the tail cell in the self-collision check, since it moves away
        // this tick unless the snake is growing.
        int bodyToCheck = (willEat || willEatSpecial) ? snake.Count : snake.Count - 1;
        for (int i = 0; i < bodyToCheck; i++)
        {
            if (snake[i] == newHead)
            {
                FinishGame(false);
                return;
            }
        }

        snake.Add(newHead);

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
        else if (willEat)
        {
            score++;
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
            snake.RemoveAt(0);
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
        if (isNewBest)
        {
            currentProfile.BestScore = score;
            currentProfile.BestTime = stopwatch.Elapsed;
        }
        if (currentProfile != null)
        {
            currentProfile.GamesPlayed++;
            SaveProfiles();
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

    // Runs the duel simulation for both snakes at once (local duel and duel host only -
    // the guest never calls this, it just renders whatever the host sends it).
    private void OnDuelTimerTick()
    {
        direction = pendingDirection;
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

        PlayerProfile found = null;
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
                    if (parts.Length != 4 || parts[0].Length == 0)
                        continue;

                    int bestScore, bestTimeSeconds, gamesPlayed;
                    if (!int.TryParse(parts[1], out bestScore)) continue;
                    if (!int.TryParse(parts[2], out bestTimeSeconds)) continue;
                    if (!int.TryParse(parts[3], out gamesPlayed)) continue;

                    result.Add(new PlayerProfile(parts[0])
                    {
                        BestScore = bestScore,
                        BestTime = TimeSpan.FromSeconds(bestTimeSeconds),
                        GamesPlayed = gamesPlayed
                    });
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
                lines.Add(p.Name + "|" + p.BestScore + "|" + (int)p.BestTime.TotalSeconds + "|" + p.GamesPlayed);
            File.WriteAllLines(ProfilesPath, lines);
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

            netWriter.WriteLine("NAME:" + currentProfile.Name);
            string line = netReader.ReadLine();
            string guestName = ParseName(line);

            BeginInvoke(new MethodInvoker(delegate
            {
                player2Name = guestName.Length > 0 ? guestName : "GUEST";
                netConnected = true;
                state = GameState.Ready;
                Invalidate();
            }));

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
            string line;
            while ((line = netReader.ReadLine()) != null)
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

            netWriter.WriteLine("NAME:" + currentProfile.Name);
            string line = netReader.ReadLine();
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
            string line;
            while ((line = netReader.ReadLine()) != null)
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

    private static string ParseName(string line)
    {
        if (line != null && line.StartsWith("NAME:"))
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
        if (mode == GameMode.Solo)
        {
            DrawOverlay(g, "SNAKE", "PLAYER " + currentProfile.Name,
                "SPACE: PLAY   L: SCORES   N: NAME   M: DUEL",
                "LEVELS SPEED UP + GROW THE BOARD",
                "BONUS: SPEED FRUIT   TRAP: SHRINKS YOU");
        }
        else if (mode == GameMode.DuelLocal)
        {
            DrawOverlay(g, "LOCAL DUEL", "P1: ARROW KEYS      P2: WASD",
                "SPACE: PLAY   M: MENU");
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
            "3: HOST ONLINE DUEL",
            "4: JOIN ONLINE DUEL",
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
            state == GameState.OnlineJoinEntry || state == GameState.OnlineConnecting)
            return;

        using (Font hudFont = new Font("Consolas", 9.5f, FontStyle.Bold))
        using (Brush hudBrush = new SolidBrush(BackgroundColor))
        {
            if (mode == GameMode.Solo)
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
        using (Brush bg = new SolidBrush(BackgroundColor))
            g.FillRectangle(bg, field);

        using (Pen gridPen = new Pen(GridLineColor))
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
            DrawSnakeBody(g, inkBrush, snake, direction, true);
            if (mode != GameMode.Solo)
                DrawSnakeBody(g, inkBrush, snake2, direction2, false);
        }

        using (Pen borderPen = new Pen(InkColor, 3))
            g.DrawRectangle(borderPen, 1, TopBarHeight + 1, gridWidth * CellSize - 2, gridHeight * CellSize - 2);
    }

    // Draws a snake as smooth, slightly banded rounded segments, tapered at the tail,
    // with a distinct head that has eyes (and an occasional tongue flick) facing the
    // direction of travel. Player 1 is drawn solid; player 2 is drawn as an outline
    // so two snakes stay easy to tell apart on the monochrome LCD palette.
    private void DrawSnakeBody(Graphics g, Brush inkBrush, List<Point> body, Direction dir, bool filled)
    {
        if (body.Count == 0)
            return;

        Color midColor = Color.FromArgb(
            (InkColor.R + BackgroundColor.R) / 2,
            (InkColor.G + BackgroundColor.G) / 2,
            (InkColor.B + BackgroundColor.B) / 2);

        using (Brush bandBrush = new SolidBrush(midColor))
        using (Pen outlinePen = new Pen(InkColor, 1.6f))
        {
            for (int i = 0; i < body.Count - 1; i++)
            {
                Point p = body[i];
                bool isTail = i == 0;
                float inset = isTail ? 5f : 2f;
                RectangleF r = new RectangleF(
                    p.X * CellSize + inset,
                    TopBarHeight + p.Y * CellSize + inset,
                    CellSize - inset * 2,
                    CellSize - inset * 2);

                if (filled)
                {
                    Brush segmentBrush = (i % 2 == 0) ? inkBrush : bandBrush;
                    FillRoundedRect(g, segmentBrush, r, 5f);
                }
                else
                {
                    DrawRoundedRect(g, outlinePen, r, 5f);
                }
            }
        }

        DrawSnakeHead(g, inkBrush, body, dir, filled);
    }

    private void DrawSnakeHead(Graphics g, Brush inkBrush, List<Point> body, Direction dir, bool filled)
    {
        Point head = body[body.Count - 1];
        RectangleF r = new RectangleF(
            head.X * CellSize + 1,
            TopBarHeight + head.Y * CellSize + 1,
            CellSize - 2,
            CellSize - 2);

        if (filled)
        {
            FillRoundedRect(g, inkBrush, r, 6f);
        }
        else
        {
            using (Pen headPen = new Pen(InkColor, 1.8f))
                DrawRoundedRect(g, headPen, r, 6f);
        }

        DrawEyes(g, r, dir, filled);
        DrawTongue(g, r, dir);
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

    private void DrawGameOverOverlay(Graphics g)
    {
        if (mode == GameMode.Solo)
        {
            string title = won ? "YOU WIN!" : "GAME OVER";
            string resultLine = "SCORE " + score.ToString("000") + "   LVL " + level + "   TIME " + FormatTime(stopwatch.Elapsed);
            string bestLine = isNewBest ? "NEW BEST SCORE!" : "BEST " + currentProfile.BestScore.ToString("000");
            DrawOverlay(g, title, resultLine, bestLine, "SPACE: RETRY   L: SCORES   M: MENU");
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

        List<PlayerProfile> ranked = new List<PlayerProfile>(profiles);
        ranked.Sort(delegate (PlayerProfile a, PlayerProfile b) { return b.BestScore.CompareTo(a.BestScore); });

        using (Font titleFont = new Font("Consolas", 14f, FontStyle.Bold))
        using (Font rowFont = new Font("Consolas", 9f, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(InkColor))
        {
            string title = "SCOREBOARD";
            SizeF titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, textBrush, field.Left + (field.Width - titleSize.Width) / 2, field.Top + 8);

            float y = field.Top + 42;
            int rowCount = Math.Min(ranked.Count, ScoreboardRows);
            for (int i = 0; i < rowCount; i++)
            {
                PlayerProfile p = ranked[i];
                string marker = ReferenceEquals(p, currentProfile) ? ">" : " ";
                string row = string.Format("{0}{1,2}.{2,-10}{3,4}  {4}",
                    marker, i + 1, Truncate(p.Name, MaxNameLength), p.BestScore, FormatTime(p.BestTime));
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
                if (e.KeyCode == Keys.D1) { mode = GameMode.Solo; state = GameState.Ready; Invalidate(); }
                else if (e.KeyCode == Keys.D2) { StartLocalDuelSetup(); }
                else if (e.KeyCode == Keys.D3) { HostDuel(); }
                else if (e.KeyCode == Keys.D4) { joinIpInput = ""; netStatusMessage = ""; state = GameState.OnlineJoinEntry; Invalidate(); }
                else if (e.KeyCode == Keys.Escape) { state = GameState.Ready; Invalidate(); }
                return;

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
                    if (mode == GameMode.Solo)
                        StartGame();
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
                else if (e.KeyCode == Keys.M)
                {
                    CloseNetworking();
                    mode = GameMode.Solo;
                    state = GameState.ModeSelect;
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

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SnakeGame());
    }
}
