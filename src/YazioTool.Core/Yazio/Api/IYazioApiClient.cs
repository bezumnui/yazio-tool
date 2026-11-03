namespace YazioTool.Core.Yazio.Api;

/// <summary>Authenticated, one-to-one access to the YAZIO endpoints used by this tool.</summary>
internal interface IYazioApiClient
{
    Task<ConsumedItemsDto> GetConsumedItemsAsync(DateOnly date, CancellationToken cancellationToken);

    Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken);

    Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    Task AddConsumedItemsAsync(AddConsumedItemsRequest request, CancellationToken cancellationToken);

    Task DeleteConsumedItemsAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken);

    /// <returns>Streak days keyed by yyyy-MM-dd.</returns>
    Task<Dictionary<string, StreakDayDto>> GetStreakAsync(CancellationToken cancellationToken);

    /// <summary>Creates or replaces one streak day.</summary>
    Task SetStreakDayAsync(DateOnly date, StreakDayDto day, CancellationToken cancellationToken);
}
