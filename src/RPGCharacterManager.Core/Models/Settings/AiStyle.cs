namespace RPGCharacterManager.Core.Models.Settings;

/// <summary>
/// Стиль ответов помощника.
/// Перечень задан документом 024_AI_Помощник.md.
/// </summary>
public enum AiStyle
{
    /// <summary>Кратко: только суть, без пояснений.</summary>
    Brief = 0,

    /// <summary>Подробно: с пояснениями и примерами.</summary>
    Detailed = 1,

    /// <summary>Как мастер игры: живым языком, с художественными описаниями.</summary>
    GameMaster = 2,

    /// <summary>Технически: значениями полей, формулами и точными названиями.</summary>
    Technical = 3,
}
