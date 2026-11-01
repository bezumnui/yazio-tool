using YazioTool.Core.Abstractions;
using YazioTool.Core.Domain;
using YazioTool.Core.Yazio.Api;

namespace YazioTool.Core.Yazio.Diary;

internal sealed class YazioDiaryWriter(IYazioApiClient api) : IDiaryWriter
{
    public async Task<Guid> AddQuickEntryAsync(QuickEntry entry, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var simpleProduct = new NewSimpleProductDto(
            id,
            entry.Name,
            YazioFormats.FormatDateTime(entry.ConsumedAt),
            YazioFormats.FormatDaytime(entry.Meal),
            YazioFormats.FromNutrition(entry.Nutrition),
            IsAiGenerated: false);

        await api.AddConsumedItemsAsync(new AddConsumedItemsRequest([], [], [simpleProduct]), cancellationToken);
        return id;
    }

    public Task RemoveEntriesAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken = default) =>
        entryIds.Count == 0 ? Task.CompletedTask : api.DeleteConsumedItemsAsync(entryIds, cancellationToken);
}
