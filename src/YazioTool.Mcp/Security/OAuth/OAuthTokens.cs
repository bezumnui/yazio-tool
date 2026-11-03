using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace YazioTool.Mcp.Security.OAuth;

internal static class TokenType
{
    public const string Client = "client";
    public const string Code = "code";
    public const string Access = "access";
    public const string Refresh = "refresh";
}

/// <summary>Payload of every value <see cref="OAuthTokens"/> signs.</summary>
internal sealed record OAuthToken(
    [property: JsonPropertyName("typ")] string Type,
    [property: JsonPropertyName("jti")] string Id,
    [property: JsonPropertyName("exp")] long? ExpiresAt = null,
    [property: JsonPropertyName("cid")] string? ClientId = null,
    [property: JsonPropertyName("uris")] string[]? RedirectUris = null,
    [property: JsonPropertyName("uri")] string? RedirectUri = null,
    [property: JsonPropertyName("pkce")] string? CodeChallenge = null,
    [property: JsonPropertyName("scope")] string? Scope = null);

/// <summary>
/// Issues and validates the stateless, HMAC-signed values of the OAuth flow: client ids (dynamic registration),
/// authorization codes, access and refresh tokens. Nothing is persisted, so tokens survive restarts.
/// The signing key is derived from <c>Mcp:ApiKey</c>: changing the key revokes every client and token.
/// </summary>
internal sealed class OAuthTokens
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(90);

    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly byte[]? _signingKey;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, long> _redeemedCodes = new();

    public OAuthTokens(IOptions<ApiKeyOptions> options, TimeProvider time)
    {
        var apiKey = options.Value.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
            _signingKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes(apiKey), "yazio-tool/oauth/v1"u8);
        _time = time;
    }

    /// <summary>OAuth is available only when <c>Mcp:ApiKey</c> is set.</summary>
    public bool Enabled => _signingKey is not null;

    public string CreateClient(IEnumerable<string> redirectUris) =>
        Sign(new(TokenType.Client, NewId(), RedirectUris: [.. redirectUris]));

    public string CreateCode(OAuthToken client, string redirectUri, string codeChallenge, string? scope) =>
        Sign(new(TokenType.Code, NewId(), ExpiresIn(CodeLifetime), client.Id, RedirectUri: redirectUri, CodeChallenge: codeChallenge, Scope: scope));

    public string CreateAccessToken(string clientId, string? scope) =>
        Sign(new(TokenType.Access, NewId(), ExpiresIn(AccessTokenLifetime), clientId, Scope: scope));

    public string CreateRefreshToken(string clientId, string? scope) =>
        Sign(new(TokenType.Refresh, NewId(), ExpiresIn(RefreshTokenLifetime), clientId, Scope: scope));

    /// <summary>Returns the payload if <paramref name="value"/> is a valid, unexpired value of <paramref name="type"/>.</summary>
    public OAuthToken? Read(string? value, string type)
    {
        if (_signingKey is null || string.IsNullOrEmpty(value))
            return null;

        var dot = value.IndexOf('.');
        if (dot <= 0)
            return null;

        OAuthToken? token;
        try
        {
            var payload = Base64Url.DecodeFromChars(value.AsSpan(0, dot));
            var signature = Base64Url.DecodeFromChars(value.AsSpan(dot + 1));
            if (!CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(_signingKey, payload)))
                return null;
            token = JsonSerializer.Deserialize<OAuthToken>(payload, JsonOptions);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }

        if (token is null || token.Type != type || token.ExpiresAt <= _time.GetUtcNow().ToUnixTimeSeconds())
            return null;
        return token;
    }

    /// <summary>Marks an authorization code as used; returns false if it was redeemed before.</summary>
    public bool TryRedeem(OAuthToken code)
    {
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        foreach (var (id, expiresAt) in _redeemedCodes)
        {
            if (expiresAt <= now)
                _redeemedCodes.TryRemove(id, out _);
        }

        return _redeemedCodes.TryAdd(code.Id, code.ExpiresAt ?? now);
    }

    /// <summary>Checks a PKCE <c>code_verifier</c> against an S256 <c>code_challenge</c>.</summary>
    public static bool VerifyPkce(string? codeChallenge, string? codeVerifier)
    {
        if (string.IsNullOrEmpty(codeChallenge) || codeVerifier is not { Length: >= 43 and <= 128 })
            return false;

        var expected = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(codeChallenge));
    }

    private string Sign(OAuthToken token)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(token, JsonOptions);
        var signature = HMACSHA256.HashData(_signingKey ?? throw new InvalidOperationException("OAuth is disabled: Mcp:ApiKey is not set."), payload);
        return $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(signature)}";
    }

    private long ExpiresIn(TimeSpan lifetime) => _time.GetUtcNow().Add(lifetime).ToUnixTimeSeconds();

    private static string NewId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
}
