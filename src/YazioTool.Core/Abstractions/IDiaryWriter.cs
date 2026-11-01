using YazioTool.Core.Domain;

namespace YazioTool.Core.Abstractions;

public interface IDiaryWriter
{
    /// <returns>Id of the created diary entry.</returns>
    Task<Guid> AddQuickEntryAsync(QuickEntry entry, CancellationToken cancellationToken = default);

    Task RemoveEntriesAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken = default);
}
