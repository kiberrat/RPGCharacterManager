using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Engine;
using RPGCharacterManager.Engine.Functions;

namespace RPGCharacterManager.Tests.Engine;

/// <summary>
/// Источник случайных значений с предсказуемым поведением.
/// Позволяет проверять броски кубиков без зависимости от случайности.
/// </summary>
internal sealed class ConstantRandomSource(int value) : IRandomSource
{
    public int Next(int minimumInclusive, int maximumInclusive) =>
        Math.Clamp(value, minimumInclusive, maximumInclusive);
}

public sealed class FormulaEngineTests
{
    private const double Tolerance = 1e-9;

    private static FormulaEngine CreateEngine(IRandomSource? random = null)
    {
        var source = random ?? new ConstantRandomSource(3);

        IFormulaFunction[] functions =
        [
            new MinimumFunction(),
            new MaximumFunction(),
            new SumFunction(),
            new AverageFunction(),
            new CountFunction(),
            new RoundFunction(),
            new FloorFunction(),
            new CeilingFunction(),
            new AbsoluteFunction(),
            new ClampFunction(),
            new IfFunction(),
            new DiceFunction(source),
            new RandomFunction(source),
        ];

        return new FormulaEngine(functions, source);
    }

    private static double Evaluate(string expression, IFormulaContext? context = null)
    {
        var result = CreateEngine().Evaluate(expression, context);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value.AsNumber();
    }

    // ---------- Арифметика ----------

    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("10 - 4", 6)]
    [InlineData("6 * 7", 42)]
    [InlineData("20 / 4", 5)]
    [InlineData("10 % 3", 1)]
    [InlineData("2 ^ 10", 1024)]
    [InlineData("-5 + 3", -2)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    [InlineData("1.5 + 2.25", 3.75)]
    public void Вычисление_ВыполняетАрифметическиеОперации(string expression, double expected)
    {
        Assert.Equal(expected, Evaluate(expression), Tolerance);
    }

    [Theory]
    [InlineData("2 × 3", 6)]
    [InlineData("12 ÷ 4", 3)]
    [InlineData("1,5 + 1,5", 3)]
    public void Вычисление_ПринимаетРусскиеОбозначенияОпераций(string expression, double expected)
    {
        Assert.Equal(expected, Evaluate(expression), Tolerance);
    }

    // ---------- Переменные ----------

    [Fact]
    public void Переменные_ПодставляютсяИзИсточникаЗначений()
    {
        var context = new DictionaryFormulaContext()
            .Set("Сила", 18)
            .Set("Уровень", 5);

        Assert.Equal(30, Evaluate("Сила + Уровень * 2.4", context), Tolerance);
    }

    [Fact]
    public void Переменные_НеЗависятОтРегистра()
    {
        var context = new DictionaryFormulaContext().Set("Ловкость", 14);

        Assert.Equal(14, Evaluate("ЛОВКОСТЬ", context), Tolerance);
    }

    [Fact]
    public void Переменные_НеизвестноеИмяВозвращаетОшибку()
    {
        var result = CreateEngine().Evaluate("Сила + 1");

        Assert.True(result.IsFailure);
        Assert.Contains("Сила", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ПереченьПеременных_ВозвращаетВсеИспользуемыеИмена()
    {
        var result = CreateEngine().GetReferencedVariables("(Сила + Ловкость) * Уровень + Сила");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(["Ловкость", "Сила", "Уровень"], result.Value);
    }

    // ---------- Формула модификатора характеристики ----------

    [Theory]
    [InlineData(8, -1)]
    [InlineData(10, 0)]
    [InlineData(15, 2)]
    [InlineData(18, 4)]
    [InlineData(20, 5)]
    public void Формула_ВычисляетМодификаторХарактеристики(double value, double expected)
    {
        // Правило игровой системы задаётся данными, а не кодом приложения.
        var context = new DictionaryFormulaContext().Set("Значение", value);

        Assert.Equal(expected, Evaluate("ОкруглитьВниз((Значение - 10) / 2)", context), Tolerance);
    }

    // ---------- Условия ----------

    [Theory]
    [InlineData("5 > 3", true)]
    [InlineData("5 < 3", false)]
    [InlineData("5 >= 5", true)]
    [InlineData("5 != 3", true)]
    [InlineData("5 = 5", true)]
    [InlineData("5 > 3 и 2 > 1", true)]
    [InlineData("5 > 3 и 1 > 2", false)]
    [InlineData("5 < 3 или 2 > 1", true)]
    [InlineData("не (5 < 3)", true)]
    public void Условия_ВычисляютЛогическиеВыражения(string expression, bool expected)
    {
        var result = CreateEngine().Evaluate(expression);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expected, result.Value.AsBoolean());
    }

    [Fact]
    public void Условия_ПроверяютТребованияКПерсонажу()
    {
        var context = new DictionaryFormulaContext()
            .Set("Уровень", 6)
            .Set("Сила", 16);

        var result = CreateEngine().Evaluate("Уровень >= 4 и Сила >= 15", context);

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Value.AsBoolean());
    }

    [Theory]
    [InlineData("Если(1 > 0; 10; 20)", 10)]
    [InlineData("Если(1 < 0; 10; 20)", 20)]
    [InlineData("Если(1 < 0; 10)", 0)]
    public void ФункцияЕсли_ВыбираетВетвь(string expression, double expected)
    {
        Assert.Equal(expected, Evaluate(expression), Tolerance);
    }

    [Fact]
    public void ФункцияЕсли_НеВычисляетНевыбраннуюВетвь()
    {
        // Ветвь с делением на ноль не должна вычисляться, если условие ложно.
        var result = CreateEngine().Evaluate("Если(1 > 0; 5; 1 / 0)");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(5, result.Value.AsNumber(), Tolerance);
    }

    // ---------- Функции ----------

    [Theory]
    [InlineData("Минимум(5; 2; 9)", 2)]
    [InlineData("Максимум(5; 2; 9)", 9)]
    [InlineData("Сумма(1; 2; 3; 4)", 10)]
    [InlineData("Среднее(2; 4; 6)", 4)]
    [InlineData("Количество(1; 2; 3)", 3)]
    [InlineData("Округлить(2.5)", 3)]
    [InlineData("Округлить(2.345; 2)", 2.35)]
    [InlineData("ОкруглитьВниз(2.9)", 2)]
    [InlineData("ОкруглитьВверх(2.1)", 3)]
    [InlineData("Модуль(-7)", 7)]
    [InlineData("Ограничить(15; 1; 10)", 10)]
    [InlineData("Ограничить(-5; 1; 10)", 1)]
    public void Функции_ВычисляютОжидаемыеЗначения(string expression, double expected)
    {
        Assert.Equal(expected, Evaluate(expression), Tolerance);
    }

    [Fact]
    public void Функции_ВложенныеВызовыВычисляютсяКорректно()
    {
        var context = new DictionaryFormulaContext()
            .Set("Мудрость", 12)
            .Set("Интеллект", 17);

        Assert.Equal(17, Evaluate("Максимум(Мудрость; Интеллект)", context), Tolerance);
    }

    // ---------- Кубики ----------

    [Fact]
    public void Кубики_СуммируютЗначенияВсехКубиков()
    {
        var engine = CreateEngine(new ConstantRandomSource(4));
        var result = engine.Evaluate("3d6");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(12, result.Value.AsNumber(), Tolerance);
    }

    [Fact]
    public void Кубики_ЗаписьБезКоличестваОзначаетОдинКубик()
    {
        var engine = CreateEngine(new ConstantRandomSource(15));
        var result = engine.Evaluate("d20");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(15, result.Value.AsNumber(), Tolerance);
    }

    [Fact]
    public void Кубики_ПоддерживаютРусскуюБуквуОбозначения()
    {
        var engine = CreateEngine(new ConstantRandomSource(5));
        var result = engine.Evaluate("2к8");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(10, result.Value.AsNumber(), Tolerance);
    }

    [Fact]
    public void Кубики_УчаствуютВВыраженииСПеременными()
    {
        var engine = CreateEngine(new ConstantRandomSource(4));
        var context = new DictionaryFormulaContext().Set("Ловкость", 3);

        var result = engine.Evaluate("2d6 + Ловкость", context);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(11, result.Value.AsNumber(), Tolerance);
    }

    [Fact]
    public void Кубики_ПревышениеПределаВозвращаетОшибку()
    {
        var result = CreateEngine().Evaluate("99999d6");

        Assert.True(result.IsFailure);
        Assert.Contains("кубиков", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Строки ----------

    [Fact]
    public void Строки_ОбъединяютсяОператоромСложения()
    {
        var context = new DictionaryFormulaContext().Set("Имя", "Арагорн");
        var result = CreateEngine().Evaluate("\"Герой: \" + Имя", context);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("Герой: Арагорн", result.Value.AsText());
    }

    // ---------- Проверка ошибок ----------

    [Theory]
    [InlineData("2 +")]
    [InlineData("(2 + 3")]
    [InlineData("2 $ 3")]
    [InlineData("")]
    [InlineData("Максимум(")]
    public void Проверка_ОбнаруживаетСинтаксическиеОшибки(string expression)
    {
        Assert.True(CreateEngine().Validate(expression).IsFailure);
    }

    [Fact]
    public void Проверка_ОбнаруживаетНеизвестнуюФункцию()
    {
        var result = CreateEngine().Validate("НеизвестнаяФункция(1; 2)");

        Assert.True(result.IsFailure);
        Assert.Contains("НеизвестнаяФункция", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Проверка_ОбнаруживаетНеверноеКоличествоАргументов()
    {
        var result = CreateEngine().Validate("Модуль(1; 2; 3)");

        Assert.True(result.IsFailure);
        Assert.Contains("Модуль", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Проверка_НеТребуетЗначенийПеременных()
    {
        // Проверка выполняется без вычисления, поэтому неизвестные переменные допустимы.
        Assert.True(CreateEngine().Validate("Сила + Ловкость * 2").IsSuccess);
    }

    [Fact]
    public void Вычисление_ДелениеНаНольВозвращаетОшибку()
    {
        var result = CreateEngine().Evaluate("10 / 0");

        Assert.True(result.IsFailure);
        Assert.Contains("ноль", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Кэширование ----------

    [Fact]
    public void Кэш_ПовторноеВычислениеДаётТотЖеРезультат()
    {
        var engine = CreateEngine();
        var context = new DictionaryFormulaContext().Set("Сила", 10);

        var first = engine.Evaluate("Сила * 2", context);
        var second = engine.Evaluate("Сила * 2", context);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(first.Value.AsNumber(), second.Value.AsNumber(), Tolerance);
    }

    [Fact]
    public void Кэш_УчитываетИзменениеЗначенийПеременных()
    {
        var engine = CreateEngine();

        var low = engine.Evaluate("Сила * 2", new DictionaryFormulaContext().Set("Сила", 5));
        var high = engine.Evaluate("Сила * 2", new DictionaryFormulaContext().Set("Сила", 9));

        Assert.Equal(10, low.Value.AsNumber(), Tolerance);
        Assert.Equal(18, high.Value.AsNumber(), Tolerance);
    }

    [Fact]
    public void Кэш_ВыдерживаетПереполнение()
    {
        var engine = CreateEngine();

        // Количество различных выражений превышает предел кэша: движок обязан
        // продолжать работу, а не исчерпать память или начать возвращать ошибки.
        for (var index = 0; index <= FormulaEngine.MaximumCachedExpressions + 10; index++)
        {
            var result = engine.Evaluate($"{index} + 1");
            Assert.True(result.IsSuccess, result.Error);
        }

        Assert.Equal(3, Evaluate("1 + 2"), Tolerance);
    }

    // ---------- Диапазон значений ----------

    [Fact]
    public void Диапазон_ВыражениеСКубиками_ДаётГраницыБроска()
    {
        var result = CreateEngine().EvaluateRange("2d6 + 3");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(5, result.Value.Minimum, Tolerance);
        Assert.Equal(15, result.Value.Maximum, Tolerance);
        Assert.False(result.Value.IsExact);
    }

    [Fact]
    public void Диапазон_ВыражениеБезКубиков_ДаётОдноЗначение()
    {
        var context = new DictionaryFormulaContext().Set("Сила", 16);
        var result = CreateEngine().EvaluateRange("Сила / 2", context);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(8, result.Value.Minimum, Tolerance);
        Assert.True(result.Value.IsExact);
    }

    [Fact]
    public void Диапазон_ФункцияКубик_УчитываетсяКакБросок()
    {
        // Функция «Кубик» получает собственный источник значений, поэтому границы
        // не зависят от того, записан бросок как 2d6 или как вызов функции.
        var result = CreateEngine().EvaluateRange("Кубик(2; 6)");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.Minimum, Tolerance);
        Assert.Equal(12, result.Value.Maximum, Tolerance);
    }

    [Fact]
    public void Диапазон_ВычитаниеБроска_ВозвращаетГраницыПоВозрастанию()
    {
        var result = CreateEngine().EvaluateRange("10 - 1d6");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(4, result.Value.Minimum, Tolerance);
        Assert.Equal(9, result.Value.Maximum, Tolerance);
    }

    [Fact]
    public void Диапазон_НеизвестнаяПеременная_ВозвращаетОшибку()
    {
        var result = CreateEngine().EvaluateRange("1d6 + Мощь");

        Assert.True(result.IsFailure);
    }

    // ---------- Расширяемость ----------

    [Fact]
    public void ПользовательскаяФункция_СтановитсяДоступнойВФормулах()
    {
        var engine = new FormulaEngine([new DoubleFunction()], new ConstantRandomSource(1));
        var result = engine.Evaluate("Удвоить(21)");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(42, result.Value.AsNumber(), Tolerance);
    }

    /// <summary>
    /// Пример пользовательской функции: подсистема регистрирует её в контейнере
    /// зависимостей и функция сразу доступна во всех формулах приложения.
    /// </summary>
    private sealed class DoubleFunction() : FormulaFunctionBase(
        "Удвоить",
        "Умножает значение на два.",
        1,
        1)
    {
        public override FormulaValue Invoke(IReadOnlyList<FormulaValue> arguments) =>
            FormulaValue.FromNumber(arguments[0].AsNumber() * 2);
    }
}
