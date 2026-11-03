using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace YazioTool.Mcp.Tests;

public sealed class OAuthFlowTests : IClassFixture<OAuthFlowTests.Factory>
{
    private const string ApiKey = "test-key";
    private const string RedirectUri = "https://claude.ai/api/mcp/auth_callback";
    private const string Verifier = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJ";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string Challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    private readonly HttpClient _http;

    public OAuthFlowTests(Factory factory)
    {
        _http = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.UseSetting("Mcp:ApiKey", ApiKey).UseSetting("Mcp:PublicUrl", "https://yazio.example.com");
    }

    [Fact]
    public async Task Unauthorized_mcp_request_points_to_resource_metadata()
    {
        var response = await _http.PostAsync("/mcp", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer resource_metadata=\"https://yazio.example.com/.well-known/oauth-protected-resource/mcp\"",
            response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Publishes_metadata()
    {
        var resource = await _http.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource/mcp", Ct);
        var server = await _http.GetFromJsonAsync<JsonElement>("/.well-known/oauth-authorization-server", Ct);

        Assert.Equal("https://yazio.example.com/mcp", resource.GetProperty("resource").GetString());
        Assert.Equal("https://yazio.example.com", resource.GetProperty("authorization_servers")[0].GetString());
        Assert.Equal("https://yazio.example.com/token", server.GetProperty("token_endpoint").GetString());
        Assert.Equal("https://yazio.example.com/register", server.GetProperty("registration_endpoint").GetString());
    }

    [Fact]
    public async Task Full_flow_grants_access_to_mcp_and_refreshes()
    {
        var clientId = await RegisterAsync();

        var page = await _http.GetAsync(AuthorizeUrl(clientId), Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("name=\"key\"", await page.Content.ReadAsStringAsync(Ct));

        var code = await ApproveAsync(clientId);
        var tokens = await ExchangeAsync(new()
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["client_id"] = clientId,
            ["redirect_uri"] = RedirectUri, ["code_verifier"] = Verifier,
        });
        Assert.Equal(HttpStatusCode.OK, tokens.StatusCode);
        var body = await tokens.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, (await InitializeAsync(body.GetProperty("access_token").GetString()!)).StatusCode);

        var refreshed = await ExchangeAsync(new()
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = body.GetProperty("refresh_token").GetString()!, ["client_id"] = clientId,
        });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var refreshedBody = await refreshed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(HttpStatusCode.OK, (await InitializeAsync(refreshedBody.GetProperty("access_token").GetString()!)).StatusCode);
    }

    [Fact]
    public async Task Code_cannot_be_reused()
    {
        var clientId = await RegisterAsync();
        var code = await ApproveAsync(clientId);
        var request = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["client_id"] = clientId, ["code_verifier"] = Verifier,
        };

        Assert.Equal(HttpStatusCode.OK, (await ExchangeAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ExchangeAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Wrong_pkce_verifier_is_rejected()
    {
        var clientId = await RegisterAsync();
        var code = await ApproveAsync(clientId);

        var response = await ExchangeAsync(new()
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["client_id"] = clientId,
            ["code_verifier"] = new string('x', 43),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_key_does_not_issue_a_code()
    {
        var clientId = await RegisterAsync();

        var response = await PostAuthorizeAsync(clientId, "wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Wrong key", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Unregistered_redirect_uri_is_not_redirected_to()
    {
        var clientId = await RegisterAsync();

        var response = await _http.GetAsync(AuthorizeUrl(clientId, "https://evil.example.com/callback"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Registration_rejects_plain_http_redirects()
    {
        var response = await _http.PostAsJsonAsync("/register", new { redirect_uris = new[] { "http://example.com/cb" } }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Static_key_still_works()
    {
        Assert.Equal(HttpStatusCode.OK, (await InitializeAsync(ApiKey)).StatusCode);
    }

    private async Task<string> RegisterAsync()
    {
        var response = await _http.PostAsJsonAsync("/register", new { client_name = "Claude", redirect_uris = new[] { RedirectUri } }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("client_id").GetString()!;
    }

    private async Task<string> ApproveAsync(string clientId)
    {
        var response = await PostAuthorizeAsync(clientId, ApiKey);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!;
        Assert.StartsWith(RedirectUri + "?", location.ToString());
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("xyz", query["state"].ToString());
        Assert.Equal("https://yazio.example.com", query["iss"].ToString());
        return query["code"].ToString();
    }

    private Task<HttpResponseMessage> PostAuthorizeAsync(string clientId, string key) =>
        _http.PostAsync("/authorize", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = RedirectUri,
            ["code_challenge"] = Challenge, ["code_challenge_method"] = "S256", ["state"] = "xyz",
            ["key"] = key, ["action"] = "allow",
        }), Ct);

    private static string AuthorizeUrl(string clientId, string redirectUri = RedirectUri) =>
        QueryHelpers.AddQueryString("/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = redirectUri,
            ["code_challenge"] = Challenge, ["code_challenge_method"] = "S256", ["state"] = "xyz",
        });

    private Task<HttpResponseMessage> ExchangeAsync(Dictionary<string, string> form) =>
        _http.PostAsync("/token", new FormUrlEncodedContent(form), Ct);

    private Task<HttpResponseMessage> InitializeAsync(string bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""",
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return _http.SendAsync(request, Ct);
    }
}
