using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace RPGCharacterManager.UI.Views.Documents.Games;

internal sealed class DoomArenaGame : ArcadeGameBase
{
    private const double FieldOfView = Math.PI / 3;
    private static readonly string[] Map =
    [
        "##############",
        "#............#",
        "#..##........#",
        "#..#.....##..#",
        "#..#.........#",
        "#......#.....#",
        "#.##...#..#..#",
        "#......#..#..#",
        "#..#.........#",
        "#..#..###....#",
        "#............#",
        "##############",
    ];

    private readonly HashSet<Key> _keys = [];
    private readonly List<Enemy> _enemies = [];
    private double _playerX;
    private double _playerY;
    private double _angle;
    private double _health;

    public DoomArenaGame()
        : base(
            ArcadeGameKind.Doom,
            "DOOM: открытая арена",
            "Самостоятельная ретро-арена на открытой реализации без закрытых ресурсов оригинальной игры.",
            "W/S — вперёд/назад · A/D — шаг вбок · ← → — поворот · Пробел — огонь · P — пауза")
    {
    }

    public override void Start()
    {
        _playerX = 1.7;
        _playerY = 1.7;
        _angle = 0;
        _health = 100;
        _keys.Clear();
        _enemies.Clear();
        _enemies.AddRange(
        [
            new Enemy(8.5, 1.7),
            new Enemy(11.3, 4.5),
            new Enemy(5.5, 7.5),
            new Enemy(10.5, 9.5),
            new Enemy(2.5, 9.5),
        ]);
        ResetScore();
        IsRunning = true;
        IsPaused = false;
        UpdateStatus();
        RaiseChanged();
    }

    public override void Tick(TimeSpan elapsed)
    {
        if (!IsRunning || IsPaused)
        {
            return;
        }

        var seconds = Math.Min(0.08, elapsed.TotalSeconds);
        var move = 2.7 * seconds;
        var turn = 2.2 * seconds;

        if (_keys.Contains(Key.Left))
        {
            _angle -= turn;
        }

        if (_keys.Contains(Key.Right))
        {
            _angle += turn;
        }

        var forward = (_keys.Contains(Key.W) ? 1 : 0) - (_keys.Contains(Key.S) ? 1 : 0);
        var strafe = (_keys.Contains(Key.D) ? 1 : 0) - (_keys.Contains(Key.A) ? 1 : 0);
        Move(
            Math.Cos(_angle) * forward * move + Math.Cos(_angle + Math.PI / 2) * strafe * move,
            Math.Sin(_angle) * forward * move + Math.Sin(_angle + Math.PI / 2) * strafe * move);

        foreach (var enemy in _enemies.Where(enemy => enemy.IsAlive))
        {
            var distance = Distance(_playerX, _playerY, enemy.X, enemy.Y);
            if (distance < 0.9)
            {
                _health -= 24 * seconds;
            }
        }

        if (_health <= 0)
        {
            _health = 0;
            Finish($"Вы погибли. Уничтожено: {_enemies.Count(enemy => !enemy.IsAlive)}.");
            return;
        }

        UpdateStatus();
        RaiseChanged();
    }

    public override void KeyDown(Key key)
    {
        if (key == Key.P)
        {
            TogglePause();
            return;
        }

        if (key == Key.Space && IsRunning && !IsPaused)
        {
            Shoot();
            return;
        }

        _keys.Add(key);
    }

    public override void KeyUp(Key key) => _keys.Remove(key);

    public override void Render(DrawingContext context, Rect bounds)
    {
        var horizon = bounds.Y + bounds.Height * 0.5;
        Fill(context, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height * 0.5),
            Color.Parse("#26191A"));
        Fill(context, new Rect(bounds.X, horizon, bounds.Width, bounds.Height * 0.5),
            Color.Parse("#292725"));

        var depthBuffer = new double[Math.Max(1, (int)Math.Ceiling(bounds.Width / 3))];
        for (var column = 0; column < depthBuffer.Length; column++)
        {
            var screen = column / (double)Math.Max(1, depthBuffer.Length - 1);
            var rayAngle = _angle - FieldOfView / 2 + screen * FieldOfView;
            var distance = CastRay(rayAngle);
            depthBuffer[column] = distance;
            var corrected = Math.Max(0.08, distance * Math.Cos(rayAngle - _angle));
            var wallHeight = Math.Min(bounds.Height * 1.8, bounds.Height / corrected);
            var shade = (byte)Math.Clamp(210 - corrected * 15, 45, 200);
            var color = Color.FromRgb(shade, (byte)(shade * 0.32), (byte)(shade * 0.23));
            Cell(context, new Rect(
                bounds.X + column * 3,
                horizon - wallHeight / 2,
                3.2,
                wallHeight), color);
        }

        foreach (var enemy in _enemies.Where(enemy => enemy.IsAlive)
                     .OrderByDescending(enemy => Distance(_playerX, _playerY, enemy.X, enemy.Y)))
        {
            var dx = enemy.X - _playerX;
            var dy = enemy.Y - _playerY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var relative = NormalizeAngle(Math.Atan2(dy, dx) - _angle);
            if (Math.Abs(relative) > FieldOfView * 0.62)
            {
                continue;
            }

            var screenX = bounds.X + bounds.Width * (0.5 + relative / FieldOfView);
            var size = Math.Clamp(bounds.Height / distance * 0.78, 12, bounds.Height * 0.9);
            var depthColumn = Math.Clamp(
                (int)((screenX - bounds.X) / Math.Max(1, bounds.Width) * depthBuffer.Length),
                0,
                depthBuffer.Length - 1);
            if (distance > depthBuffer[depthColumn] + 0.4)
            {
                continue;
            }

            var body = new Rect(screenX - size * 0.24, horizon - size * 0.28, size * 0.48, size * 0.68);
            Cell(context, body, Color.Parse("#B92B27"), Color.Parse("#FF7B70"), 2);
            context.DrawEllipse(
                new SolidColorBrush(Color.Parse("#D9473F")),
                new Pen(new SolidColorBrush(Color.Parse("#FF9A8F")), 2),
                new Point(screenX, horizon - size * 0.35),
                size * 0.24,
                size * 0.24);
        }

        var gunWidth = Math.Min(150, bounds.Width * 0.22);
        Cell(context, new Rect(
            bounds.Center.X - gunWidth / 2,
            bounds.Bottom - bounds.Height * 0.18,
            gunWidth,
            bounds.Height * 0.22),
            Color.Parse("#44464A"),
            Color.Parse("#8A8D92"),
            2);

        Text(context, $"ЗДОРОВЬЕ {Math.Ceiling(_health):0}", new Point(bounds.X + 14, bounds.Y + 10),
            17, _health > 35 ? Color.Parse("#F2F2F7") : Color.Parse("#FF453A"), FontWeight.Bold);
    }

    private void Shoot()
    {
        Enemy? target = null;
        var targetDistance = double.MaxValue;

        foreach (var enemy in _enemies.Where(enemy => enemy.IsAlive))
        {
            var angle = NormalizeAngle(Math.Atan2(enemy.Y - _playerY, enemy.X - _playerX) - _angle);
            var distance = Distance(_playerX, _playerY, enemy.X, enemy.Y);
            if (Math.Abs(angle) < 0.10 && distance < targetDistance && HasLineOfSight(enemy))
            {
                target = enemy;
                targetDistance = distance;
            }
        }

        if (target is null)
        {
            Status = "Выстрел в пустоту.";
            RaiseChanged();
            return;
        }

        target.IsAlive = false;
        AddScore(250);
        var remaining = _enemies.Count(enemy => enemy.IsAlive);
        if (remaining == 0)
        {
            AddScore((int)Math.Ceiling(_health) * 10);
            Finish($"Арена очищена! Счёт: {Score}.");
            return;
        }

        Status = $"Попадание! Противников осталось: {remaining}.";
        RaiseChanged();
    }

    private void Move(double offsetX, double offsetY)
    {
        const double radius = 0.18;
        var nextX = _playerX + offsetX;
        var nextY = _playerY + offsetY;

        if (!IsWall(nextX + Math.Sign(offsetX) * radius, _playerY))
        {
            _playerX = nextX;
        }

        if (!IsWall(_playerX, nextY + Math.Sign(offsetY) * radius))
        {
            _playerY = nextY;
        }
    }

    private double CastRay(double rayAngle)
    {
        var distance = 0.04;
        while (distance < 20)
        {
            var x = _playerX + Math.Cos(rayAngle) * distance;
            var y = _playerY + Math.Sin(rayAngle) * distance;
            if (IsWall(x, y))
            {
                return distance;
            }

            distance += 0.035;
        }

        return 20;
    }

    private bool HasLineOfSight(Enemy enemy)
    {
        var angle = Math.Atan2(enemy.Y - _playerY, enemy.X - _playerX);
        var distance = Distance(_playerX, _playerY, enemy.X, enemy.Y);
        return CastRay(angle) + 0.25 >= distance;
    }

    private static bool IsWall(double x, double y)
    {
        var mapX = (int)Math.Floor(x);
        var mapY = (int)Math.Floor(y);
        return mapY < 0 || mapY >= Map.Length || mapX < 0 || mapX >= Map[mapY].Length
            || Map[mapY][mapX] == '#';
    }

    private void UpdateStatus()
    {
        var remaining = _enemies.Count(enemy => enemy.IsAlive);
        Status = $"Здоровье: {Math.Ceiling(_health):0} · Противников: {remaining}.";
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= Math.PI * 2;
        }

        while (angle < -Math.PI)
        {
            angle += Math.PI * 2;
        }

        return angle;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var x = x2 - x1;
        var y = y2 - y1;
        return Math.Sqrt(x * x + y * y);
    }

    private sealed class Enemy(double x, double y)
    {
        public double X { get; } = x;

        public double Y { get; } = y;

        public bool IsAlive { get; set; } = true;
    }
}
