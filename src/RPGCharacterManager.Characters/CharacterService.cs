using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Хранение созданных персонажей.
/// </summary>
public sealed class CharacterService : ICharacterService
{
    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CharacterService> _logger;

    /// <summary>
    /// Создаёт службу персонажей.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public CharacterService(
        IDbContextFactory<RpgDbContext> contextFactory,
        IEventBus eventBus,
        ILogger<CharacterService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<PagedResult<CharacterListItem>> SearchAsync(
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Characters.AsNoTracking().Where(character => !character.IsTemplate);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(character => EF.Functions.Like(character.Name, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Выбираются только отображаемые поля: список рассчитан на десятки тысяч
        // персонажей, загружать их целиком для отрисовки строк недопустимо.
        var items = await query
            .OrderBy(character => character.Name)
            .ThenBy(character => character.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(character => new CharacterListItem(
                character.Id,
                character.Name,
                character.Level,
                character.GameSystem == null ? null : character.GameSystem.Name,
                character.Race == null ? null : character.Race.Name,
                character.Class == null ? null : character.Class.Name,
                character.Portrait,
                character.ModifiedAt ?? character.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<CharacterListItem>(items, totalCount, pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<Character?> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await LoadWithRelatedData(context.Characters)
            .AsNoTracking()
            .FirstOrDefaultAsync(character => character.Id == characterId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await context.Characters
                .FirstOrDefaultAsync(item => item.Id == characterId, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure("Персонаж не найден: возможно, он уже удалён.");
            }

            context.Characters.Remove(character);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CharacterLog.CharacterDeleted(_logger, character.Name);

            await _eventBus
                .PublishAsync(
                    new CharacterChangedEvent(characterId, CharacterChangeKind.Deleted),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.CharacterUpdateFailed(_logger, exception, characterId);

            return Result.Failure($"Не удалось удалить персонажа: {exception.Message}");
        }
    }

    /// <summary>
    /// Добавляет к запросу связанные данные персонажа.
    /// Используется всюду, где персонаж загружается целиком: для пересчёта
    /// требуются все его характеристики, навыки, черты, заклинания и ресурсы.
    /// </summary>
    /// <param name="query">Исходный запрос.</param>
    /// <returns>Запрос со связанными данными.</returns>
    internal static IQueryable<Character> LoadWithRelatedData(IQueryable<Character> query) => query
        .Include(character => character.Race)
        .Include(character => character.Class)
        .Include(character => character.Subclass)
        .Include(character => character.Background)
        .Include(character => character.Attributes)
        .Include(character => character.Skills)
        .Include(character => character.Traits)
        .Include(character => character.CustomAbilities)
        .Include(character => character.Currencies)
        .Include(character => character.Spells)
        .Include(character => character.Resources);
}
