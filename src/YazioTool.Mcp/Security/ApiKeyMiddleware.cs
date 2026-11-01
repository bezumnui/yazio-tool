using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace YazioTool.Mcp.Security;

/// <summary>
/// Protects <c>/mcp</c> with a static key passed as <c>Authorization: Bearer &lt;key&gt;</c>
/// or, for clients that cannot send headers, as the <c>key</c> query parameter.
/// </summary>
internal sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[]? _expectedKey;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
            logger.LogWarning("Mcp:ApiKey is not set: the MCP endpoint is accessible without authentication.");
        else
            _expectedKey = Encoding.UTF8.GetBytes(apiKey);
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (_expectedKey is null || !context.Request.Path.StartsWithSegments("/mcp") || IsAuthorized(context.Request))
            return _next(context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    private bool IsAuthorized(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        var provided = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : request.Query["key"].ToString();

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _expectedKey);
    }
}
