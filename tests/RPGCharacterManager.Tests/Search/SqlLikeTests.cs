using RPGCharacterManager.Database;

namespace RPGCharacterManager.Tests.Search;

/// <summary>
/// Проверка сравнения с образцом <c>LIKE</c>, заменяющего встроенное в SQLite.
/// </summary>
public sealed class SqlLikeTests
{
    [Theory]
    [InlineData("%волк%", "Волколак")]
    [InlineData("%ВОЛК%", "волколак")]
    [InlineData("%Ёж%", "ёжик")]
    [InlineData("%ёЖ%", "Ёжик")]
    public void Сравнение_НеРазличаетРегистрКириллицы(string pattern, string value)
    {
        // Встроенный LIKE в SQLite сводит регистр только у латиницы: ради этого
        // он и заменён (решение Р-95).
        Assert.True(SqlLike.Matches(pattern, value));
    }

    [Theory]
    [InlineData("%wolf%", "Wolfhound")]
    [InlineData("%WOLF%", "wolfhound")]
    public void Сравнение_НеРазличаетРегистрЛатиницы(string pattern, string value) =>
        Assert.True(SqlLike.Matches(pattern, value));

    [Theory]
    [InlineData("волк", "Волколак")]
    [InlineData("%лис%", "Волколак")]
    [InlineData("%волк", "Волколак")]
    public void Сравнение_ОтклоняетНеподходящее(string pattern, string value) =>
        Assert.False(SqlLike.Matches(pattern, value));

    [Theory]
    [InlineData("Волк%", "Волколак")]
    [InlineData("%лак", "Волколак")]
    [InlineData("Волколак", "Волколак")]
    [InlineData("%", "Волколак")]
    [InlineData("%%", "Волколак")]
    public void Сравнение_ПонимаетПроцент(string pattern, string value) =>
        Assert.True(SqlLike.Matches(pattern, value));

    [Theory]
    [InlineData("_олк", "Волк")]
    [InlineData("В_лк", "Волк")]
    [InlineData("____", "Волк")]
    public void Сравнение_ПонимаетПодчёркивание(string pattern, string value) =>
        Assert.True(SqlLike.Matches(pattern, value));

    [Fact]
    public void Сравнение_ПодчёркиваниеТребуетРовноОдинЗнак()
    {
        Assert.False(SqlLike.Matches("_олк", "Уволк"));
        Assert.False(SqlLike.Matches("_____", "Волк"));
    }

    [Fact]
    public void Сравнение_ПонимаетЭкранирование()
    {
        // Экранированный знак ищется как обычный, а не как подстановка.
        Assert.True(SqlLike.Matches("100!%", "100%", '!'));
        Assert.False(SqlLike.Matches("100!%", "100 рублей", '!'));
        Assert.True(SqlLike.Matches("а!_б", "а_б", '!'));
    }

    [Fact]
    public void Сравнение_СПустымЗначением_НеИстинно()
    {
        Assert.False(SqlLike.Matches(null, "Волк"));
        Assert.False(SqlLike.Matches("%волк%", null));
    }

    [Fact]
    public void Сравнение_ДлинногоЗначения_НеПереполняетСтек()
    {
        var value = new string('а', 20_000) + "волк";

        // Обход с возвратом вместо рекурсии: длинное описание не должно ронять приложение.
        Assert.True(SqlLike.Matches("%волк", value));
        Assert.False(SqlLike.Matches("%лис", value));
    }
}
