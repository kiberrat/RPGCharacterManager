using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Shared;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Ai;

/// <summary>
/// Клиент службы языковых моделей.
///
/// Поддерживаются Groq и OpenRouter: обе отвечают в формате, совместимом
/// с описанием OpenAI, поэтому выбор службы меняет только адрес, ключ и состав
/// доступных моделей. Всё читается из настроек при каждом обращении: пользователь
/// может переключить службу, и следующий запрос уйдёт уже к ней.
/// </summary>
public sealed class AiChatClient : IAiClient, IDisposable
{
    private const string CompletionsPath = "chat/completions";
    private const string ModelsPath = "models";
    private const string CheckQuestion = "Ответь одним словом на русском языке: работает?";
    private const int RequestTimeoutSeconds = 180;

    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly ILogger<AiChatClient> _logger;

    /// <summary>
    /// Создаёт клиент службы языковых моделей.
    /// </summary>
    /// <param name="settings">Служба пользовательских настроек.</param>
    /// <param name="logger">Журналировщик.</param>
    public AiChatClient(ISettingsService settings, ILogger<AiChatClient> logger)
    {
        _settings = Guard.NotNull(settings);
        _logger = Guard.NotNull(logger);

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds) };

        // OpenRouter просит приложение представляться. Название одно и то же для
        // всех служб, сведений о пользователе в нём нет.
        _http.DefaultRequestHeaders.Add("X-Title", ApplicationConstants.ApplicationName);
    }

    /// <inheritdoc />
    public bool IsConfigured => _settings.Current.GetAiKey() is not null;

    /// <inheritdoc />
    public string Model => _settings.Current.GetAiModel() ?? Profile.DefaultModel;

    /// <inheritdoc />
    public AiServiceInfo Service => Profile.Info;

    /// <inheritdoc />
    public IReadOnlyList<string> RecommendedModels => Profile.RecommendedModels;

    private AiProviderProfile Profile => AiProviderProfile.Of(_settings.Current.AiProvider);

    /// <inheritdoc />
    public async Task<Result<AiReply>> CompleteAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        var model = Model;
        var body = ChatProtocol.BuildRequest(model, request).ToJsonString();
        var response = await SendAsync(CompletionsPath, body, cancellationToken).ConfigureAwait(false);

        if (response.IsFailure)
        {
            AiLog.RequestFailed(_logger, model, response.Error!);
            return Result.Failure<AiReply>(response.Error!);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Value);

            return Result.Success(ChatProtocol.ReadReply(document.RootElement));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            AiLog.RequestFailed(_logger, model, exception.Message);
            return Result.Failure<AiReply>("Служба вернула ответ в непонятном виде.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<AiConnection>> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Result.Failure<AiConnection>(NoKeyMessage());
        }

        var watch = Stopwatch.StartNew();

        var models = await GetModelsAsync(cancellationToken).ConfigureAwait(false);

        if (models.IsFailure)
        {
            // Неудачная проверка связи — единственный след работы помощника, который
            // остаётся в журнале при отказе службы: обычные запросы до неё не доходят.
            AiLog.RequestFailed(_logger, Model, models.Error!);

            return Result.Failure<AiConnection>(models.Error!);
        }

        // Список моделей подтверждает только ключ. Настоящая проверка — короткий
        // вопрос выбранной модели: он показывает, что и модель существует,
        // и ответ приходит на русском языке.
        var request = new AiRequest([AiMessage.User(CheckQuestion)]) { Temperature = 0 };
        var reply = await CompleteAsync(request, cancellationToken).ConfigureAwait(false);

        watch.Stop();

        if (reply.IsFailure)
        {
            return Result.Failure<AiConnection>(reply.Error!);
        }

        AiLog.ConnectionChecked(_logger, Model, watch.ElapsedMilliseconds, models.Value.Count);

        return Result.Success(new AiConnection(
            Model,
            models.Value.Count,
            watch.Elapsed,
            reply.Value.Text));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> GetModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = Profile;
        var response = await SendAsync(ModelsPath, body: null, cancellationToken).ConfigureAwait(false);

        if (response.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(response.Error!);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Value);

            return Result.Success(
                ChatProtocol.ReadModels(document.RootElement, profile.Info.FreeModelsOnly));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            AiLog.RequestFailed(_logger, Model, exception.Message);

            return Result.Failure<IReadOnlyList<string>>("Служба вернула список моделей в непонятном виде.");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Выполняет обращение к службе и возвращает тело ответа.
    /// </summary>
    /// <param name="path">Путь запроса относительно адреса службы.</param>
    /// <param name="body">Тело запроса; <see langword="null"/> для чтения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Тело ответа либо понятное пользователю описание ошибки.</returns>
    private async Task<Result<string>> SendAsync(
        string path,
        string? body,
        CancellationToken cancellationToken)
    {
        var profile = Profile;
        var key = _settings.Current.GetAiKey();

        if (key is null)
        {
            return Result.Failure<string>(NoKeyMessage());
        }

        using var message = new HttpRequestMessage(
            body is null ? HttpMethod.Get : HttpMethod.Post,
            new Uri(profile.Address, path));

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        if (body is not null)
        {
            message.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _http
                .SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? Result.Success(content)
                : Result.Failure<string>(Describe(profile, response.StatusCode, content));
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure<string>(
                $"Не удалось связаться со службой {profile.Info.Title}: {exception.Message}. " +
                "Проверьте подключение к сети.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<string>(
                $"Служба не ответила за {RequestTimeoutSeconds} секунд. " +
                "Попробуйте ещё раз или выберите модель полегче.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Ответы служб разнообразнее, чем их описания: непредусмотренный вид
            // ответа не должен обрывать работу приложения. Раздел «AI» —
            // не повод потерять несохранённые изменения в остальных разделах.
            AiLog.AiOperationFailed(_logger, exception);

            return Result.Failure<string>(
                $"Непредвиденный сбой при обращении к службе {profile.Info.Title}: {exception.Message}");
        }
    }

    /// <summary>
    /// Сообщает, что ключ выбранной службы не задан.
    /// </summary>
    /// <returns>Текст сообщения.</returns>
    private string NoKeyMessage()
    {
        var info = Profile.Info;

        return $"Ключ службы {info.Title} не задан. Откройте «Настройки» → «Помощник» " +
            $"и введите ключ, полученный на {info.KeyPage}.";
    }

    /// <summary>
    /// Превращает ответ службы об ошибке в понятное пользователю сообщение.
    /// </summary>
    /// <param name="profile">Профиль службы.</param>
    /// <param name="status">Код состояния ответа.</param>
    /// <param name="content">Тело ответа.</param>
    /// <returns>Текст сообщения.</returns>
    private static string Describe(AiProviderProfile profile, HttpStatusCode status, string content)
    {
        var detail = ChatProtocol.ReadError(content);

        return status switch
        {
            HttpStatusCode.Unauthorized =>
                $"Ключ службы {profile.Info.Title} не принят. Проверьте ключ в настройках.",

            HttpStatusCode.PaymentRequired =>
                "Модель требует оплаты. Выберите бесплатную модель — " +
                "приложение показывает в списке только их.",

            HttpStatusCode.Forbidden =>
                $"Доступ закрыт. Проверьте состояние учётной записи {profile.Info.Title}.",

            HttpStatusCode.NotFound =>
                $"Модель недоступна: {detail ?? "её нет в списке службы"}. " +
                "Нажмите «Обновить список» и выберите модель заново.",

            HttpStatusCode.RequestEntityTooLarge =>
                "Запрос получился слишком большим. Разберите книгу по частям или очистите беседу.",

            // Бесплатные пределы считаются для каждой модели отдельно, поэтому
            // самый быстрый выход — не ждать, а выбрать другую модель.
            HttpStatusCode.TooManyRequests =>
                "Исчерпан бесплатный предел обращений к этой модели. " +
                $"{detail ?? "Подождите или выберите в настройках другую модель."} " +
                "Предел считается для каждой модели отдельно.",

            HttpStatusCode.BadRequest =>
                $"Служба отклонила запрос: {detail ?? "неизвестная причина"}.",

            _ => $"Служба ответила ошибкой {(int)status}: {detail ?? status.ToString()}.",
        };
    }
}
