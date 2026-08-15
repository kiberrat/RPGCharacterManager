using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Tests.Shared;

public sealed class GuardTests
{
    [Fact]
    public void NotNull_ВозвращаетЗначение_ЕслиОноЗадано()
    {
        var value = new object();

        Assert.Same(value, Guard.NotNull(value));
    }

    [Fact]
    public void NotNull_БросаетИсключение_ЕслиЗначениеNull()
    {
        object? value = null;

        var exception = Assert.Throws<ArgumentNullException>(() => Guard.NotNull(value));
        Assert.Equal(nameof(value), exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NotNullOrWhiteSpace_БросаетИсключение_ДляПустыхСтрок(string? value)
    {
        Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace(value));
    }

    [Fact]
    public void NotNullOrWhiteSpace_ВозвращаетСтроку_ЕслиОнаЗаполнена()
    {
        Assert.Equal("Сила", Guard.NotNullOrWhiteSpace("Сила"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    public void InRange_ВозвращаетЗначение_ВнутриДиапазона(int value)
    {
        Assert.Equal(value, Guard.InRange(value, 1, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void InRange_БросаетИсключение_ЗаПределамиДиапазона(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.InRange(value, 1, 20));
    }
}
