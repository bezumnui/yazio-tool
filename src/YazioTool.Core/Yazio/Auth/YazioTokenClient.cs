using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YazioTool.Core.Yazio.Api;

namespace YazioTool.Core.Yazio.Auth;

internal sealed class YazioTokenClient(IHttpClientFactory httpClientFactory, IOptions<YazioOptions> options)
    : IYazioTokenClient
{
    public const string HttpClientName = "yazio-oauth";

    public Task<TokenResponse> RequestWithPasswordAsync(string username, string password, CancellationToken cancellationToken) =>
        RequestAsync(new()
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
        }, cancellationToken);

    public Task<TokenResponse> RequestWithRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestAsync(new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, cancellationToken);

    private async Task<TokenResponse> RequestAsync(Dictionary<string, string> grant, CancellationToken cancellationToken)
    {
        grant["client_id"] = options.Value.ClientId;
        grant["client_secret"] = options.Value.ClientSecret;

        var http = httpClientFactory.CreateClient(HttpClientName);
        using var response = await http.PostAsJsonAsync("oauth/token", grant, YazioJsonContext.Default.DictionaryStringString, cancellationToken);
        await YazioApiException.ThrowIfFailedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync(YazioJsonContext.Default.TokenResponse, cancellationToken)
            ?? throw new YazioApiException(response.StatusCode, "YAZIO returned an empty token response.");
    }
}
