using YazioTool.Core.DependencyInjection;
using YazioTool.Core.Yazio;
using YazioTool.Mcp.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddYazio(builder.Configuration.GetSection(YazioOptions.SectionName));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseMiddleware<ApiKeyMiddleware>();
app.MapGet("/health", () => Results.Ok("ok"));
app.MapMcp("/mcp");

app.Run();
