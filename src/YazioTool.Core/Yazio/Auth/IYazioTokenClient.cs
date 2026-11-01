using YazioTool.Core.Yazio.Api;

namespace YazioTool.Core.Yazio.Auth;

/// <summary>Raw access to the YAZIO OAuth token endpoint.</summary>
internal interface IYazioTokenClient
{
    Task<TokenResponse> RequestWithPasswordAsync(string username, string password, CancellationToken cancellationToken);

    Task<TokenResponse> RequestWithRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
}
