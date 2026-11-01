using Microsoft.Extensions.Options;
using YazioTool.Core.Yazio.Api;

namespace YazioTool.Core.Yazio.Auth;

/// <summary>
/// Caches the access token, renews it with the refresh token when it expires
/// and falls back to the password grant when refreshing is not possible.
/// </summary>
internal sealed class YazioAccessTokenProvider(
    IYazioTokenClient tokenClient,
    ITokenStore tokenStore,
    IOptions<YazioOptions> options,
    TimeProvider timeProvider) : IAccessTokenProvider, IDisposable
{
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private StoredToken? _token;
    private bool _storeLoaded;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetValid(out var accessToken))
            return accessToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_storeLoaded)
            {
                _token ??= await tokenStore.LoadAsync(cancellationToken);
                _storeLoaded = true;
            }

            if (TryGetValid(out accessToken))
                return accessToken;

            _token = await AcquireAsync(_token?.RefreshToken, cancellationToken);
            await tokenStore.SaveAsync(_token, cancellationToken);
            return _token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        if (_token is { } token)
            _token = token with { ExpiresAt = DateTimeOffset.MinValue };
    }

    public void Dispose() => _lock.Dispose();

    private bool TryGetValid(out string accessToken)
    {
        var token = _token;
        accessToken = token?.AccessToken ?? "";
        return token is not null && token.ExpiresAt > timeProvider.GetUtcNow() + ExpirySkew;
    }

    private async Task<StoredToken> AcquireAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                return ToStored(await tokenClient.RequestWithRefreshTokenAsync(refreshToken, cancellationToken));
            }
            catch (YazioApiException)
            {
                // Refresh token revoked or expired: log in again with the password.
            }
        }

        var o = options.Value;
        if (string.IsNullOrEmpty(o.Username) || string.IsNullOrEmpty(o.Password))
            throw new YazioCredentialsMissingException();

        return ToStored(await tokenClient.RequestWithPasswordAsync(o.Username, o.Password, cancellationToken));
    }

    private StoredToken ToStored(TokenResponse response) =>
        new(response.AccessToken, response.RefreshToken, timeProvider.GetUtcNow().AddSeconds(response.ExpiresIn));
}
