using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace RPGCharacterManager.UI.Views.Documents.Games;

internal sealed class TetrisGame : ArcadeGameBase
{
    private const int Columns = 10;
    private const int Rows = 20;
    private static readonly Color[] BlockColors =
    [
        Color.Parse("#00000000"),
        Color.Parse("#5AC8FA"),
        Color.Parse("#FFD60A"),
        Color.Parse("#BF5AF2"),
        Color.Parse("#30D158"),
        Color.Parse("#FF453A"),
        Color.Parse("#0A84FF"),
        Color.Parse("#FF9F0A"),
    ];

    private static readonly (int X, int Y)[][] Shapes =
    [
        [(-1, 0), (0, 0), (1, 0), (2, 0)],
        [(0, 0), (1, 0), (0, 1), (1, 1)],
        [(-1, 0), (0, 0), (1, 0), (0, 1)],
        [(0, 0), (1, 0), (-1, 1), (0, 1)],
        [(-1, 0), (0, 0), (0, 1), (1, 1)],
        [(-1, 0), (0, 0), (1, 0), (-1, 1)],
        [(-1, 0), (0, 0), (1, 0), (1, 1)],
    ];

    private readonly Random _random = new();
    private readonly int[,] _board = new int[Rows, Columns];
    private int _piece;
    private int _rotation;
    private int _pieceX;
    private int _pieceY;
    private TimeSpan _accumulator;

    public TetrisGame()
        : base(
            ArcadeGameKind.Tetris,
            "Тетрис",
            "Собирайте полные горизонтальные линии.",
            "← → — движение · ↓ — ускорить · ↑ — поворот · Пробел — сброс · P — пауза")
    {
    }

    public override void Start()
    {
        Array.Clear(_board);
        ResetScore();
        _accumulator = TimeSpan.Zero;
        IsRunning = true;
        IsPaused = false;
        Status = "Игра идёт.";
        Spawn();
        RaiseChanged();
    }

    public override void Tick(TimeSpan elapsed)
    {
        if (!IsRunning || IsPaused)
        {
            return;
        }

        _accumulator += elapsed;
        var delay = TimeSpan.FromMilliseconds(Math.Max(90, 480 - Score / 8));
        if (_accumulator < delay)
        {
            return;
        }

        _accumulator -= delay;
        if (!Move(0, 1))
        {
            LockPiece();
        }

        RaiseChanged();
    }

    public override void KeyDown(Key key)
    {
        if (!IsRunning)
        {
            return;
        }

        switch (key)
        {
            case Key.Left:
            case Key.A:
                Move(-1, 0);
                break;
            case Key.Right:
            case Key.D:
                Move(1, 0);
                break;
            case Key.Down:
            case Key.S:
                if (Move(0, 1))
                {
                    AddScore(1);
                }
                break;
            case Key.Up:
            case Key.W:
                Rotate();
                break;
            case Key.Space:
                var distance = 0;
                while (Move(0, 1))
                {
                    distance++;
                }

                AddScore(distance * 2);
                LockPiece();
                break;
            case Key.P:
                TogglePause();
                break;
        }

        RaiseChanged();
    }

    public override void Render(DrawingContext context, Rect bounds)
    {
        Fill(context, bounds, Color.Parse("#111217"));

        var cell = Math.Min(bounds.Width / Columns, bounds.Height / Rows);
        var width = cell * Columns;
        var height = cell * Rows;
        var origin = new Point(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2);

        Cell(context, new Rect(origin, new Size(width, height)),
            Color.Parse("#171820"), Color.Parse("#454650"), 2);

        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                if (_board[y, x] != 0)
                {
                    DrawBlock(context, origin, cell, x, y, _board[y, x]);
                }
            }
        }

        if (IsRunning)
        {
            foreach (var point in PieceCells(_pieceX, _pieceY, _rotation))
            {
                if (point.Y >= 0)
                {
                    DrawBlock(context, origin, cell, point.X, point.Y, _piece + 1);
                }
            }
        }
    }

    private bool Move(int offsetX, int offsetY)
    {
        if (Collides(_pieceX + offsetX, _pieceY + offsetY, _rotation))
        {
            return false;
        }

        _pieceX += offsetX;
        _pieceY += offsetY;
        return true;
    }

    private void Rotate()
    {
        if (_piece == 1)
        {
            return;
        }

        var next = (_rotation + 1) % 4;
        foreach (var kick in new[] { 0, -1, 1, -2, 2 })
        {
            if (!Collides(_pieceX + kick, _pieceY, next))
            {
                _pieceX += kick;
                _rotation = next;
                return;
            }
        }
    }

    private void LockPiece()
    {
        foreach (var point in PieceCells(_pieceX, _pieceY, _rotation))
        {
            if (point.Y < 0)
            {
                Finish($"Игра окончена. Счёт: {Score}.");
                return;
            }

            _board[point.Y, point.X] = _piece + 1;
        }

        ClearLines();
        Spawn();
    }

    private void ClearLines()
    {
        var cleared = 0;
        for (var y = Rows - 1; y >= 0; y--)
        {
            var full = true;
            for (var x = 0; x < Columns; x++)
            {
                full &= _board[y, x] != 0;
            }

            if (!full)
            {
                continue;
            }

            cleared++;
            for (var copyY = y; copyY > 0; copyY--)
            {
                for (var x = 0; x < Columns; x++)
                {
                    _board[copyY, x] = _board[copyY - 1, x];
                }
            }

            for (var x = 0; x < Columns; x++)
            {
                _board[0, x] = 0;
            }

            y++;
        }

        var bonus = cleared switch
        {
            1 => 100,
            2 => 300,
            3 => 500,
            4 => 800,
            _ => 0,
        };

        if (bonus > 0)
        {
            AddScore(bonus);
            Status = $"Линий убрано: {cleared}.";
        }
    }

    private void Spawn()
    {
        _piece = _random.Next(Shapes.Length);
        _rotation = 0;
        _pieceX = Columns / 2;
        _pieceY = 1;

        if (Collides(_pieceX, _pieceY, _rotation))
        {
            Finish($"Игра окончена. Счёт: {Score}.");
        }
    }

    private bool Collides(int originX, int originY, int rotation)
    {
        foreach (var point in PieceCells(originX, originY, rotation))
        {
            if (point.X < 0 || point.X >= Columns || point.Y >= Rows
                || point.Y >= 0 && _board[point.Y, point.X] != 0)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<(int X, int Y)> PieceCells(int originX, int originY, int rotation)
    {
        foreach (var source in Shapes[_piece])
        {
            var point = source;
            for (var turn = 0; turn < rotation; turn++)
            {
                point = (-point.Y, point.X);
            }

            yield return (originX + point.X, originY + point.Y);
        }
    }

    private static void DrawBlock(
        DrawingContext context,
        Point origin,
        double cell,
        int x,
        int y,
        int colorIndex)
    {
        Cell(context,
            new Rect(origin.X + x * cell + 1, origin.Y + y * cell + 1, cell - 2, cell - 2),
            BlockColors[colorIndex], Color.Parse("#88FFFFFF"));
    }
}
