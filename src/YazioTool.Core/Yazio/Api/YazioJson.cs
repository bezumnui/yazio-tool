using System.Text.Json;

namespace YazioTool.Core.Yazio.Api;

internal static class YazioJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
