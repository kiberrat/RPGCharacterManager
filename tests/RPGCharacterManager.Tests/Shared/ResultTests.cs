using RPGCharacterManager.Shared.Extensions;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Tests.Shared;

public sealed class ResultTests
{
    [Fact]
    public void Success_СоздаётУспешныйРезультат()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_СохраняетОписаниеОшибки()
    {
        var result = Result.Failure("Файл повреждён");

        Assert.True(result.IsFailure);
        Assert.Equal("Файл повреждён", result.Error);
    }

    [Fact]
    public void SuccessСоЗначением_ВозвращаетЗначение()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Value_БросаетИсключение_ДляНеуспешногоРезультата()
    {
        var result = Result.Failure<int>("Нет данных");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void GetValueOrDefault_ВозвращаетЗапаснойВариант_ПриОшибке()
    {
        var result = Result.Failure<int>("Нет данных");

        Assert.Equal(7, result.GetValueOrDefault(7));
    }
}

public sealed class StringExtensionsTests
{
    [Theory]
    [InlineData("Сила", 10, "Сила")]
    [InlineData("Очень длинное название", 10, "Очень дли…")]
    [InlineData(null, 10, "")]
    public void Truncate_ОбрезаетСтрокуДоУказаннойДлины(string? value, int length, string expected)
    {
        Assert.Equal(expected, value.Truncate(length));
    }

    [Theory]
    [InlineData("Сила", "сила")]
    [InlineData("Класс брони", "класс_брони")]
    [InlineData("  Очки  действий  ", "очки_действий")]
    [InlineData("HP/МП", "hp_мп")]
    [InlineData(null, "")]
    public void ToSystemName_ПриводитИмяКВнутреннемуФормату(string? value, string expected)
    {
        Assert.Equal(expected, value.ToSystemName());
    }

    [Fact]
    public void EqualsIgnoreCase_СравниваетБезУчётаРегистра()
    {
        Assert.True("Ловкость".EqualsIgnoreCase("ЛОВКОСТЬ"));
        Assert.False("Ловкость".EqualsIgnoreCase("Сила"));
    }
}
