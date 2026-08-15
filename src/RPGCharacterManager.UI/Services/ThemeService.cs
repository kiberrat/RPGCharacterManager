using System.Collections.Frozen;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.Services;

/// <summary>
/// Применение темы, акцентного цвета и размера шрифта к работающему приложению.
///
/// Все значения подставляются в ресурсы приложения, на которые разметка ссылается
/// через <c>DynamicResource</c>, поэтому оформление меняется без перезапуска.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string AccentColorResourceKey = "AppAccentColor";
    private const string AccentHoverColorResourceKey = "AppAccentHoverColor";
    private const string AccentPressedColorResourceKey = "AppAccentPressedColor";
    private const string FontSizeResourceKey = "AppFontSize";
    private const string SmallFontSizeResourceKey = "AppFontSizeSmall";
    private const string LargeFontSizeResourceKey = "AppFontSizeLarge";
    private const string TitleFontSizeResourceKey = "AppFontSizeTitle";

    private const double SmallFontSizeOffset = -2.0;
    private const double LargeFontSizeOffset = 4.0;
    private const double TitleFontSizeOffset = 8.0;

    /// <summary>
    /// Палитра акцентных цветов: основной оттенок, оттенок наведения и оттенок нажатия.
    /// </summary>
    private static readonly FrozenDictionary<AccentColor, (string Base, string Hover, string Pressed)> AccentPalette =
        new Dictionary<AccentColor, (string Base, string Hover, string Pressed)>
        {
            [AccentColor.Blue] = ("#FF0A84FF", "#FF3D9BFF", "#FF0060DF"),
            [AccentColor.Red] = ("#FFFF453A", "#FFFF6A61", "#FFD93026"),
            [AccentColor.Green] = ("#FF30D158", "#FF57DC79", "#FF23A845"),
            [AccentColor.Purple] = ("#FFBF5AF2", "#FFCC7CF5", "#FF9A3FC7"),
            [AccentColor.Orange] = ("#FFFF9F0A", "#FFFFB340", "#FFDB8500"),
        }.ToFrozenDictionary();

    /// <inheritdoc />
    public ThemeMode CurrentTheme { get; private set; } = ThemeMode.Dark;

    /// <inheritdoc />
    public AccentColor CurrentAccent { get; private set; } = AccentColor.Blue;

    /// <inheritdoc />
    public void Apply(AppSettings settings)
    {
        Guard.NotNull(settings);

        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        application.RequestedThemeVariant = ToThemeVariant(settings.Theme);
        ApplyAccent(application, settings.Accent);
        ApplyFontSize(application, settings.FontSize);

        CurrentTheme = settings.Theme;
        CurrentAccent = settings.Accent;
    }

    private static ThemeVariant ToThemeVariant(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        // ThemeVariant.Default означает «следовать настройке операционной системы».
        _ => ThemeVariant.Default,
    };

    private static void ApplyAccent(Application application, AccentColor accent)
    {
        if (!AccentPalette.TryGetValue(accent, out var palette))
        {
            palette = AccentPalette[AccentColor.Blue];
        }

        application.Resources[AccentColorResourceKey] = Color.Parse(palette.Base);
        application.Resources[AccentHoverColorResourceKey] = Color.Parse(palette.Hover);
        application.Resources[AccentPressedColorResourceKey] = Color.Parse(palette.Pressed);
    }

    private static void ApplyFontSize(Application application, double baseFontSize)
    {
        application.Resources[FontSizeResourceKey] = baseFontSize;
        application.Resources[SmallFontSizeResourceKey] = baseFontSize + SmallFontSizeOffset;
        application.Resources[LargeFontSizeResourceKey] = baseFontSize + LargeFontSizeOffset;
        application.Resources[TitleFontSizeResourceKey] = baseFontSize + TitleFontSizeOffset;
    }
}
