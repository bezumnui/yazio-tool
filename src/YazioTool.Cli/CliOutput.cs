using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using YazioTool.Core.Application;

namespace YazioTool.Cli;

internal sealed record AddResult(Guid Id, string Name, string Meal, string ConsumedAt, NutritionView Nutrition);

internal sealed record RemoveResult(IReadOnlyCollection<Guid> Removed);

internal sealed record ErrorResult(string Error);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(DiaryView))]
[JsonSerializable(typeof(AddResult))]
[JsonSerializable(typeof(RemoveResult))]
[JsonSerializable(typeof(ErrorResult))]
internal sealed partial class CliJsonContext : JsonSerializerContext
{
    /// <summary>Keeps non-ASCII text (e.g. Cyrillic food names) readable in the output.</summary>
    public static CliJsonContext Readable { get; } = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });
}
