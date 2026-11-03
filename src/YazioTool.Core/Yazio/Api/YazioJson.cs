using System.Text.Json;
using System.Text.Json.Serialization;
using YazioTool.Core.Yazio.Auth;

namespace YazioTool.Core.Yazio.Api;

/// <summary>Source-generated JSON metadata, so the library stays trimming-safe.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(ConsumedItemsDto))]
[JsonSerializable(typeof(DailySummaryDto))]
[JsonSerializable(typeof(ProductDto))]
[JsonSerializable(typeof(AddConsumedItemsRequest))]
[JsonSerializable(typeof(IReadOnlyCollection<Guid>))]
[JsonSerializable(typeof(StoredToken))]
[JsonSerializable(typeof(Dictionary<string, StreakDayDto>))]
[JsonSerializable(typeof(StreakDayDto))]
internal sealed partial class YazioJsonContext : JsonSerializerContext;
