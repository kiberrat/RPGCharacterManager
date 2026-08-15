using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace RPGCharacterManager.UI.Views.Documents.Games;

internal sealed class MinesweeperGame : ArcadeGameBase
{
    private const int Columns = 14;
    private const int Rows = 10;
    private const int MineCount = 22;

    private readonly Random _random = new();
    private readonly bool[,] _mines = new bool[Rows, Columns];
    private readonly bool[,] _revealed = new bool[Rows, Columns];
    private readonly bool[,] _flags = new bool[Rows, Columns];
    private TimeSpan _secondAccumulator;
    private int _elapsedSeconds;
    private bool _generated;

    public MinesweeperGame()
        : base(
            ArcadeGameKind.Minesweeper,
            "Сапёр",
            "Откройте все безопасные клетки, отмечая предполагаемые мины.",
            "Левая кнопка — открыть · Правая кнопка — флаг · P — пауза")
    {
    }

    public override void Start()
    {
        Array.Clear(_mines);
        Array.Clear(_revealed);
        Array.Clear(_flags);
        _secondAccumulator = TimeSpan.Zero;
        _elapsedSeconds = 0;
        _generated = false;
        ResetScore();
        IsRunning = true;
        IsPaused = false;
        Status = $"Мин: {MineCount}. Первый ход всегда безопасен.";
        RaiseChanged();
    }

    public override void Tick(TimeSpan elapsed)
    {
        if (!IsRunning || IsPaused)
        {
            return;
        }

        _secondAccumulator += elapsed;
        if (_secondAccumulator < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _secondAccumulator -= TimeSpan.FromSeconds(1);
        _elapsedSeconds++;
        Status = $"Мин: {MineCount}. Время: {_elapsedSeconds} сек.";
        RaiseChanged();
    }

    public override void KeyDown(Avalonia.Input.Key key)
    {
        if (key == Avalonia.Input.Key.P)
        {
            TogglePause();
        }
    }

    public override void Pointer(Point point, Size size, bool rightButton)
    {
        if (!IsRunning || IsPaused)
        {
            return;
        }

        var geometry = BoardGeometry(new Rect(size));
        if (!geometry.Board.Contains(point))
        {
            return;
        }

        var x = Math.Clamp((int)((point.X - geometry.Board.X) / geometry.Cell), 0, Columns - 1);
        var y = Math.Clamp((int)((point.Y - geometry.Board.Y) / geometry.Cell), 0, Rows - 1);

        if (rightButton)
        {
            if (!_revealed[y, x])
            {
                _flags[y, x] = !_flags[y, x];
                Status = $"Флагов: {CountFlags()} из {MineCount}.";
            }

            RaiseChanged();
            return;
        }

        if (_flags[y, x] || _revealed[y, x])
        {
            return;
        }

        if (!_generated)
        {
            Generate(x, y);
        }

        if (_mines[y, x])
        {
            RevealAllMines();
            Finish($"Мина! Счёт: {Score}. Время: {_elapsedSeconds} сек.");
            return;
        }

        RevealArea(x, y);
        Score = CountRevealed() * 5;

        if (CountRevealed() == Rows * Columns - MineCount)
        {
            AddScore(Math.Max(100, 1200 - _elapsedSeconds * 8));
            Finish($"Поле очищено за {_elapsedSeconds} сек. Счёт: {Score}.");
            return;
        }

        RaiseChanged();
    }

    public override void Render(DrawingContext context, Rect bounds)
    {
        Fill(context, bounds, Color.Parse("#17191D"));
        var geometry = BoardGeometry(bounds);

        Cell(context, geometry.Board, Color.Parse("#202329"), Color.Parse("#656870"), 2);

        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                var rect = new Rect(
                    geometry.Board.X + x * geometry.Cell + 1,
                    geometry.Board.Y + y * geometry.Cell + 1,
                    geometry.Cell - 2,
                    geometry.Cell - 2);

                if (_revealed[y, x])
                {
                    Cell(context, rect, Color.Parse("#34373D"), Color.Parse("#24262B"));
                    if (_mines[y, x])
                    {
                        context.DrawEllipse(
                            new SolidColorBrush(Color.Parse("#FF453A")),
                            null,
                            rect.Center,
                            geometry.Cell * 0.22,
                            geometry.Cell * 0.22);
                    }
                    else
                    {
                        var adjacent = AdjacentMines(x, y);
                        if (adjacent > 0)
                        {
                            var color = adjacent switch
                            {
                                1 => Color.Parse("#64D2FF"),
                                2 => Color.Parse("#30D158"),
                                3 => Color.Parse("#FF9F0A"),
                                _ => Color.Parse("#FF453A"),
                            };
                            Text(context, adjacent.ToString(CultureInfo.InvariantCulture), new Point(
                                rect.X + geometry.Cell * 0.35,
                                rect.Y + geometry.Cell * 0.16),
                                Math.Max(11, geometry.Cell * 0.48), color, FontWeight.Bold);
                        }
                    }
                }
                else
                {
                    Cell(context, rect, Color.Parse("#4A4D54"), Color.Parse("#686B73"));
                    if (_flags[y, x])
                    {
                        var flag = new Rect(
                            rect.X + geometry.Cell * 0.28,
                            rect.Y + geometry.Cell * 0.22,
                            geometry.Cell * 0.46,
                            geometry.Cell * 0.5);
                        Cell(context, flag, Color.Parse("#FF453A"));
                    }
                }
            }
        }
    }

    private static (Rect Board, double Cell) BoardGeometry(Rect bounds)
    {
        var cell = Math.Min(bounds.Width / Columns, bounds.Height / Rows);
        var board = new Rect(
            bounds.X + (bounds.Width - cell * Columns) / 2,
            bounds.Y + (bounds.Height - cell * Rows) / 2,
            cell * Columns,
            cell * Rows);
        return (board, cell);
    }

    private void Generate(int safeX, int safeY)
    {
        _generated = true;
        var placed = 0;
        while (placed < MineCount)
        {
            var x = _random.Next(Columns);
            var y = _random.Next(Rows);
            if (_mines[y, x] || Math.Abs(x - safeX) <= 1 && Math.Abs(y - safeY) <= 1)
            {
                continue;
            }

            _mines[y, x] = true;
            placed++;
        }
    }

    private void RevealArea(int startX, int startY)
    {
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (point.X < 0 || point.X >= Columns || point.Y < 0 || point.Y >= Rows
                || _revealed[point.Y, point.X] || _flags[point.Y, point.X]
                || _mines[point.Y, point.X])
            {
                continue;
            }

            _revealed[point.Y, point.X] = true;
            if (AdjacentMines(point.X, point.Y) != 0)
            {
                continue;
            }

            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    queue.Enqueue((point.X + offsetX, point.Y + offsetY));
                }
            }
        }
    }

    private int AdjacentMines(int x, int y)
    {
        var count = 0;
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var checkX = x + offsetX;
                var checkY = y + offsetY;
                if (checkX >= 0 && checkX < Columns && checkY >= 0 && checkY < Rows
                    && _mines[checkY, checkX])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int CountRevealed()
    {
        var count = 0;
        foreach (var revealed in _revealed)
        {
            count += revealed ? 1 : 0;
        }

        return count;
    }

    private int CountFlags()
    {
        var count = 0;
        foreach (var flag in _flags)
        {
            count += flag ? 1 : 0;
        }

        return count;
    }

    private void RevealAllMines()
    {
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                _revealed[y, x] |= _mines[y, x];
            }
        }
    }
}
