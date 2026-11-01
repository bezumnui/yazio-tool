using System.Text.Json;
using YazioTool.Core.Yazio.Api;

namespace YazioTool.Core.Yazio.Auth;

/// <summary>For short-lived processes (CLI): keeps tokens in a JSON file readable only by the owner.</summary>
internal sealed class FileTokenStore(string path) : ITokenStore
{
    public async Task<StoredToken?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, YazioJsonContext.Default.StoredToken, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public async Task SaveAsync(StoredToken token, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        await using var stream = new FileStream(path, options);
        await JsonSerializer.SerializeAsync(stream, token, YazioJsonContext.Default.StoredToken, cancellationToken);
    }
}
