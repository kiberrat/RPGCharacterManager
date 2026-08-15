using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Search;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Search;

/// <summary>
/// Глобальный поиск: собирает находки всех поставщиков в один список.
///
/// Служба ничего не ищет сама и не знает, где что лежит. Каждая подсистема
/// отвечает за себя своим поставщиком, поэтому подсистема будущего этапа
/// попадает в поиск регистрацией и ничего здесь не меняет (решение Р-96).
/// </summary>
public sealed class SearchService : ISearchService
{
    private readonly IReadOnlyList<ISearchProvider> _providers;
    private readonly ILogger<SearchService> _logger;

    /// <summary>
    /// Создаёт службу поиска.
    /// </summary>
    /// <param name="providers">Поставщики находок.</param>
    /// <param name="logger">Журналировщик.</param>
    public SearchService(IEnumerable<ISearchProvider> providers, ILogger<SearchService> logger)
    {
        _providers = [.. Guard.NotNull(providers).OrderBy(provider => provider.Order)];
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<SearchResult>> SearchAsync(
        string query,
        int limit = SearchDefaults.GroupLimit,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed.Length < SearchDefaults.MinimumQueryLength)
        {
            return Result.Success(new SearchResult(trimmed, []));
        }

        var groups = new List<SearchGroup>();

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var found = await provider
                    .SearchAsync(trimmed, limit, cancellationToken).ConfigureAwait(false);

                groups.AddRange(found.Where(group => group.Hits.Count > 0));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Отказ одного поставщика не отменяет остальных: поиск,
                // молчащий из-за одной подсистемы, бесполезен целиком.
                SearchLog.ProviderFailed(_logger, exception, provider.GetType().Name);
            }
        }

        var ordered = groups
            .OrderBy(group => group.Order)
            .ThenBy(group => group.Title, StringComparer.CurrentCulture)
            .ToList();

        SearchLog.SearchCompleted(_logger, trimmed, ordered.Sum(group => group.Hits.Count));

        return Result.Success(new SearchResult(trimmed, ordered));
    }
}
