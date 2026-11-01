using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YazioTool.Core.DependencyInjection;
using YazioTool.Core.Yazio;

namespace YazioTool.Cli;

/// <summary>Composition root of the CLI.</summary>
internal static class CliServices
{
    /// <summary>
    /// Sources, later wins: appsettings.json next to the binary, user secrets,
    /// <c>Yazio__*</c> variables, short <c>YAZIO_USERNAME</c>/<c>YAZIO_PASSWORD</c> variables.
    /// </summary>
    public static ServiceProvider Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddUserSecrets(typeof(CliServices).Assembly, optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(ShortEnvironmentVariables())
            .Build();

        var services = new ServiceCollection();
        services.AddYazio(configuration.GetSection(YazioOptions.SectionName));
        services.AddYazioFileTokenStore(TokenCachePath());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static IEnumerable<KeyValuePair<string, string?>> ShortEnvironmentVariables()
    {
        (string Variable, string Key)[] map =
        [
            ("YAZIO_USERNAME", "Yazio:Username"),
            ("YAZIO_PASSWORD", "Yazio:Password"),
        ];

        foreach (var (variable, key) in map)
        {
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value)
                yield return new(key, value);
        }
    }

    private static string TokenCachePath() =>
        Environment.GetEnvironmentVariable("YAZIO_TOKEN_CACHE") is { Length: > 0 } path
            ? path
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                "yazio-tool",
                "token.json");
}
