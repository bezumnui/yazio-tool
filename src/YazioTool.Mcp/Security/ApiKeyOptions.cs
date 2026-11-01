namespace YazioTool.Mcp.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Mcp";

    /// <summary>When empty, the MCP endpoint is not protected.</summary>
    public string ApiKey { get; set; } = "";
}
