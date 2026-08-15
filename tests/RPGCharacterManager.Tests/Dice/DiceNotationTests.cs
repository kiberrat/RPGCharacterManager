using RPGCharacterManager.Core.Abstractions.Dice;

namespace RPGCharacterManager.Tests.Dice;

/// <summary>
/// Запись броска: сборка смешанного выражения нажатиями на кубики.
/// </summary>
public sealed class DiceNotationTests
{
    [Fact]
    public void Добавление_КПустомуВыражению_ДаётОдинБросок()
    {
        Assert.Equal("2d10", DiceNotation.Add(null, 2, 10));
        Assert.Equal("2d10", DiceNotation.Add(string.Empty, 2, 10));
        Assert.Equal("2d10", DiceNotation.Add("   ", 2, 10));
    }

    [Fact]
    public void Добавление_РазныхКубиков_СобираетСмешанныйБросок()
    {
        var expression = DiceNotation.Add(null, 2, 10);

        expression = DiceNotation.Add(expression, 4, 4);
        expression = DiceNotation.Add(expression, 15, 8);

        Assert.Equal("2d10 + 4d4 + 15d8", expression);
    }

    [Fact]
    public void Добавление_ТогоЖеКубика_ОбъединяетКоличество()
    {
        // Пять нажатий на один кубик должны дать 5d6, а не пять слагаемых подряд.
        var expression = string.Empty;

        for (var index = 0; index < 5; index++)
        {
            expression = DiceNotation.Add(expression, 1, 6);
        }

        Assert.Equal("5d6", expression);
    }

    [Fact]
    public void Добавление_ТогоЖеКубикаПослеДругого_ОбъединяетТолькоПоследний()
    {
        var expression = DiceNotation.Add("2d10 + 4d4", 3, 4);

        Assert.Equal("2d10 + 7d4", expression);
    }

    [Fact]
    public void Добавление_КЗаписиБезКоличества_СчитаетЕёОднимКубиком()
    {
        Assert.Equal("3d20", DiceNotation.Add("d20", 2, 20));
    }

    [Fact]
    public void Добавление_КРусскойЗаписи_ОбъединяетЕёТакЖе()
    {
        // Пользователь вправе писать формулы по-русски, и собранный набор
        // не должен разваливаться на два слагаемых из-за буквы.
        Assert.Equal("5к6", DiceNotation.Add("2к6", 3, 6));
    }

    [Theory]
    [InlineData("1d20 + Сила", 2, 6, "1d20 + Сила + 2d6")]
    [InlineData("2d10", 3, 6, "2d10 + 3d6")]
    [InlineData("10", 2, 6, "10 + 2d6")]
    [InlineData("2d6 * 2", 1, 6, "2d6 * 2 + 1d6")]
    public void Добавление_КогдаОбъединитьНельзя_ДописываетСлагаемое(
        string expression,
        int count,
        int sides,
        string expected) =>
        Assert.Equal(expected, DiceNotation.Add(expression, count, sides));

    [Fact]
    public void Добавление_КИмениСЦифрами_НеПортитЕго()
    {
        // «урон2d6» — имя переменной, а не бросок: дописывать внутрь него нельзя.
        Assert.Equal("урон2d6 + 3d6", DiceNotation.Add("урон2d6", 3, 6));
    }

    [Fact]
    public void Добавление_ХвостовыеПробелы_НеМешаютОбъединению()
    {
        Assert.Equal("5d6", DiceNotation.Add("2d6   ", 3, 6));
    }

    [Fact]
    public void БукваКубика_ЗаданаОднимПеречнем()
    {
        Assert.Contains(DiceNotation.Separator, DiceNotation.Separators);

        Assert.True(DiceNotation.IsSeparator('d'));
        Assert.True(DiceNotation.IsSeparator('к'));
        Assert.False(DiceNotation.IsSeparator('x'));
    }
}
