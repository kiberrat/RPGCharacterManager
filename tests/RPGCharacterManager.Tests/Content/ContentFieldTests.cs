using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Tests.Content;

/// <summary>
/// Проверка полей формы редактирования контента.
/// </summary>
public sealed class ContentFieldTests
{
    private static IContentField Field(string name)
    {
        var descriptor = StandardContentTypes.Create()
            .Single(type => type.Id == ContentTypeIds.Attributes);

        return descriptor.Fields.Single(field => field.Name == name);
    }

    [Fact]
    public void НеобязательноеЧисло_ПустоеЗначение_ОзначаетОтсутствиеОграничения()
    {
        var attribute = new AttributeDefinition { MaximumValue = 20 };
        var maximum = Field("maximum");

        Assert.True(maximum.TrySetText(attribute, string.Empty, out var error), error);

        // Пустой предел не превращается в ноль: иначе характеристику нельзя было бы
        // поднять выше нуля, хотя пользователь ограничения не задавал.
        Assert.Null(attribute.MaximumValue);
    }

    [Fact]
    public void НеобязательноеЧисло_ЗаданноеЗначение_Сохраняется()
    {
        var attribute = new AttributeDefinition();
        var maximum = Field("maximum");

        Assert.True(maximum.TrySetText(attribute, "18", out var error), error);

        Assert.Equal(18, attribute.MaximumValue);
    }

    [Fact]
    public void НеобязательноеЧисло_НеЗадано_ОтображаетсяПустойСтрокой()
    {
        var attribute = new AttributeDefinition { MinimumValue = null };

        Assert.Equal(string.Empty, Field("minimum").GetText(attribute));
    }

    [Fact]
    public void ОбязательноеЧисло_ПустоеЗначение_ОзначаетНоль()
    {
        var attribute = new AttributeDefinition { DefaultValue = 12 };
        var defaultValue = Field("defaultValue");

        Assert.True(defaultValue.TrySetText(attribute, string.Empty, out var error), error);

        Assert.Equal(0, attribute.DefaultValue);
    }
}
