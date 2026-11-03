using Microsoft.Extensions.Options;
using YazioTool.Mcp.Security.OAuth;

namespace YazioTool.Mcp.Security;

/// <summary>
/// Protects <c>/mcp</c>. Accepts the static key as <c>Authorization: Bearer &lt;key&gt;</c> or, for clients
/// that cannot send headers, as the <c>key</c> query parameter, and access tokens issued by the OAuth flow.
/// </summary>
internal sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _options;
    private readonly OAuthTokens _tokens;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options, OAuthTokens tokens, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _tokens = tokens;
        if (string.IsNullOrEmpty(_options.ApiKey))
            logger.LogWarning("Mcp:ApiKey is not set: the MCP endpoint is accessible without authentication.");
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (!_tokens.Enabled || !context.Request.Path.StartsWithSegments("/mcp") || IsAuthorized(context.Request))
            return _next(context);

        var metadataUrl = OAuthUrls.BaseUrl(context.Request, _options) + OAuthUrls.ProtectedResourceMetadataPath + "/mcp";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{metadataUrl}\"";
        return Task.CompletedTask;
    }

    private bool IsAuthorized(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return _options.Matches(request.Query["key"].ToString());

        var provided = header["Bearer ".Length..].Trim();
        return _options.Matches(provided) || _tokens.Read(provided, TokenType.Access) is not null;
    }
}
