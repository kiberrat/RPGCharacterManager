using System.Text.Json.Serialization;
using RPGCharacterManager.Shared;

namespace RPGCharacterManager.Core.Models.Settings;

/// <summary>
/// Пользовательские настройки приложения.
/// Соответствует таблице <c>Settings</c> из документа 004_База_данных.md
/// и сохраняется в файле настроек профиля пользователя.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Минимально допустимый масштаб интерфейса.</summary>
    public const double MinimumInterfaceScale = 0.75;

    /// <summary>Максимально допустимый масштаб интерфейса.</summary>
    public const double MaximumInterfaceScale = 2.0;

    /// <summary>Минимально допустимый базовый размер шрифта в пикселях.</summary>
    public const double MinimumFontSize = 10.0;

    /// <summary>Максимально допустимый базовый размер шрифта в пикселях.</summary>
    public const double MaximumFontSize = 24.0;

    /// <summary>Базовый размер шрифта по умолчанию в пикселях.</summary>
    public const double DefaultFontSize = 14.0;


    /// <summary>Интервал создания резервных копий по умолчанию в часах.</summary>
    public const int DefaultBackupIntervalHours = 24;

    /// <summary>Количество хранимых записей журнала бросков по умолчанию.</summary>
    public const int DefaultDiceHistoryLimit = 500;

    /// <summary>Срок хранения резервных копий по умолчанию в днях.</summary>
    public const int DefaultBackupRetentionDays = 30;

    /// <summary>Режим оформления интерфейса.</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    /// <summary>Акцентный цвет интерфейса.</summary>
    public AccentColor Accent { get; set; } = AccentColor.Blue;

    /// <summary>Код языка интерфейса.</summary>
    public string Language { get; set; } = ApplicationConstants.DefaultLanguageCode;

    /// <summary>Базовый размер шрифта интерфейса в пикселях.</summary>
    public double FontSize { get; set; } = DefaultFontSize;

    /// <summary>Дополнительный масштаб интерфейса поверх масштаба Windows.</summary>
    public double InterfaceScale { get; set; } = 1.0;



    /// <summary>Интервал автоматического резервного копирования базы данных в часах.</summary>
    public int BackupIntervalHours { get; set; } = DefaultBackupIntervalHours;

    /// <summary>
    /// Срок хранения резервных копий в днях.
    /// Копии старше указанного срока удаляются командой очистки.
    /// </summary>
    public int BackupRetentionDays { get; set; } = DefaultBackupRetentionDays;

    /// <summary>Максимальное количество хранимых записей журнала бросков.</summary>
    public int DiceHistoryLimit { get; set; } = DefaultDiceHistoryLimit;

    /// <summary>
    /// Показывать полёт кубика при броске.
    /// Выключение оставляет только итог: бросок завершается мгновенно.
    /// </summary>
    public bool DiceAnimationEnabled { get; set; } = true;

    /// <summary>Служба языковых моделей, к которой обращается помощник.</summary>
    public AiProvider AiProvider { get; set; } = AiProvider.Groq;

    /// <summary>
    /// Ключ доступа к службе Groq.
    ///
    /// Ключ принадлежит пользователю и хранится в его файле настроек рядом с базой
    /// данных. Приложение не передаёт его никуда, кроме самой службы, и не
    /// записывает в журналы: подсистема журналирования получает только имя модели.
    ///
    /// Имя в файле настроек оставлено прежним: ключ, введённый до появления второй
    /// службы, продолжает работать без повторного ввода.
    /// </summary>
    [JsonPropertyName("AiApiKey")]
    public string? AiGroqKey { get; set; }

    /// <summary>Ключ доступа к службе OpenRouter.</summary>
    public string? AiOpenRouterKey { get; set; }

    /// <summary>Ключ доступа к службе Google AI Studio.</summary>
    public string? AiGoogleKey { get; set; }

    /// <summary>Имя языковой модели, используемой помощником в службе Groq.</summary>
    public string? AiModel { get; set; }

    /// <summary>Имя языковой модели, используемой помощником в службе OpenRouter.</summary>
    public string? AiOpenRouterModel { get; set; }

    /// <summary>Имя языковой модели, используемой помощником в службе Google AI Studio.</summary>
    public string? AiGoogleModel { get; set; }

    /// <summary>Стиль ответов помощника.</summary>
    public AiStyle AiStyle { get; set; } = AiStyle.Detailed;

    /// <summary>Идентификатор игровой системы, выбираемой по умолчанию.</summary>
    public Guid? DefaultGameSystemId { get; set; }

    /// <summary>Имя рабочего пространства, открываемого при запуске.</summary>
    public string? DefaultWorkspace { get; set; }

    /// <summary>
    /// Возвращает ключ доступа к выбранной службе.
    ///
    /// Ключи хранятся отдельно для каждой службы: переключение туда и обратно
    /// не должно требовать повторного ввода.
    /// </summary>
    /// <returns>Ключ либо <see langword="null"/>, если он не задан.</returns>
    public string? GetAiKey()
    {
        var key = AiProvider switch
        {
            AiProvider.OpenRouter => AiOpenRouterKey,
            AiProvider.GoogleAi => AiGoogleKey,
            _ => AiGroqKey,
        };

        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    /// <summary>
    /// Записывает ключ доступа к выбранной службе.
    /// </summary>
    /// <param name="key">Ключ доступа.</param>
    public void SetAiKey(string? key)
    {
        var value = string.IsNullOrWhiteSpace(key) ? null : key.Trim();

        switch (AiProvider)
        {
            case AiProvider.OpenRouter:
                AiOpenRouterKey = value;
                break;

            case AiProvider.GoogleAi:
                AiGoogleKey = value;
                break;

            default:
                AiGroqKey = value;
                break;
        }
    }

    /// <summary>
    /// Возвращает модель, выбранную для текущей службы.
    ///
    /// Платная модель, оставшаяся в настройках, для OpenRouter не возвращается:
    /// приложение работает там только с бесплатными.
    /// </summary>
    /// <returns>Имя модели либо <see langword="null"/>, если выбор не сделан.</returns>
    public string? GetAiModel()
    {
        var model = AiProvider switch
        {
            AiProvider.OpenRouter => AiOpenRouterModel,
            AiProvider.GoogleAi => AiGoogleModel,
            _ => AiModel,
        };

        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var trimmed = model.Trim();

        return AiProviders.RequiresFreeModels(AiProvider) && !AiProviders.IsFreeModel(trimmed)
            ? null
            : trimmed;
    }

    /// <summary>
    /// Записывает модель, выбранную для текущей службы.
    /// </summary>
    /// <param name="model">Имя модели.</param>
    public void SetAiModel(string? model)
    {
        var value = string.IsNullOrWhiteSpace(model) ? null : model.Trim();

        switch (AiProvider)
        {
            case AiProvider.OpenRouter:
                AiOpenRouterModel = value;
                break;

            case AiProvider.GoogleAi:
                AiGoogleModel = value;
                break;

            default:
                AiModel = value;
                break;
        }
    }

    /// <summary>
    /// Создаёт независимую копию настроек.
    /// Используется для редактирования настроек с возможностью отмены.
    /// </summary>
    /// <returns>Копия текущих настроек.</returns>
    public AppSettings Clone() => new()
    {
        Theme = Theme,
        Accent = Accent,
        Language = Language,
        FontSize = FontSize,
        InterfaceScale = InterfaceScale,
        BackupIntervalHours = BackupIntervalHours,
        BackupRetentionDays = BackupRetentionDays,
        DiceHistoryLimit = DiceHistoryLimit,
        DiceAnimationEnabled = DiceAnimationEnabled,
        AiProvider = AiProvider,
        AiGroqKey = AiGroqKey,
        AiOpenRouterKey = AiOpenRouterKey,
        AiGoogleKey = AiGoogleKey,
        AiModel = AiModel,
        AiOpenRouterModel = AiOpenRouterModel,
        AiGoogleModel = AiGoogleModel,
        AiStyle = AiStyle,
        DefaultGameSystemId = DefaultGameSystemId,
        DefaultWorkspace = DefaultWorkspace,
    };

    /// <summary>
    /// Приводит значения настроек к допустимым диапазонам.
    /// Вызывается после загрузки файла настроек, который мог быть изменён вручную.
    /// </summary>
    public void Normalize()
    {
        FontSize = Math.Clamp(FontSize, MinimumFontSize, MaximumFontSize);
        InterfaceScale = Math.Clamp(InterfaceScale, MinimumInterfaceScale, MaximumInterfaceScale);
        BackupIntervalHours = Math.Max(1, BackupIntervalHours);
        BackupRetentionDays = Math.Max(1, BackupRetentionDays);
        DiceHistoryLimit = Math.Max(0, DiceHistoryLimit);

        if (string.IsNullOrWhiteSpace(Language))
        {
            Language = ApplicationConstants.DefaultLanguageCode;
        }
    }
}
