using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace YazioTool.Mcp.Security.OAuth;

/// <summary>
/// Minimal OAuth 2.1 authorization server for MCP clients such as claude.ai connectors and Claude Code:
/// protected resource metadata (RFC 9728), authorization server metadata (RFC 8414), dynamic client
/// registration (RFC 7591), authorization code flow with PKCE S256, and refresh tokens.
/// The user approves access on <c>/authorize</c> by entering <c>Mcp:ApiKey</c>.
/// </summary>
internal static class OAuthEndpoints
{
    private static readonly TimeSpan WrongKeyDelay = TimeSpan.FromSeconds(1);

    public static void MapOAuth(this WebApplication app)
    {
        app.MapGet(OAuthUrls.ProtectedResourceMetadataPath, ProtectedResourceMetadata);
        app.MapGet(OAuthUrls.ProtectedResourceMetadataPath + "/mcp", ProtectedResourceMetadata);
        app.MapGet(OAuthUrls.AuthorizationServerMetadataPath, AuthorizationServerMetadata);
        app.MapPost(OAuthUrls.RegisterPath, Register);
        app.MapMethods(OAuthUrls.AuthorizePath, [HttpMethods.Get, HttpMethods.Post], Authorize);
        app.MapPost(OAuthUrls.TokenPath, Token);
    }

    private static IResult ProtectedResourceMetadata(HttpRequest request, IOptions<ApiKeyOptions> options)
    {
        var baseUrl = OAuthUrls.BaseUrl(request, options.Value);
        return Results.Json(new Dictionary<string, object>
        {
            ["resource"] = baseUrl + "/mcp",
            ["authorization_servers"] = new[] { baseUrl },
            ["bearer_methods_supported"] = new[] { "header" },
            ["resource_name"] = "YAZIO",
        });
    }

    private static IResult AuthorizationServerMetadata(HttpRequest request, IOptions<ApiKeyOptions> options)
    {
        var baseUrl = OAuthUrls.BaseUrl(request, options.Value);
        return Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = baseUrl,
            ["authorization_endpoint"] = baseUrl + OAuthUrls.AuthorizePath,
            ["token_endpoint"] = baseUrl + OAuthUrls.TokenPath,
            ["registration_endpoint"] = baseUrl + OAuthUrls.RegisterPath,
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["token_endpoint_auth_methods_supported"] = new[] { "none" },
        });
    }

    private static IResult Register(ClientRegistrationRequest request, OAuthTokens tokens)
    {
        if (request.RedirectUris is not { Length: > 0 } redirectUris)
            return Error("invalid_redirect_uri", "redirect_uris is required.");
        if (redirectUris.FirstOrDefault(uri => !IsAllowedRedirectUri(uri)) is { } invalid)
            return Error("invalid_redirect_uri", $"Redirect URI must be https or http on a loopback address: {invalid}");

        // Public client: PKCE protects the code, so no client secret is issued regardless of what was asked for.
        return Results.Json(new Dictionary<string, object?>
        {
            ["client_id"] = tokens.CreateClient(redirectUris),
            ["client_id_issued_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["client_name"] = request.ClientName,
            ["redirect_uris"] = redirectUris,
            ["token_endpoint_auth_method"] = "none",
            ["grant_types"] = new[] { "authorization_code", "refresh_token" },
            ["response_types"] = new[] { "code" },
        }, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Authorize(HttpContext context, OAuthTokens tokens, IOptions<ApiKeyOptions> options)
    {
        var request = context.Request;
        var isPost = HttpMethods.IsPost(request.Method);
        var form = isPost && request.HasFormContentType ? await request.ReadFormAsync(context.RequestAborted) : null;
        string? Get(string name) => (form?[name] ?? request.Query[name]).ToString() is { Length: > 0 } value ? value : null;

        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        context.Response.Headers.CacheControl = "no-store";

        // Without a valid client and redirect URI there is nowhere safe to send errors to.
        var client = tokens.Read(Get("client_id"), TokenType.Client);
        if (client is null)
            return AuthorizePage.Error("Unknown client. Remove and add the connector again so that it registers anew.");
        var redirectUri = Get("redirect_uri") ?? (client.RedirectUris is [var only] ? only : null);
        if (redirectUri is null || client.RedirectUris?.Contains(redirectUri) != true)
            return AuthorizePage.Error("redirect_uri is not registered for this client.");

        var state = Get("state");
        var issuer = OAuthUrls.BaseUrl(request, options.Value);
        if (Get("response_type") != "code")
            return RedirectWith(redirectUri, issuer, state, ("error", "unsupported_response_type"));
        var codeChallenge = Get("code_challenge");
        if (codeChallenge is null || Get("code_challenge_method") != "S256")
            return RedirectWith(redirectUri, issuer, state, ("error", "invalid_request"), ("error_description", "PKCE with S256 is required."));

        var fields = new[] { "response_type", "client_id", "redirect_uri", "code_challenge", "code_challenge_method", "state", "scope", "resource" }
            .Select(name => KeyValuePair.Create(name, Get(name)))
            .ToList();
        if (!isPost)
            return AuthorizePage.Form(fields);
        if (Get("action") == "deny")
            return RedirectWith(redirectUri, issuer, state, ("error", "access_denied"));
        if (!options.Value.Matches(Get("key")))
        {
            await Task.Delay(WrongKeyDelay, context.RequestAborted);
            return AuthorizePage.Form(fields, "Wrong key.");
        }

        var code = tokens.CreateCode(client, redirectUri, codeChallenge, Get("scope"));
        return RedirectWith(redirectUri, issuer, state, ("code", code));
    }

    private static async Task<IResult> Token(HttpContext context, OAuthTokens tokens)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasFormContentType)
            return Error("invalid_request", "Expected application/x-www-form-urlencoded.");

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        string? Get(string name) => form[name].ToString() is { Length: > 0 } value ? value : null;
        var clientId = Get("client_id") ?? BasicAuthClientId(context.Request);

        switch (Get("grant_type"))
        {
            case "authorization_code":
                var code = tokens.Read(Get("code"), TokenType.Code);
                var client = tokens.Read(clientId, TokenType.Client);
                if (code is null || client is null || code.ClientId != client.Id
                    || (Get("redirect_uri") is { } redirectUri && redirectUri != code.RedirectUri)
                    || !OAuthTokens.VerifyPkce(code.CodeChallenge, Get("code_verifier"))
                    || !tokens.TryRedeem(code))
                    return Error("invalid_grant", "The authorization code is invalid, expired or already used.");
                return IssueTokens(tokens, client.Id, code.Scope);

            case "refresh_token":
                var refresh = tokens.Read(Get("refresh_token"), TokenType.Refresh);
                if (refresh?.ClientId is null || (clientId is not null && tokens.Read(clientId, TokenType.Client)?.Id != refresh.ClientId))
                    return Error("invalid_grant", "The refresh token is invalid or expired.");
                return IssueTokens(tokens, refresh.ClientId, refresh.Scope);

            default:
                return Error("unsupported_grant_type", "Supported grants: authorization_code, refresh_token.");
        }
    }

    private static IResult IssueTokens(OAuthTokens tokens, string clientId, string? scope) =>
        Results.Json(new Dictionary<string, object?>
        {
            ["access_token"] = tokens.CreateAccessToken(clientId, scope),
            ["token_type"] = "Bearer",
            ["expires_in"] = (int)OAuthTokens.AccessTokenLifetime.TotalSeconds,
            ["refresh_token"] = tokens.CreateRefreshToken(clientId, scope),
            ["scope"] = scope,
        });

    private static IResult RedirectWith(string redirectUri, string issuer, string? state, params (string Name, string Value)[] parameters)
    {
        var query = parameters.Select(p => KeyValuePair.Create(p.Name, (string?)p.Value)).ToList();
        if (state is not null)
            query.Add(KeyValuePair.Create("state", (string?)state));
        query.Add(KeyValuePair.Create("iss", (string?)issuer));
        return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, query));
    }

    private static IResult Error(string error, string description) =>
        Results.Json(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description },
            statusCode: StatusCodes.Status400BadRequest);

    private static bool IsAllowedRedirectUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    private static string? BasicAuthClientId(HttpRequest request)
    {
        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header)
            || !"Basic".Equals(header.Scheme, StringComparison.OrdinalIgnoreCase) || header.Parameter is null)
            return null;

        try
        {
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            return Uri.UnescapeDataString(credentials.Split(':', 2)[0]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    internal sealed record ClientRegistrationRequest(
        [property: JsonPropertyName("redirect_uris")] string[]? RedirectUris,
        [property: JsonPropertyName("client_name")] string? ClientName);
}
