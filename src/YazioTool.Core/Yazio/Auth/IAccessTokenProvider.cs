namespace YazioTool.Core.Yazio.Auth;

internal interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    /// <summary>Drops the cached token so the next call re-authenticates.</summary>
    void Invalidate();
}
