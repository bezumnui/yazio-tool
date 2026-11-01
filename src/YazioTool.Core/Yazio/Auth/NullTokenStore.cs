namespace YazioTool.Core.Yazio.Auth;

/// <summary>For long-running hosts: the token lives only in memory of the provider.</summary>
internal sealed class NullTokenStore : ITokenStore
{
    public Task<StoredToken?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<StoredToken?>(null);

    public Task SaveAsync(StoredToken token, CancellationToken cancellationToken) => Task.CompletedTask;
}
