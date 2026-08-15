using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPGCharacterManager.Core.Abstractions.Distribution;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Infrastructure.Distribution;

/// <summary>Отправляет обратную связь на сервер, не раскрывая получателя в клиенте.</summary>
public sealed class HttpFeedbackService : IFeedbackService, IDisposable
{
    private const int MaximumMessageLength = 5000;
    private const int MaximumContactLength = 200;
    private readonly DistributionOptions _options;
    private readonly ILogger<HttpFeedbackService> _logger;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Создаёт службу отправки.</summary>
    /// <param name="options">Параметры доставки.</param>
    /// <param name="logger">Журналировщик.</param>
    public HttpFeedbackService(IOptions<DistributionOptions> options, ILogger<HttpFeedbackService> logger)
    {
        _options = Guard.NotNull(options).Value;
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public bool IsConfigured => Uri.TryCreate(_options.FeedbackEndpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    /// <inheritdoc />
    public async Task<Result> SendAsync(FeedbackMessage message, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(message);

        if (!IsConfigured)
        {
            return Result.Failure("Отправка будет включена после публикации тестовой версии.");
        }

        var text = message.Message.Trim();
        var contact = string.IsNullOrWhiteSpace(message.Contact) ? null : message.Contact.Trim();

        if (text.Length is < 3 or > MaximumMessageLength)
        {
            return Result.Failure($"Сообщение должно содержать от 3 до {MaximumMessageLength} символов.");
        }

        if (contact?.Length > MaximumContactLength)
        {
            return Result.Failure($"Контакт не должен быть длиннее {MaximumContactLength} символов.");
        }

        var payload = new
        {
            kind = message.Kind.ToString(),
            message = text,
            contact,
            applicationVersion = message.ApplicationVersion,
            technicalInformation = message.TechnicalInformation,
            sentAtUtc = DateTimeOffset.UtcNow,
            website = string.Empty,
        };

        try
        {
            using var response = await _client.PostAsJsonAsync(
                _options.FeedbackEndpoint,
                payload,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                DistributionLog.FeedbackRejected(_logger, (int)response.StatusCode);
                return Result.Failure("Сервер не принял сообщение. Попробуйте немного позже.");
            }

            var confirmation = await response.Content.ReadFromJsonAsync<FeedbackResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return confirmation?.Ok == true
                ? Result.Success()
                : Result.Failure("Сервер не подтвердил отправку сообщения.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure("Сервер обратной связи не ответил вовремя.");
        }
        catch (HttpRequestException exception)
        {
            DistributionLog.FeedbackSendFailed(_logger, exception);
            return Result.Failure("Не удалось отправить сообщение. Проверьте подключение к интернету.");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private sealed record FeedbackResponse(bool Ok);
}