namespace YazioTool.Mcp.Security.OAuth;

internal static class OAuthUrls
{
    public const string ProtectedResourceMetadataPath = "/.well-known/oauth-protected-resource";
    public const string AuthorizationServerMetadataPath = "/.well-known/oauth-authorization-server";
    public const string RegisterPath = "/register";
    public const string AuthorizePath = "/authorize";
    public const string TokenPath = "/token";

    /// <summary><c>Mcp:PublicUrl</c> if set, otherwise the scheme and host of the request.</summary>
    public static string BaseUrl(HttpRequest request, ApiKeyOptions options) =>
        string.IsNullOrEmpty(options.PublicUrl)
            ? $"{request.Scheme}://{request.Host}{request.PathBase}"
            : options.PublicUrl.TrimEnd('/');
}
