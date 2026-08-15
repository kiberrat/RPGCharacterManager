using Avalonia;
using Avalonia.Controls;
using RPGCharacterManager.UI.ViewModels.Characters;

namespace RPGCharacterManager.UI.Controls;

/// <summary>
/// Панель, делящая ширину между детьми пропорционально их долям.
///
/// Доля берётся у панели листа персонажа: две панели с долями 1 и 2 занимают
/// треть и две трети ширины вкладки. Обычная сетка делит ширину поровну,
/// а колонки со звёздочкой нельзя задать из списка, поэтому размер панели
/// задаётся именно так.
/// </summary>
public sealed class SharePanel : Panel
{
    /// <summary>Доля, применяемая к ребёнку без собственной доли.</summary>
    private const double DefaultShare = 1;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var total = TotalShare();
        var height = 0.0;

        foreach (var child in Children)
        {
            var width = double.IsInfinity(availableSize.Width)
                ? availableSize.Width
                : availableSize.Width * (ShareOf(child) / total);

            child.Measure(new Size(width, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(
            double.IsInfinity(availableSize.Width) ? Children.Sum(child => child.DesiredSize.Width) : availableSize.Width,
            height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var total = TotalShare();
        var offset = 0.0;

        foreach (var child in Children)
        {
            var width = finalSize.Width * (ShareOf(child) / total);

            child.Arrange(new Rect(offset, 0, width, finalSize.Height));
            offset += width;
        }

        return finalSize;
    }

    /// <summary>
    /// Возвращает сумму долей всех детей.
    /// </summary>
    /// <returns>Сумма долей; не меньше единицы, чтобы не делить на ноль.</returns>
    private double TotalShare()
    {
        var total = Children.Sum(ShareOf);

        return total > 0 ? total : DefaultShare;
    }

    /// <summary>
    /// Возвращает долю ребёнка.
    /// </summary>
    /// <param name="child">Ребёнок панели.</param>
    /// <returns>Доля ширины.</returns>
    private static double ShareOf(Control child) =>
        child.DataContext is SheetPanelViewModel panel && panel.Width > 0
            ? panel.Width
            : DefaultShare;
}
