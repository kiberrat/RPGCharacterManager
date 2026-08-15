using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace RPGCharacterManager.UI.Views.Documents.Games;

/// <summary>Каталог и игровая площадка встроенных мини-игр.</summary>
public sealed class ArcadeHub : UserControl
{
    private readonly Dictionary<ArcadeGameKind, ArcadeGameBase> _games;
    private readonly DispatcherTimer _timer;
    private readonly ArcadeSurface _surface;
    private readonly TextBlock _gameTitle;
    private readonly TextBlock _gameSubtitle;
    private readonly TextBlock _help;
    private readonly TextBlock _status;
    private readonly TextBlock _score;
    private readonly TextBlock _record;
    private readonly Button _startButton;
    private readonly Button _pauseButton;
    private ArcadeGameBase _active;
    private DateTimeOffset _lastTick;

    /// <summary>Создаёт каталог мини-игр.</summary>
    public ArcadeHub()
    {
        _games = new Dictionary<ArcadeGameKind, ArcadeGameBase>
        {
            [ArcadeGameKind.Snake] = new SnakeGame(),
            [ArcadeGameKind.Tetris] = new TetrisGame(),
            [ArcadeGameKind.Minesweeper] = new MinesweeperGame(),
            [ArcadeGameKind.Doom] = new DoomArenaGame(),
        };

        foreach (var game in _games.Values)
        {
            game.Changed += Refresh;
        }

        _active = _games[ArcadeGameKind.Snake];
        _surface = new ArcadeSurface
        {
            Game = _active,
            Focusable = true,
            MinHeight = 430,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _surface.KeyDown += OnKeyDown;
        _surface.KeyUp += OnKeyUp;
        _surface.PointerPressed += OnPointerPressed;

        _gameTitle = new TextBlock
        {
            FontSize = 21,
            FontWeight = FontWeight.SemiBold,
        };
        _gameSubtitle = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        };
        _help = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        };
        _status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
        };
        _score = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
        };
        _record = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
        };

        _startButton = new Button { Content = "Начать заново" };
        _startButton.Click += (_, _) =>
        {
            _active.Start();
            _surface.Focus();
        };

        _pauseButton = new Button { Content = "Пауза" };
        _pauseButton.Click += (_, _) =>
        {
            _active.TogglePause();
            _surface.Focus();
        };

        Content = BuildLayout();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _timer.Tick += OnTick;
        AttachedToVisualTree += (_, _) =>
        {
            _lastTick = DateTimeOffset.UtcNow;
            _timer.Start();
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();

        Select(ArcadeGameKind.Snake);
    }

    private Grid BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));

        var heading = new StackPanel { Spacing = 5 };
        heading.Children.Add(new TextBlock
        {
            Text = "Тишину навели",
            FontSize = 27,
            FontWeight = FontWeight.SemiBold,
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Небольшая передышка между приключениями. Выберите игру — она откроется здесь же.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var selector = new WrapPanel
        {
            Margin = new Thickness(0, 16, 0, 12),
            Orientation = Orientation.Horizontal,
        };

        AddGameButton(selector, "Змейка", ArcadeGameKind.Snake);
        AddGameButton(selector, "Тетрис", ArcadeGameKind.Tetris);
        AddGameButton(selector, "Сапёр", ArcadeGameKind.Minesweeper);
        AddGameButton(selector, "DOOM: открытая арена", ArcadeGameKind.Doom);
        Grid.SetRow(selector, 1);
        root.Children.Add(selector);

        var infoBorder = new Border
        {
            Padding = new Thickness(18, 14),
            Margin = new Thickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A3A3D")),
            Background = new SolidColorBrush(Color.Parse("#202024")),
        };

        var info = new Grid();
        info.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        info.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var description = new StackPanel { Spacing = 5 };
        description.Children.Add(_gameTitle);
        description.Children.Add(_gameSubtitle);
        description.Children.Add(_help);
        description.Children.Add(_status);
        info.Children.Add(description);

        var stats = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(24, 0, 0, 0),
            MinWidth = 190,
        };
        stats.Children.Add(CreateStat("Счёт", _score));
        stats.Children.Add(CreateStat("Рекорд", _record));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        actions.Children.Add(_startButton);
        actions.Children.Add(_pauseButton);
        stats.Children.Add(actions);
        Grid.SetColumn(stats, 1);
        info.Children.Add(stats);
        infoBorder.Child = info;
        Grid.SetRow(infoBorder, 2);
        root.Children.Add(infoBorder);

        var surfaceBorder = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#45454A")),
            Background = new SolidColorBrush(Color.Parse("#111217")),
            ClipToBounds = true,
            Child = _surface,
        };
        Grid.SetRow(surfaceBorder, 3);
        root.Children.Add(surfaceBorder);

        return root;
    }

    private void AddGameButton(Panel panel, string title, ArcadeGameKind kind)
    {
        var button = new Button
        {
            Content = title,
            Margin = new Thickness(0, 0, 8, 8),
        };
        button.Click += (_, _) => Select(kind);
        panel.Children.Add(button);
    }

    private static Grid CreateStat(string title, Control value)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.Children.Add(new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
        });
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        return row;
    }

    private void Select(ArcadeGameKind kind)
    {
        _active = _games[kind];
        _surface.Game = _active;
        _surface.InvalidateVisual();
        Refresh();
        _surface.Focus();
    }

    private void Refresh()
    {
        _gameTitle.Text = _active.Title;
        _gameSubtitle.Text = _active.Subtitle;
        _help.Text = _active.Help;
        _status.Text = _active.Status;
        _score.Text = _active.Score.ToString(CultureInfo.InvariantCulture);
        _record.Text = _active.BestScore.ToString(CultureInfo.InvariantCulture);
        _pauseButton.IsEnabled = _active.IsRunning;
        _pauseButton.Content = _active.IsPaused ? "Продолжить" : "Пауза";
        _surface.InvalidateVisual();
    }

    private void OnTick(object? sender, EventArgs args)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = now - _lastTick;
        _lastTick = now;
        _active.Tick(elapsed);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        _active.KeyDown(args.Key);
        args.Handled = args.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Space;
    }

    private void OnKeyUp(object? sender, KeyEventArgs args) => _active.KeyUp(args.Key);

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        _surface.Focus();
        var point = args.GetCurrentPoint(_surface);
        var right = point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;
        _active.Pointer(point.Position, _surface.Bounds.Size, right);
        args.Handled = true;
    }
}

internal sealed class ArcadeSurface : Control
{
    public ArcadeGameBase? Game { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Game?.Render(context, new Rect(Bounds.Size));
    }
}
