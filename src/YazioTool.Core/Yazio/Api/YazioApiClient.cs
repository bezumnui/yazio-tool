using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;

namespace YazioTool.Core.Yazio.Api;

internal sealed class YazioApiClient(IHttpClientFactory httpClientFactory) : IYazioApiClient
{
    public const string HttpClientName = "yazio-api";

    private HttpClient Http => httpClientFactory.CreateClient(HttpClientName);

    public Task<ConsumedItemsDto> GetConsumedItemsAsync(DateOnly date, CancellationToken cancellationToken) =>
        GetAsync($"user/consumed-items?date={YazioFormats.FormatDate(date)}", YazioJsonContext.Default.ConsumedItemsDto, cancellationToken);

    public Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken) =>
        GetAsync($"user/widgets/daily-summary?date={YazioFormats.FormatDate(date)}", YazioJsonContext.Default.DailySummaryDto, cancellationToken);

    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync($"products/{productId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync(YazioJsonContext.Default.ProductDto, cancellationToken);
    }

    public async Task AddConsumedItemsAsync(AddConsumedItemsRequest request, CancellationToken cancellationToken)
    {
        using var response = await Http.PostAsJsonAsync("user/consumed-items", request, YazioJsonContext.Default.AddConsumedItemsRequest, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);
    }

    public async Task DeleteConsumedItemsAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "user/consumed-items")
        {
            Content = JsonContent.Create(entryIds, YazioJsonContext.Default.IReadOnlyCollectionGuid),
        };
        using var response = await Http.SendAsync(request, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);
    }

    public Task<Dictionary<string, StreakDayDto>> GetStreakAsync(CancellationToken cancellationToken) =>
        GetAsync("user/streak", YazioJsonContext.Default.DictionaryStringStreakDayDto, cancellationToken);

    public async Task SetStreakDayAsync(DateOnly date, StreakDayDto day, CancellationToken cancellationToken)
    {
        using var response = await Http.PostAsJsonAsync($"user/streak/{YazioFormats.FormatDate(date)}", day, YazioJsonContext.Default.StreakDayDto, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string uri, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(uri, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken)
            ?? throw new YazioApiException(response.StatusCode, $"YAZIO returned an empty body for {uri}.");
    }
}
