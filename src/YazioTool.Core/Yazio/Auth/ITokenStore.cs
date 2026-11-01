namespace YazioTool.Core.Yazio.Auth;

public sealed record StoredToken(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>Persists YAZIO tokens between process runs.</summary>
public interface ITokenStore
{
    Task<StoredToken?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(StoredToken token, CancellationToken cancellationToken);
}
