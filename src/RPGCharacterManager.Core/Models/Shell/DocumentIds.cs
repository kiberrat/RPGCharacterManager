namespace RPGCharacterManager.Core.Models.Shell;

/// <summary>
/// Идентификаторы документов рабочей области.
///
/// Объявлены в контрактах, потому что документ открывают не только его
/// собственный раздел: глобальный поиск переходит к находке, а подсистемы
/// ссылаются друг на друга. Сама навигация уже работает со строковым
/// идентификатором, и перечень собран здесь, чтобы он был один.
/// </summary>
public static class DocumentIds
{
    /// <summary>Обзор приложения.</summary>
    public const string Overview = "core.overview";

    /// <summary>Настройки.</summary>
    public const string Settings = "core.settings";

    /// <summary>Обратная связь с разработчиком.</summary>
    public const string Feedback = "core.feedback";

    /// <summary>Резервные копии.</summary>
    public const string Backups = "core.backups";

    /// <summary>Конструктор правил.</summary>
    public const string Rules = "core.rules";

    /// <summary>Менеджер контента.</summary>
    public const string Content = "core.content";

    /// <summary>Журнал событий.</summary>
    public const string Journal = "core.journal";

    /// <summary>Статистика игры.</summary>
    public const string Statistics = "statistics.report";

    /// <summary>Расширения приложения.</summary>
    public const string Extensions = "extensions.list";

    /// <summary>Список персонажей.</summary>
    public const string Characters = "characters.list";

    /// <summary>Мастер создания персонажа.</summary>
    public const string CharacterWizard = "characters.wizard";

    /// <summary>Лист персонажа.</summary>
    public const string CharacterSheet = "characters.sheet";

    /// <summary>Кампании.</summary>
    public const string Campaigns = "campaigns.list";

    /// <summary>Режим мастера.</summary>
    public const string Master = "master.board";

    /// <summary>Редактор интерфейса.</summary>
    public const string Layouts = "layouts.editor";

    /// <summary>Макросы.</summary>
    public const string Macros = "macros.list";

    /// <summary>Глобальный поиск.</summary>
    public const string Search = "search.results";

    /// <summary>Помощник.</summary>
    public const string Ai = "ai.assistant";
}
