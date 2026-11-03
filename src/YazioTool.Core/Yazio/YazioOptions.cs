namespace YazioTool.Core.Yazio;

public sealed class YazioOptions
{
    public const string SectionName = "Yazio";

    public string BaseUrl { get; set; } = "https://yzapi.yazio.com/v22/";

    /// <summary>Sent as X-Yazio-App-Version; the API rejects old versions with 403 version_blocked.</summary>
    public string AppVersion { get; set; } = "26.43.1";

    /// <summary>Sent as X-Yazio-Build-Number (must be present, value is not checked).</summary>
    public string BuildNumber { get; set; } = "2643100";

    /// <summary>Sent as X-Yazio-Platform.</summary>
    public string Platform { get; set; } = "android";

    /// <summary>OAuth client credentials of the official YAZIO app (publicly known).</summary>
    public string ClientId { get; set; } = "1_4hiybetvfksgw40o0sog4s884kwc840wwso8go4k8c04goo4c";

    public string ClientSecret { get; set; } = "6rok2m65xuskgkgogw40wkkk8sw0osg84s8cggsc4woos4s8o";

    /// <summary>
    /// Needed only to log in when no valid or refreshable token is available
    /// (see <see cref="Auth.ITokenStore"/>).
    /// </summary>
    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    internal const string ValidationMessage = "Yazio:BaseUrl must be an absolute URL and Yazio:ClientId / Yazio:ClientSecret must be set.";

    internal static bool IsValid(YazioOptions options) =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(options.ClientId)
        && !string.IsNullOrWhiteSpace(options.ClientSecret);
}
