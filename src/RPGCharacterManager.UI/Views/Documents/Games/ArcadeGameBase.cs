using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace RPGCharacterManager.UI.Views.Documents.Games;

internal enum ArcadeGameKind
{
    Snake,
    Tetris,
    Minesweeper,
    Doom,
}

internal abstract class ArcadeGameBase
{
    private static readonly Typeface GameTypeface = new("Segoe UI");
    private int _score;

    protected ArcadeGameBase(ArcadeGameKind kind, string title, string subtitle, string help)
    {
        Kind = kind;
        Title = title;
        Subtitle = subtitle;
        Help = help;
        BestScore = ArcadeRecords.Get(kind);
    }

    public event Action? Changed;

    public ArcadeGameKind Kind { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Help { get; }

    public string Status { get; protected set; } = "Нажмите «Начать».";

    public int Score
    {
        get => _score;
        protected set
        {
            _score = Math.Max(0, value);
            if (_score > BestScore)
            {
                BestScore = _score;
                ArcadeRecords.Set(Kind, BestScore);
            }
        }
    }

    public int BestScore { get; private set; }

    public bool IsRunning { get; protected set; }

    public bool IsPaused { get; protected set; }

    public abstract void Start();

    public virtual void Tick(TimeSpan elapsed)
    {
    }

    public virtual void KeyDown(Key key)
    {
    }

    public virtual void KeyUp(Key key)
    {
    }

    public virtual void Pointer(Point point, Size size, bool rightButton)
    {
    }

    public abstract void Render(DrawingContext context, Rect bounds);

    public void TogglePause()
    {
        if (!IsRunning)
        {
            return;
        }

        IsPaused = !IsPaused;
        Status = IsPaused ? "Пауза." : "Игра продолжается.";
        RaiseChanged();
    }

    protected void ResetScore() => Score = 0;

    protected void AddScore(int value) => Score += value;

    protected void Finish(string status)
    {
        IsRunning = false;
        IsPaused = false;
        Status = status;
        RaiseChanged();
    }

    protected void RaiseChanged() => Changed?.Invoke();

    protected static void Fill(DrawingContext context, Rect bounds, Color color) =>
        context.DrawRectangle(new SolidColorBrush(color), null, bounds);

    protected static void Cell(
        DrawingContext context,
        Rect rect,
        Color fill,
        Color? border = null,
        double borderWidth = 1)
    {
        context.DrawRectangle(
            new SolidColorBrush(fill),
            border is null ? null : new Pen(new SolidColorBrush(border.Value), borderWidth),
            rect);
    }

    protected static void Text(
        DrawingContext context,
        string value,
        Point point,
        double size,
        Color color,
        FontWeight? weight = null)
    {
        var formatted = new FormattedText(
            value,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(GameTypeface.FontFamily, FontStyle.Normal, weight ?? FontWeight.Normal),
            size,
            new SolidColorBrush(color));

        context.DrawText(formatted, point);
    }
}

internal static class ArcadeRecords
{
    private static readonly object Sync = new();
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RPGCharacterManager",
        "arcade-records.json");

    private static Dictionary<ArcadeGameKind, int>? _values;

    public static int Get(ArcadeGameKind kind)
    {
        lock (Sync)
        {
            EnsureLoaded();
            return _values!.GetValueOrDefault(kind);
        }
    }

    public static void Set(ArcadeGameKind kind, int score)
    {
        lock (Sync)
        {
            EnsureLoaded();
            var values = _values!;
            if (values.GetValueOrDefault(kind) >= score)
            {
                return;
            }

            values[kind] = score;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(values));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // Отсутствие доступа к рекордам не должно мешать самой игре.
            }
        }
    }

    private static void EnsureLoaded()
    {
        if (_values is not null)
        {
            return;
        }

        try
        {
            _values = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<ArcadeGameKind, int>>(
                    File.ReadAllText(FilePath)) ?? []
                : [];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _values = [];
        }
    }
}
