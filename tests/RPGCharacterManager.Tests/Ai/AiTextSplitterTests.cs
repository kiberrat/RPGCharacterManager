using System.Text;
using RPGCharacterManager.Ai;

namespace RPGCharacterManager.Tests.Ai;

/// <summary>
/// Проверка разбиения книги на части.
/// </summary>
public sealed class AiTextSplitterTests
{
    private const int Size = 200;

    private static string Book(int paragraphs)
    {
        var builder = new StringBuilder();

        for (var index = 1; index <= paragraphs; index++)
        {
            builder.Append("Оружие номер ").Append(index)
                .Append(": длинное описание, занимающее место в строке абзаца.")
                .AppendLine().AppendLine();
        }

        return builder.ToString();
    }

    [Fact]
    public void Разбиение_ПустойТекст_НетЧастей()
    {
        Assert.Empty(AiTextSplitter.Split(string.Empty));
        Assert.Empty(AiTextSplitter.Split("   "));
    }

    [Fact]
    public void Разбиение_КороткийТекст_ОднаЧасть()
    {
        var parts = AiTextSplitter.Split("Короткое описание оружия.", Size);

        Assert.Equal("Короткое описание оружия.", Assert.Single(parts));
    }

    [Fact]
    public void Разбиение_ДлинныйТекст_НесколькоЧастей()
    {
        var parts = AiTextSplitter.Split(Book(20), Size);

        Assert.True(parts.Count > 1);
        Assert.All(parts, part => Assert.False(string.IsNullOrWhiteSpace(part)));
    }

    [Fact]
    public void Разбиение_ДлинныйТекст_НеТеряетАбзацев()
    {
        var parts = AiTextSplitter.Split(Book(20), Size);
        var joined = string.Join("\n", parts);

        // Описание, разрезанное пополам, не станет объектом ни в одной из частей,
        // поэтому каждый абзац обязан целиком попасть в какую-нибудь часть.
        for (var index = 1; index <= 20; index++)
        {
            var paragraph = $"Оружие номер {index}: длинное описание, занимающее место в строке абзаца.";

            Assert.Contains(paragraph, joined, StringComparison.Ordinal);
            Assert.Contains(parts, part => part.Contains(paragraph, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Разбиение_ТекстБезАбзацев_РежетПоПредложениям()
    {
        var text = string.Concat(Enumerable.Repeat("Ещё одно предложение книги. ", 40));
        var parts = AiTextSplitter.Split(text, Size);

        Assert.True(parts.Count > 1);
        Assert.All(parts, part => Assert.EndsWith(".", part, StringComparison.Ordinal));
    }
}
