using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using RPGCharacterManager.Core.Abstractions.Dice;
using RPGCharacterManager.Core.Models.Dice;

namespace RPGCharacterManager.UI.Controls;

/// <summary>
/// Сцена броска: летящие, вращающиеся и падающие кубики.
///
/// Кубик показан настоящим телом — тетраэдром, кубом, трапецоэдром, икосаэдром или
/// шаром, — которое поворачивается в пространстве, а не набором заранее нарисованных
/// кадров. Поэтому любой кубик, включая созданный пользователем с произвольным числом
/// граней, бросается одинаково: форму даёт <see cref="DieMesh"/>, а сцена показывает её.
///
/// Исход броска известен до начала полёта: его вычислила служба бросков. Полёт —
/// показ уже полученного результата, поэтому кубик приземляется нужной гранью
/// к зрителю, а отключение полёта ничего не меняет в игре.
/// </summary>
public sealed class DiceTray : Control
{
    /// <summary>Длительность кувыркания кубика.</summary>
    private static readonly TimeSpan TumbleDuration = TimeSpan.FromMilliseconds(950);

    /// <summary>Длительность доворота кубика нужной гранью к зрителю.</summary>
    private static readonly TimeSpan SettleDuration = TimeSpan.FromMilliseconds(420);

    /// <summary>Задержка между началом полёта соседних кубиков.</summary>
    private static readonly TimeSpan CascadeDelay = TimeSpan.FromMilliseconds(70);

    /// <summary>
    /// Наибольшее количество кубиков, вылетающих друг за другом.
    /// Бросок из десятков костей иначе тянулся бы недопустимо долго.
    /// </summary>
    private const int MaximumCascade = 6;

    /// <summary>Частота обновления сцены: примерно шестьдесят кадров в секунду.</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>Расстояние от зрителя до центра сцены в радиусах кубика.</summary>
    private const float CameraDistance = 4.2f;

    /// <summary>Высота, с которой падает кубик, в его радиусах.</summary>
    private const double DropHeight = 2.6;

    /// <summary>Количество касаний поверхности за время падения.</summary>
    private const double BounceCount = 2.5;

    /// <summary>Во сколько раз затухает подскок к концу падения.</summary>
    private const double BounceDecay = 40;

    /// <summary>Скорость вращения кубика в оборотах за секунду в начале полёта.</summary>
    private const float SpinSpeed = 2.4f;

    /// <summary>Затухание вращения: к концу кувыркания кубик почти останавливается.</summary>
    private const float SpinDecay = 2.6f;

    /// <summary>Наибольшее смещение кубика вбок при падении, в его радиусах.</summary>
    private const double DriftRange = 0.9;

    /// <summary>
    /// Наклон кубика после приземления.
    ///
    /// Точно перпендикулярная зрителю грань выглядит плоским квадратом: кубик
    /// перестаёт читаться как тело. Небольшой наклон оставляет число полностью
    /// видимым и возвращает предмету объём.
    /// </summary>
    private const float LandingTilt = 0.26f;

    /// <summary>Направление на источник света.</summary>
    private static readonly Vector3 LightDirection =
        Vector3.Normalize(new Vector3(-0.35f, 0.75f, 0.55f));

    /// <summary>Наименьшая освещённость грани: теневая сторона не должна быть чёрной.</summary>
    private const double AmbientLight = 0.42;

    /// <summary>Во сколько раз темнее цвет кубика на неосвещённой грани.</summary>
    private const double ShadowFactor = 0.45;

    /// <summary>Насколько освещённая грань высветляется к белому.</summary>
    private const double HighlightMix = 0.30;

    /// <summary>Количество заготовленных оттенков грани.</summary>
    private const int ShadeCount = 32;

    /// <summary>Размер шрифта, которым готовятся числа граней.</summary>
    private const double LabelBaseSize = 48;

    /// <summary>Наименьшая обращённость грани к зрителю, при которой видно число.</summary>
    private const double LabelVisibility = 0.45;

    /// <summary>Обращённость грани, при которой число показано полностью.</summary>
    private const double LabelFullVisibility = 0.82;

    /// <summary>Отношение размера числа к размеру грани.</summary>
    private const double LabelScale = 1.5;

    /// <summary>Во сколько раз выпавшее число крупнее остальных.</summary>
    private const double ResultScale = 1.18;

    /// <summary>Толщина обводки выпавшего числа.</summary>
    private const double ResultOutline = 2.2;

    /// <summary>Насыщенность цвета выпавшего числа.</summary>
    private const double ResultSaturation = 0.85;

    /// <summary>
    /// Наименьшая насыщенность кубика, при которой противоположный оттенок
    /// ещё имеет смысл. У серого кубика оттенка нет, и его нечему противопоставить.
    /// </summary>
    private const double ColorfulEnough = 0.15;

    /// <summary>Цвет выпавшего числа на кубике без собственного оттенка.</summary>
    private static readonly Color NeutralResultColor = Color.FromRgb(0xFF, 0xD2, 0x4A);

    /// <summary>Доля ячейки, которую занимает кубик.</summary>
    private const double CellFill = 0.40;

    private readonly List<FlyingDie> _dice = [];
    private readonly List<VisibleFace> _visible = [];
    private readonly List<Point> _polygon = [];
    private readonly Dictionary<string, FormattedText> _labels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Geometry> _outlines = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private ImmutableSolidColorBrush[]? _shades;
    private ImmutableSolidColorBrush? _shadow;
    private ImmutableSolidColorBrush? _result;
    private IPen? _edge;
    private IPen? _resultOutline;
    private TimeSpan _duration;

    /// <summary>Выпавшие кубики, показываемые на сцене.</summary>
    public static readonly StyledProperty<IReadOnlyList<DieCast>?> CastsProperty =
        AvaloniaProperty.Register<DiceTray, IReadOnlyList<DieCast>?>(nameof(Casts));

    /// <summary>Показывать полёт кубика. Без него кубик появляется сразу приземлившимся.</summary>
    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<DiceTray, bool>(nameof(IsAnimated), true);

    /// <summary>Цвет кубика по умолчанию: обычно акцентный цвет оформления.</summary>
    public static readonly StyledProperty<Color> DieColorProperty =
        AvaloniaProperty.Register<DiceTray, Color>(nameof(DieColor), Color.FromRgb(0x0A, 0x84, 0xFF));

    /// <summary>
    /// Цвет брошенного кубика, заданный пользователем при его создании.
    /// Пустое значение означает, что кубик рисуется цветом оформления.
    /// </summary>
    public static readonly StyledProperty<Color?> CustomColorProperty =
        AvaloniaProperty.Register<DiceTray, Color?>(nameof(CustomColor));

    /// <summary>
    /// Создаёт сцену броска.
    /// </summary>
    public DiceTray()
    {
        // Кубик уходит выше края сцены: без отсечения он рисовался бы поверх
        // соседних частей панели.
        ClipToBounds = true;

        // Кадры отсчитывает таймер, а не анимация свойства: положение и поворот
        // кубика задаёт расчёт полёта, и промежуточные значения интерполировать нечем.
        _timer = new DispatcherTimer(FrameInterval, DispatcherPriority.Render, OnFrame);
    }

    /// <summary>Выпавшие кубики, показываемые на сцене.</summary>
    public IReadOnlyList<DieCast>? Casts
    {
        get => GetValue(CastsProperty);
        set => SetValue(CastsProperty, value);
    }

    /// <summary>Показывать полёт кубика.</summary>
    public bool IsAnimated
    {
        get => GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    /// <summary>Цвет кубика по умолчанию.</summary>
    public Color DieColor
    {
        get => GetValue(DieColorProperty);
        set => SetValue(DieColorProperty, value);
    }

    /// <summary>Цвет брошенного кубика, заданный пользователем при его создании.</summary>
    public Color? CustomColor
    {
        get => GetValue(CustomColorProperty);
        set => SetValue(CustomColorProperty, value);
    }

    /// <summary>Перо, которым обводятся рёбра граней.</summary>
    private IPen Edge
    {
        get
        {
            const double EdgeOpacity = 0.35;
            const double EdgeThickness = 1;

            return _edge ??= new ImmutablePen(
                new ImmutableSolidColorBrush(Colors.White, EdgeOpacity),
                EdgeThickness);
        }
    }

    /// <summary>
    /// Возвращает длительность полёта указанного количества кубиков.
    ///
    /// Модель представления ждёт это время, прежде чем объявить итог броска:
    /// иначе число появилось бы раньше, чем кубик успел его показать.
    /// </summary>
    /// <param name="count">Количество кубиков в броске.</param>
    /// <returns>Длительность полёта.</returns>
    public static TimeSpan DurationOf(int count) =>
        TumbleDuration + SettleDuration + (CascadeDelay * Math.Clamp(count - 1, 0, MaximumCascade));

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Bounds.Size;

        if (_dice.Count == 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        // Пока таймер работает, время берётся у секундомера; после остановки
        // кубики лежат, и время равно полной длительности полёта. Тот же путь
        // приводит к лежащим кубикам при отключённой анимации.
        var elapsed = _timer.IsEnabled ? _clock.Elapsed : _duration;

        var columns = Math.Clamp(
            (int)Math.Ceiling(Math.Sqrt(_dice.Count * size.Width / size.Height)),
            1,
            _dice.Count);

        var rows = (int)Math.Ceiling(_dice.Count / (double)columns);
        var cellWidth = size.Width / columns;
        var cellHeight = size.Height / rows;
        var radius = Math.Min(cellWidth, cellHeight) * CellFill;

        for (var index = 0; index < _dice.Count; index++)
        {
            var cell = new Point(
                ((index % columns) + 0.5) * cellWidth,
                ((index / columns) + 0.5) * cellHeight);

            Draw(context, _dice[index], cell, radius, elapsed);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CastsProperty)
        {
            Throw(change.GetNewValue<IReadOnlyList<DieCast>?>());
            return;
        }

        if (change.Property == DieColorProperty || change.Property == CustomColorProperty)
        {
            // Цвет чисел подбирается по цвету кубика, поэтому надписи готовятся заново.
            _shades = null;
            _result = null;
            _resultOutline = null;
            _labels.Clear();
            InvalidateVisual();
            return;
        }

        if (change.Property == TextElement.FontFamilyProperty)
        {
            _labels.Clear();
            _outlines.Clear();
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Таймер продолжал бы будить поток интерфейса и после закрытия панели.
        _timer.Stop();
        _clock.Stop();

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Начинает новый бросок.
    /// </summary>
    /// <param name="casts">Выпавшие кубики.</param>
    private void Throw(IReadOnlyList<DieCast>? casts)
    {
        _timer.Stop();
        _clock.Reset();
        _dice.Clear();

        if (casts is not null)
        {
            for (var index = 0; index < casts.Count; index++)
            {
                _dice.Add(FlyingDie.Create(casts[index], index));
            }
        }

        _duration = DurationOf(_dice.Count);

        if (_dice.Count > 0 && IsAnimated)
        {
            _clock.Start();
            _timer.Start();
        }

        InvalidateVisual();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_clock.Elapsed >= _duration)
        {
            _timer.Stop();
            _clock.Stop();
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Рисует один кубик.
    /// </summary>
    /// <param name="context">Контекст отрисовки.</param>
    /// <param name="die">Летящий кубик.</param>
    /// <param name="cell">Центр ячейки, в которой лежит кубик.</param>
    /// <param name="radius">Радиус кубика на экране.</param>
    /// <param name="elapsed">Время с начала броска.</param>
    private void Draw(DrawingContext context, FlyingDie die, Point cell, double radius, TimeSpan elapsed)
    {
        var time = (elapsed - die.Delay).TotalSeconds;
        var height = die.HeightAt(time);
        var orientation = die.OrientationAt(time);
        var flight = Math.Clamp(1 - (time / TumbleDuration.TotalSeconds), 0, 1);
        var landed = FlyingDie.HasLanded(time);

        var center = new Point(
            cell.X + (die.Drift * radius * flight),
            cell.Y - (height * radius));

        DrawShadow(context, cell, radius, height);

        _visible.Clear();

        for (var index = 0; index < die.Mesh.Faces.Count; index++)
        {
            var face = die.Mesh.Faces[index];
            var normal = Vector3.Transform(face.Normal, orientation);

            // Грань, отвёрнутая от зрителя, закрыта самим телом кубика.
            if (normal.Z > 0)
            {
                _visible.Add(new VisibleFace(
                    index,
                    Vector3.Transform(face.Center, orientation).Z,
                    normal));
            }
        }

        // Дальние грани рисуются первыми и закрываются ближними.
        _visible.Sort(static (left, right) => left.Depth.CompareTo(right.Depth));

        foreach (var visible in _visible)
        {
            DrawFace(context, die, visible, orientation, center, radius, landed);
        }
    }

    /// <summary>
    /// Рисует тень кубика на поверхности.
    /// </summary>
    /// <param name="context">Контекст отрисовки.</param>
    /// <param name="cell">Центр ячейки.</param>
    /// <param name="radius">Радиус кубика на экране.</param>
    /// <param name="height">Высота кубика над поверхностью в его радиусах.</param>
    private void DrawShadow(DrawingContext context, Point cell, double radius, double height)
    {
        const double GroundOpacity = 0.35;
        const double Fading = 0.8;
        const double Spreading = 0.25;
        const double Flatness = 0.28;

        // Чем выше кубик, тем тень бледнее и шире. Без этого падение читается
        // как движение по плоскости, а не сверху вниз.
        var fade = 1 / (1 + (height * Fading));
        var size = radius * (0.95 + (height * Spreading));

        _shadow ??= new ImmutableSolidColorBrush(Colors.Black);

        using (context.PushOpacity(GroundOpacity * fade))
        {
            context.DrawEllipse(
                _shadow,
                null,
                new Point(cell.X, cell.Y + (radius * 1.05)),
                size,
                size * Flatness);
        }
    }

    /// <summary>
    /// Рисует одну грань вместе с её числом.
    /// </summary>
    /// <param name="context">Контекст отрисовки.</param>
    /// <param name="die">Летящий кубик.</param>
    /// <param name="visible">Видимая грань.</param>
    /// <param name="orientation">Поворот кубика.</param>
    /// <param name="center">Центр кубика на экране.</param>
    /// <param name="radius">Радиус кубика на экране.</param>
    /// <param name="landed">Кубик уже улёгся.</param>
    private void DrawFace(
        DrawingContext context,
        FlyingDie die,
        VisibleFace visible,
        Quaternion orientation,
        Point center,
        double radius,
        bool landed)
    {
        var face = die.Mesh.Faces[visible.Index];

        _polygon.Clear();

        foreach (var vertex in face.Vertices)
        {
            _polygon.Add(Project(Vector3.Transform(vertex, orientation), center, radius));
        }

        var light = Math.Clamp(Vector3.Dot(visible.Normal, LightDirection), 0, 1);

        context.DrawGeometry(
            Shade(AmbientLight + ((1 - AmbientLight) * light)),
            Edge,
            new PolylineGeometry(_polygon, true));

        if (die.LabelOf(visible.Index) is { } label)
        {
            // Выпавшее число выделяется только после того, как кубик улёгся:
            // до этого выделять нечего — кубик ещё катится.
            DrawLabel(
                context,
                label,
                face,
                visible.Normal,
                orientation,
                center,
                radius,
                landed && die.IsResult(visible.Index));
        }
    }

    /// <summary>
    /// Пишет число на грани.
    /// </summary>
    /// <param name="context">Контекст отрисовки.</param>
    /// <param name="label">Число грани.</param>
    /// <param name="face">Грань тела.</param>
    /// <param name="normal">Повёрнутая нормаль грани.</param>
    /// <param name="orientation">Поворот кубика.</param>
    /// <param name="center">Центр кубика на экране.</param>
    /// <param name="radius">Радиус кубика на экране.</param>
    /// <param name="isResult">Это выпавшее число улёгшегося кубика.</param>
    private void DrawLabel(
        DrawingContext context,
        string label,
        DieFace face,
        Vector3 normal,
        Quaternion orientation,
        Point center,
        double radius,
        bool isResult)
    {
        if (normal.Z <= LabelVisibility)
        {
            return;
        }

        // Число проступает тем ярче, чем прямее грань смотрит на зрителя: так
        // на кубике читается одно число, а не путаница из трёх соседних.
        var visibility = Math.Clamp(
            (normal.Z - LabelVisibility) / (LabelFullVisibility - LabelVisibility),
            0,
            1);

        // Размер числа берётся от размера самой грани, поэтому он верен и для
        // крупной грани куба, и для мелкой грани икосаэдра.
        var extent = face.Vertices.Average(vertex => Vector3.Distance(vertex, face.Center));
        var scale = extent * radius * normal.Z * LabelScale / LabelBaseSize;

        if (isResult)
        {
            scale *= ResultScale;
        }

        if (scale <= 0)
        {
            return;
        }

        var text = Text(label);
        var position = Project(Vector3.Transform(face.Center, orientation), center, radius);

        using (context.PushTransform(
            Matrix.CreateTranslation(-text.Width / 2, -text.Height / 2)
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(position.X, position.Y)))
        using (context.PushOpacity(visibility))
        {
            if (isResult)
            {
                // Выпавшее число залито тёплым цветом и обведено: заливка
                // отличает его от соседних чисел, обводка отделяет от грани
                // любого цвета — даже если кубик сам тёплый и жёлтый.
                context.DrawGeometry(ResultBrush, ResultPen, Outline(label));
            }
            else
            {
                context.DrawText(text, default);
            }
        }
    }

    /// <summary>
    /// Возвращает контур числа, пригодный для заливки и обводки.
    ///
    /// Контуры готовятся один раз: за бросок их требуется не больше одного
    /// на кубик, но перерисовываются они каждый кадр.
    /// </summary>
    /// <param name="label">Число грани.</param>
    /// <returns>Контур числа.</returns>
    private Geometry Outline(string label)
    {
        if (_outlines.TryGetValue(label, out var existing))
        {
            return existing;
        }

        var created = Text(label).BuildGeometry(default) ?? new PolylineGeometry();

        _outlines[label] = created;
        return created;
    }

    /// <summary>Кисть выпавшего числа.</summary>
    private ImmutableSolidColorBrush ResultBrush =>
        _result ??= new ImmutableSolidColorBrush(HighlightColor(CustomColor ?? DieColor));

    /// <summary>Перо, которым обводится выпавшее число.</summary>
    private IPen ResultPen => _resultOutline ??= new ImmutablePen(
        new ImmutableSolidColorBrush(ContrastColor(CustomColor ?? DieColor)),
        ResultOutline);

    /// <summary>
    /// Подбирает цвет выпавшего числа.
    ///
    /// Оттенок берётся противоположным оттенку кубика, а светлота — обратной его
    /// светлоте. Жёстко заданный цвет — например, золотой — потерялся бы на золотом
    /// кубике, который пользователь вправе создать; противоположный оттенок заметен
    /// на кубике любого цвета.
    /// </summary>
    /// <param name="color">Цвет кубика.</param>
    /// <returns>Цвет выпавшего числа.</returns>
    private static Color HighlightColor(Color color)
    {
        const double HalfCircle = 180;
        const double FullCircle = 360;
        const double DarkResult = 0.30;
        const double LightResult = 0.62;

        var source = color.ToHsl();

        if (source.S < ColorfulEnough)
        {
            return NeutralResultColor;
        }

        return new HslColor(
            1,
            (source.H + HalfCircle) % FullCircle,
            ResultSaturation,
            IsLight(color) ? DarkResult : LightResult).ToRgb();
    }

    /// <summary>
    /// Переводит точку тела в точку экрана.
    /// </summary>
    /// <param name="point">Точка тела после поворота.</param>
    /// <param name="center">Центр кубика на экране.</param>
    /// <param name="radius">Радиус кубика на экране.</param>
    /// <returns>Точка экрана.</returns>
    private static Point Project(Vector3 point, Point center, double radius)
    {
        // Ближняя часть кубика крупнее дальней: без перспективы тело выглядит
        // плоской развёрткой, а не предметом.
        var depth = CameraDistance / (CameraDistance - point.Z);

        return new Point(
            center.X + (point.X * radius * depth),
            center.Y - (point.Y * radius * depth));
    }

    /// <summary>
    /// Возвращает кисть грани с заданной освещённостью.
    /// </summary>
    /// <param name="light">Освещённость от нуля до единицы.</param>
    /// <returns>Кисть грани.</returns>
    private ImmutableSolidColorBrush Shade(double light)
    {
        if (_shades is null)
        {
            var color = CustomColor ?? DieColor;
            _shades = new ImmutableSolidColorBrush[ShadeCount];

            for (var index = 0; index < ShadeCount; index++)
            {
                var factor = (index + 0.5) / ShadeCount;

                _shades[index] = new ImmutableSolidColorBrush(Color.FromArgb(
                    color.A,
                    Blend(color.R, factor),
                    Blend(color.G, factor),
                    Blend(color.B, factor)));
            }
        }

        return _shades[Math.Clamp((int)(light * ShadeCount), 0, ShadeCount - 1)];
    }

    /// <summary>
    /// Смешивает канал цвета с тенью и бликом.
    ///
    /// Освещение не просто затемняет цвет: неосвещённая грань темнеет, освещённая
    /// светлеет к белому. Иначе тело в тёмной теме сливается с фоном и выглядит
    /// не кубиком, а дырой.
    /// </summary>
    /// <param name="channel">Значение канала цвета кубика.</param>
    /// <param name="light">Освещённость от нуля до единицы.</param>
    /// <returns>Значение канала освещённой грани.</returns>
    private static byte Blend(byte channel, double light)
    {
        var shadow = channel * ShadowFactor;
        var highlight = channel + ((byte.MaxValue - channel) * HighlightMix);

        return (byte)(shadow + ((highlight - shadow) * light));
    }

    /// <summary>
    /// Возвращает подготовленную надпись числа.
    ///
    /// Надписи готовятся один раз: за кадр их рисуется десяток, и заново размечать
    /// текст для каждого кадра было бы расточительно.
    /// </summary>
    /// <param name="label">Число грани.</param>
    /// <returns>Подготовленная надпись.</returns>
    private FormattedText Text(string label)
    {
        if (_labels.TryGetValue(label, out var existing))
        {
            return existing;
        }

        var created = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.SemiBold),
            LabelBaseSize,
            new ImmutableSolidColorBrush(ContrastColor(CustomColor ?? DieColor)));

        _labels[label] = created;
        return created;
    }

    /// <summary>
    /// Подбирает цвет числа, различимый на кубике.
    ///
    /// Пользователь волен создать и почти чёрный, и ярко-жёлтый кубик, поэтому цвет
    /// числа не задан заранее, а выбирается по яркости самого кубика.
    /// </summary>
    /// <param name="color">Цвет кубика.</param>
    /// <returns>Цвет числа.</returns>
    private static Color ContrastColor(Color color) =>
        IsLight(color) ? Color.FromRgb(0x1C, 0x1C, 0x1E) : Colors.White;

    /// <summary>
    /// Проверяет, светлым ли выглядит цвет.
    ///
    /// Светлота из модели HSL для этого не годится: насыщенный синий получает
    /// в ней половину шкалы, хотя глазу кажется тёмным. Коэффициенты ниже
    /// соответствуют восприятию: зелёный кажется светлее синего той же величины.
    /// </summary>
    /// <param name="color">Проверяемый цвет.</param>
    /// <returns><see langword="true"/> для светлого цвета.</returns>
    private static bool IsLight(Color color)
    {
        const double RedWeight = 0.299;
        const double GreenWeight = 0.587;
        const double BlueWeight = 0.114;
        const double LightThreshold = 140;

        return (color.R * RedWeight) + (color.G * GreenWeight) + (color.B * BlueWeight) > LightThreshold;
    }

    /// <summary>
    /// Грань, обращённая к зрителю.
    /// </summary>
    /// <param name="Index">Номер грани в теле кубика.</param>
    /// <param name="Depth">Глубина центра грани: чем больше, тем ближе к зрителю.</param>
    /// <param name="Normal">Повёрнутая нормаль грани.</param>
    private readonly record struct VisibleFace(int Index, float Depth, Vector3 Normal);

    /// <summary>
    /// Кубик в полёте: собственное тело, начальный поворот и способ приземления.
    /// </summary>
    private sealed class FlyingDie
    {
        private readonly Quaternion _start;
        private readonly Quaternion _landing;
        private readonly Vector3 _axis;
        private readonly float _spin;
        private readonly string?[] _labels;
        private readonly int _face;
        private readonly double _bounces;
        private readonly double _drop;

        private FlyingDie(DieMesh mesh, DieCast cast, int index)
        {
            Mesh = mesh;
            Delay = CascadeDelay * Math.Min(index, MaximumCascade);
            Drift = (Random.Shared.NextDouble() - 0.5) * DriftRange;

            // Высота и число подскоков у каждого кубика свои: горсть одинаково
            // прыгающих костей выглядит нарисованной, а не брошенной.
            _bounces = BounceCount * (0.85 + (Random.Shared.NextDouble() * 0.4));
            _drop = DropHeight * (0.85 + (Random.Shared.NextDouble() * 0.3));

            var found = mesh.FindFaceIndex(cast.Value);

            // У шара граней меньше, чем значений у кубика с большим числом сторон.
            // Выпавшее число тогда пишется на той грани, что окажется перед зрителем.
            _face = found == DieMesh.NoFace ? 0 : found;

            _labels = new string?[mesh.Faces.Count];

            for (var position = 0; position < _labels.Length; position++)
            {
                _labels[position] = mesh.Faces[position].HasValue
                    ? mesh.Faces[position].Value.ToString(CultureInfo.CurrentCulture)
                    : null;
            }

            _labels[_face] = cast.Value.ToString(CultureInfo.CurrentCulture);

            _start = RandomOrientation();
            _axis = RandomAxis();
            _spin = SpinSpeed * (float)(0.75 + Random.Shared.NextDouble());

            // Приземление доворачивает выпавшую грань к зрителю. Небольшой случайный
            // поворот вокруг взгляда не даёт броскам выглядеть одинаково; числа при
            // этом остаются прямыми, потому что пишутся поверх грани.
            var roll = (float)((Random.Shared.NextDouble() - 0.5) * MathF.PI / 4);
            var lean = (float)(Random.Shared.NextDouble() * MathF.Tau);

            _landing = Quaternion.CreateFromAxisAngle(
                    new Vector3(MathF.Cos(lean), MathF.Sin(lean), 0),
                    LandingTilt)
                * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll)
                * AlignTo(mesh.Faces[_face].Normal, Vector3.UnitZ);
        }

        /// <summary>Тело кубика.</summary>
        public DieMesh Mesh { get; }

        /// <summary>Задержка перед началом полёта.</summary>
        public TimeSpan Delay { get; }

        /// <summary>Смещение вбок в начале падения, в радиусах кубика.</summary>
        public double Drift { get; }

        /// <summary>
        /// Готовит кубик к полёту.
        /// </summary>
        /// <param name="cast">Выпавший кубик.</param>
        /// <param name="index">Номер кубика в броске.</param>
        /// <returns>Кубик в полёте.</returns>
        public static FlyingDie Create(DieCast cast, int index) =>
            new(DieMesh.Create(Math.Max(cast.Sides, DieMesh.MinimumSides)), cast, index);

        /// <summary>
        /// Возвращает число, написанное на грани.
        /// </summary>
        /// <param name="face">Номер грани.</param>
        /// <returns>Число либо <see langword="null"/> для грани без числа.</returns>
        public string? LabelOf(int face) => _labels[face];

        /// <summary>
        /// Проверяет, что на этой грани написано выпавшее число.
        /// </summary>
        /// <param name="face">Номер грани.</param>
        /// <returns><see langword="true"/> для грани, которой кубик лёг к зрителю.</returns>
        public bool IsResult(int face) => face == _face;

        /// <summary>
        /// Проверяет, что кубик закончил движение.
        /// </summary>
        /// <param name="time">Время с начала полёта в секундах.</param>
        /// <returns><see langword="true"/>, если кубик улёгся.</returns>
        public static bool HasLanded(double time) =>
            time >= TumbleDuration.TotalSeconds + SettleDuration.TotalSeconds;

        /// <summary>
        /// Возвращает высоту кубика над поверхностью.
        /// </summary>
        /// <param name="time">Время с начала полёта в секундах.</param>
        /// <returns>Высота в радиусах кубика.</returns>
        public double HeightAt(double time)
        {
            if (time <= 0)
            {
                return _drop;
            }

            var tumble = TumbleDuration.TotalSeconds;

            if (time >= tumble)
            {
                return 0;
            }

            var phase = time / tumble;

            // Модуль косинуса даёт касание поверхности, степень — затухание:
            // кубик отскакивает всё ниже и наконец останавливается.
            return _drop
                * Math.Abs(Math.Cos(phase * Math.PI * _bounces))
                * Math.Pow(BounceDecay, -phase);
        }

        /// <summary>
        /// Возвращает поворот кубика.
        /// </summary>
        /// <param name="time">Время с начала полёта в секундах.</param>
        /// <returns>Поворот кубика.</returns>
        public Quaternion OrientationAt(double time)
        {
            var tumble = TumbleDuration.TotalSeconds;
            var settle = SettleDuration.TotalSeconds;

            if (time <= 0)
            {
                return _start;
            }

            if (time >= tumble + settle)
            {
                return _landing;
            }

            var tumbled = Tumble(Math.Min(time, tumble));

            if (time <= tumble)
            {
                return tumbled;
            }

            // Доворот идёт кратчайшим путём и с замедлением: кубик укладывается
            // на грань, а не перескакивает в нужное положение.
            var phase = (time - tumble) / settle;

            return Quaternion.Slerp(tumbled, _landing, (float)(1 - Math.Pow(1 - phase, 3)));
        }

        /// <summary>
        /// Возвращает поворот кубика в фазе кувыркания.
        /// </summary>
        /// <param name="time">Время с начала полёта в секундах.</param>
        /// <returns>Поворот кубика.</returns>
        private Quaternion Tumble(double time)
        {
            // Скорость вращения затухает, поэтому берётся её первообразная:
            // угол растёт быстро вначале и почти не меняется к концу полёта.
            var angle = _spin * MathF.Tau * (1 - MathF.Exp(-SpinDecay * (float)time)) / SpinDecay;

            return Quaternion.CreateFromAxisAngle(_axis, angle) * _start;
        }

        /// <summary>
        /// Возвращает поворот, направляющий одно направление в другое кратчайшим путём.
        /// </summary>
        /// <param name="from">Исходное направление.</param>
        /// <param name="to">Требуемое направление.</param>
        /// <returns>Поворот.</returns>
        private static Quaternion AlignTo(Vector3 from, Vector3 to)
        {
            const float Parallel = 0.99999f;

            var source = Vector3.Normalize(from);
            var target = Vector3.Normalize(to);
            var projection = Vector3.Dot(source, target);

            if (projection >= Parallel)
            {
                return Quaternion.Identity;
            }

            if (projection <= -Parallel)
            {
                // Направления противоположны: осью разворота служит любая
                // перпендикулярная, поворот — на половину оборота.
                var perpendicular = Vector3.Cross(source, Vector3.UnitX);

                if (perpendicular.LengthSquared() < float.Epsilon)
                {
                    perpendicular = Vector3.Cross(source, Vector3.UnitY);
                }

                return Quaternion.CreateFromAxisAngle(Vector3.Normalize(perpendicular), MathF.PI);
            }

            return Quaternion.CreateFromAxisAngle(
                Vector3.Normalize(Vector3.Cross(source, target)),
                MathF.Acos(projection));
        }

        private static Quaternion RandomOrientation() => Quaternion.CreateFromYawPitchRoll(
            (float)(Random.Shared.NextDouble() * MathF.Tau),
            (float)(Random.Shared.NextDouble() * MathF.Tau),
            (float)(Random.Shared.NextDouble() * MathF.Tau));

        private static Vector3 RandomAxis() => Vector3.Normalize(new Vector3(
            (float)(Random.Shared.NextDouble() - 0.5),
            (float)(Random.Shared.NextDouble() - 0.5),
            (float)((Random.Shared.NextDouble() - 0.5) * 0.4)));
    }
}
