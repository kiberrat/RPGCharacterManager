using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Core.Abstractions.Campaigns;
using RPGCharacterManager.Core.Abstractions.Master;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Master;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.Master;

/// <summary>
/// Окружение проверок режима мастера.
///
/// Службы соединяются так же, как в приложении: настоящая база данных, настоящая
/// служба эффектов и настоящий движок формул с предсказуемым кубиком.
/// </summary>
internal sealed class MasterTestContext : IAsyncDisposable
{
    /// <summary>Значение, выпадающее на каждом кубике проверок.</summary>
    public const int DiceValue = 10;

    private readonly CharacterTestContext _characters;

    private MasterTestContext(CharacterTestContext characters)
    {
        _characters = characters;

        Service = new MasterService(
            characters.ContextFactory,
            characters.Effects,
            characters.Builder,
            characters.Formulas,
            NullLogger<MasterService>.Instance);
    }

    /// <summary>Служба режима мастера.</summary>
    public IMasterService Service { get; }

    /// <summary>
    /// Создаёт окружение проверки.
    /// </summary>
    /// <returns>Готовое окружение.</returns>
    public static async Task<MasterTestContext> CreateAsync() =>
        new(await CharacterTestContext.CreateWithDiceAsync(DiceValue));

    /// <summary>
    /// Создаёт игровую систему.
    /// </summary>
    /// <param name="initiativeFormula">Формула инициативы; пусто — порядка хода нет.</param>
    /// <param name="name">Название системы.</param>
    /// <returns>Идентификатор игровой системы.</returns>
    public async Task<Guid> CreateGameSystemAsync(string? initiativeFormula = null, string name = "Проверка")
    {
        var system = new GameSystem
        {
            Name = name,
            SystemName = name.ToUpperInvariant(),
            InitiativeFormula = initiativeFormula,
        };

        await _characters.AddAsync(system);

        return system.Id;
    }

    /// <summary>
    /// Создаёт персонажа напрямую в базе данных.
    /// </summary>
    /// <param name="name">Имя персонажа.</param>
    /// <param name="level">Уровень персонажа.</param>
    /// <param name="gameSystemId">Игровая система персонажа.</param>
    /// <param name="isTemplate">Персонаж является заготовкой.</param>
    /// <returns>Идентификатор персонажа.</returns>
    public async Task<Guid> CreateCharacterAsync(
        string name,
        int level = 1,
        Guid? gameSystemId = null,
        bool isTemplate = false)
    {
        var character = new Character
        {
            Name = name,
            Level = level,
            GameSystemId = gameSystemId,
            IsTemplate = isTemplate,
        };

        await _characters.AddAsync(character);

        return character.Id;
    }

    /// <summary>
    /// Создаёт ресурс и выдаёт его персонажам.
    /// </summary>
    /// <param name="name">Название ресурса.</param>
    /// <param name="maximum">Максимальное значение.</param>
    /// <param name="current">Текущее значение.</param>
    /// <param name="characterIds">Персонажи, которым выдаётся ресурс.</param>
    /// <returns>Идентификатор ресурса.</returns>
    public async Task<Guid> CreateResourceAsync(
        string name,
        double maximum,
        double current,
        params Guid[] characterIds)
    {
        var resource = new GameResource
        {
            Name = name,
            SystemName = name.ToUpperInvariant(),
            MaximumFormula = maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        await _characters.AddAsync(resource);

        foreach (var characterId in characterIds)
        {
            await _characters.AddAsync(new CharacterResource
            {
                CharacterId = characterId,
                ResourceId = resource.Id,
                Current = current,
                Maximum = maximum,
            });
        }

        return resource.Id;
    }

    /// <summary>
    /// Создаёт эффект.
    /// </summary>
    /// <param name="name">Название эффекта.</param>
    /// <param name="tone">Окраска эффекта.</param>
    /// <returns>Идентификатор эффекта.</returns>
    public async Task<Guid> CreateEffectAsync(string name = "Благословение", EffectTone tone = EffectTone.Positive)
    {
        var effect = CharacterContent.Effect(name, name.ToUpperInvariant(), tone);

        await _characters.AddAsync(effect);

        return effect.Id;
    }

    /// <summary>
    /// Создаёт кампанию с указанным составом персонажей.
    /// </summary>
    /// <param name="name">Название кампании.</param>
    /// <param name="characterIds">Персонажи, входящие в состав.</param>
    /// <returns>Идентификатор кампании.</returns>
    public async Task<Guid> CreateCampaignAsync(string name, params Guid[] characterIds)
    {
        var campaign = new Campaign { Name = name };

        await _characters.AddAsync(campaign);

        foreach (var characterId in characterIds)
        {
            await _characters.AddAsync(new CampaignMember
            {
                CampaignId = campaign.Id,
                ObjectKind = CampaignObjectKinds.Characters,
                ObjectId = characterId,
                Role = "Игрок",
            });
        }

        return campaign.Id;
    }

    /// <summary>
    /// Возвращает сводку мастера.
    /// </summary>
    /// <param name="campaignId">Кампания отбора.</param>
    /// <returns>Сводка мастера.</returns>
    public async Task<MasterBoard> GetBoardAsync(Guid? campaignId = null)
    {
        var result = await Service.GetBoardAsync(campaignId);

        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    /// <summary>
    /// Возвращает текущее значение ресурса персонажа.
    /// </summary>
    /// <param name="characterId">Персонаж.</param>
    /// <param name="resourceId">Ресурс.</param>
    /// <returns>Текущее значение.</returns>
    public async Task<double> GetResourceAsync(Guid characterId, Guid resourceId)
    {
        await using var context = await _characters.CreateContextAsync();

        return await context.Set<CharacterResource>()
            .Where(value => value.CharacterId == characterId && value.ResourceId == resourceId)
            .Select(value => value.Current)
            .SingleAsync();
    }

    /// <summary>
    /// Возвращает записи журнала персонажа.
    /// </summary>
    /// <param name="characterId">Персонаж.</param>
    /// <returns>Записи журнала.</returns>
    public Task<IReadOnlyList<HistoryEntry>> GetHistoryAsync(Guid characterId) =>
        _characters.LoadHistoryAsync(characterId);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _characters.DisposeAsync();
}
