using Microsoft.AspNetCore.HttpOverrides;
using YazioTool.Core.DependencyInjection;
using YazioTool.Core.Yazio;
using YazioTool.Mcp.Security;
using YazioTool.Mcp.Security.OAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddYazio(builder.Configuration.GetSection(YazioOptions.SectionName));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
builder.Services.AddSingleton<OAuthTokens>();

// Behind a TLS-terminating proxy (cloudflared, nginx) OAuth metadata must advertise https URLs.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapGet("/health", () => Results.Ok("ok"));
if (app.Services.GetRequiredService<OAuthTokens>().Enabled)
    app.MapOAuth();
app.MapMcp("/mcp");

app.Run();

public partial class Program;
