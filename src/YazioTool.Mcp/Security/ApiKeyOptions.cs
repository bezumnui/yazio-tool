using System.Security.Cryptography;
using System.Text;

namespace YazioTool.Mcp.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Mcp";

    /// <summary>When empty, the MCP endpoint is not protected and OAuth is disabled.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Public base URL of the server (e.g. <c>https://yazio.example.com</c>), used in OAuth metadata.
    /// When empty, it is taken from the request (<c>X-Forwarded-Proto</c> is honoured).
    /// </summary>
    public string PublicUrl { get; set; } = "";

    public bool Matches(string? provided) =>
        !string.IsNullOrEmpty(ApiKey)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided ?? ""), Encoding.UTF8.GetBytes(ApiKey));
}
