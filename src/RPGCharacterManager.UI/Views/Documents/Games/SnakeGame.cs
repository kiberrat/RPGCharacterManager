using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace RPGCharacterManager.UI.Views.Documents.Games;

internal sealed class SnakeGame : ArcadeGameBase
{
    private const int Columns = 24;
    private const int Rows = 16;
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(115);

    private readonly Random _random = new();
    private readonly List<(int X, int Y)> _snake = [];
    private (int X, int Y) _food;
    private (int X, int Y) _direction = (1, 0);
    private (int X, int Y) _nextDirection = (1, 0);
    private TimeSpan _accumulator;

    public SnakeGame()
        : base(
            ArcadeGameKind.Snake,
            "Змейка",
            "Собирайте яблоки и не врезайтесь в стены или собственный хвост.",
            "Стрелки или WASD — движение · P — пауза")
    {
    }

    public override void Start()
    {
        _snake.Clear();
        _snake.AddRange([(7, 8), (6, 8), (5, 8), (4, 8)]);
        _direction = (1, 0);
        _nextDirection = _direction;
        _accumulator = TimeSpan.Zero;
        ResetScore();
        PlaceFood();
        IsRunning = true;
        IsPaused = false;
        Status = "Игра идёт.";
        RaiseChanged();
    }

    public override void Tick(TimeSpan elapsed)
    {
        if (!IsRunning || IsPaused)
        {
            return;
        }

        _accumulator += elapsed;
        if (_accumulator < StepDelay)
        {
            return;
        }

        _accumulator -= StepDelay;
        _direction = _nextDirection;
        var head = _snake[0];
        var next = (head.X + _direction.X, head.Y + _direction.Y);

        if (next.Item1 < 0 || next.Item1 >= Columns || next.Item2 < 0 || next.Item2 >= Rows
            || _snake.Contains(next))
        {
            Finish($"Игра окончена. Счёт: {Score}.");
            return;
        }

        _snake.Insert(0, next);
        if (next == _food)
        {
            AddScore(10);
            PlaceFood();
            Status = $"Яблоко съедено. Длина: {_snake.Count}.";
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }

        RaiseChanged();
    }

    public override void KeyDown(Key key)
    {
        var proposed = key switch
        {
            Key.Up or Key.W => (0, -1),
            Key.Down or Key.S => (0, 1),
            Key.Left or Key.A => (-1, 0),
            Key.Right or Key.D => (1, 0),
            _ => _nextDirection,
        };

        if (key == Key.P)
        {
            TogglePause();
            return;
        }

        if (proposed.Item1 != -_direction.X || proposed.Item2 != -_direction.Y)
        {
            _nextDirection = proposed;
        }
    }

    public override void Render(DrawingContext context, Rect bounds)
    {
        Fill(context, bounds, Color.Parse("#101812"));

        var cell = Math.Min(bounds.Width / Columns, bounds.Height / Rows);
        var width = cell * Columns;
        var height = cell * Rows;
        var origin = new Point(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2);

        Cell(
            context,
            new Rect(origin, new Size(width, height)),
            Color.Parse("#151F18"),
            Color.Parse("#34523B"));

        for (var index = 0; index < _snake.Count; index++)
        {
            var part = _snake[index];
            var color = index == 0 ? Color.Parse("#86F29B") : Color.Parse("#34C759");
            Cell(
                context,
                new Rect(origin.X + part.X * cell + 1, origin.Y + part.Y * cell + 1, cell - 2, cell - 2),
                color);
        }

        Cell(
            context,
            new Rect(origin.X + _food.X * cell + 2, origin.Y + _food.Y * cell + 2, cell - 4, cell - 4),
            Color.Parse("#FF453A"));
    }

    private void PlaceFood()
    {
        do
        {
            _food = (_random.Next(Columns), _random.Next(Rows));
        }
        while (_snake.Contains(_food));
    }
}
