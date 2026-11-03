using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using YazioTool.Core.Abstractions;
using YazioTool.Core.Yazio;
using YazioTool.Core.Yazio.Api;
using YazioTool.Core.Yazio.Auth;
using YazioTool.Core.Yazio.Diary;
using YazioTool.Core.Yazio.Products;
using YazioTool.Core.Yazio.Streak;

namespace YazioTool.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IDiaryReader"/>, <see cref="IDiaryWriter"/> and <see cref="IStreakService"/> backed by the YAZIO API.</summary>
    /// <param name="configuration">Section bound to <see cref="YazioOptions"/>.</param>
    public static IServiceCollection AddYazio(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<YazioOptions>()
            .Bind(configuration)
            .Validate(YazioOptions.IsValid, YazioOptions.ValidationMessage)
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(YazioTokenClient.HttpClientName, ConfigureHttpClient);
        services.AddTransient<BearerTokenHandler>();
        services.AddHttpClient(YazioApiClient.HttpClientName, ConfigureHttpClient)
            .AddHttpMessageHandler<BearerTokenHandler>();

        services.TryAddSingleton<ITokenStore, NullTokenStore>();
        services.AddSingleton<IYazioTokenClient, YazioTokenClient>();
        services.AddSingleton<IAccessTokenProvider, YazioAccessTokenProvider>();
        services.AddSingleton<IYazioApiClient, YazioApiClient>();

        services.AddSingleton<YazioProductCatalog>();
        services.AddSingleton<IProductCatalog>(sp =>
            new CachingProductCatalog(sp.GetRequiredService<YazioProductCatalog>()));

        services.AddSingleton<IDiaryReader, YazioDiaryReader>();
        services.AddSingleton<IDiaryWriter, YazioDiaryWriter>();
        services.AddSingleton<IStreakService, YazioStreakService>();

        return services;
    }

    /// <summary>Persists tokens to <paramref name="path"/> so short-lived processes do not log in on every run.</summary>
    public static IServiceCollection AddYazioFileTokenStore(this IServiceCollection services, string path)
    {
        services.RemoveAll<ITokenStore>();
        services.AddSingleton<ITokenStore>(new FileTokenStore(path));
        return services;
    }

    private static void ConfigureHttpClient(IServiceProvider serviceProvider, HttpClient http)
    {
        var options = serviceProvider.GetRequiredService<IOptions<YazioOptions>>().Value;
        http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.Add("X-Yazio-Platform", options.Platform);
        http.DefaultRequestHeaders.Add("X-Yazio-App-Version", options.AppVersion);
        http.DefaultRequestHeaders.Add("X-Yazio-Build-Number", options.BuildNumber);
    }
}
