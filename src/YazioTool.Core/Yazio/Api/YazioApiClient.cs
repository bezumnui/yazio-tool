using System.Net;
using System.Net.Http.Json;

namespace YazioTool.Core.Yazio.Api;

internal sealed class YazioApiClient(IHttpClientFactory httpClientFactory) : IYazioApiClient
{
    public const string HttpClientName = "yazio-api";

    private HttpClient Http => httpClientFactory.CreateClient(HttpClientName);

    public Task<ConsumedItemsDto> GetConsumedItemsAsync(DateOnly date, CancellationToken cancellationToken) =>
        GetAsync<ConsumedItemsDto>($"user/consumed-items?date={YazioFormats.FormatDate(date)}", cancellationToken);

    public Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken) =>
        GetAsync<DailySummaryDto>($"user/widgets/daily-summary?date={YazioFormats.FormatDate(date)}", cancellationToken);

    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync($"products/{productId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProductDto>(YazioJson.Options, cancellationToken);
    }

    public async Task AddConsumedItemsAsync(AddConsumedItemsRequest request, CancellationToken cancellationToken)
    {
        using var response = await Http.PostAsJsonAsync("user/consumed-items", request, YazioJson.Options, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);
    }

    public async Task DeleteConsumedItemsAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "user/consumed-items")
        {
            Content = JsonContent.Create(entryIds, options: YazioJson.Options),
        };
        using var response = await Http.SendAsync(request, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(uri, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(YazioJson.Options, cancellationToken)
            ?? throw new YazioApiException(response.StatusCode, $"YAZIO returned an empty body for {uri}.");
    }
}
