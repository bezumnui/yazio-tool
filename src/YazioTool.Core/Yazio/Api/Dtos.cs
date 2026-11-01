using System.Text.Json.Serialization;

namespace YazioTool.Core.Yazio.Api;

// Wire models of the private YAZIO API. Field names follow the JSON exactly.

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

internal sealed record ConsumedItemsDto(
    [property: JsonPropertyName("products")] IReadOnlyList<ConsumedProductDto>? Products,
    [property: JsonPropertyName("recipe_portions")] IReadOnlyList<RecipePortionDto>? RecipePortions,
    [property: JsonPropertyName("simple_products")] IReadOnlyList<SimpleProductDto>? SimpleProducts);

internal sealed record ConsumedProductDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("daytime")] string Daytime,
    [property: JsonPropertyName("product_id")] Guid ProductId,
    [property: JsonPropertyName("amount")] double Amount,
    [property: JsonPropertyName("serving")] string? Serving,
    [property: JsonPropertyName("serving_quantity")] double? ServingQuantity);

internal sealed record RecipePortionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("daytime")] string Daytime,
    [property: JsonPropertyName("recipe_id")] Guid RecipeId,
    [property: JsonPropertyName("portion_count")] double PortionCount);

internal sealed record SimpleProductDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("daytime")] string Daytime,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("nutrients")] IReadOnlyDictionary<string, double?>? Nutrients);

internal sealed record DailySummaryDto(
    [property: JsonPropertyName("meals")] IReadOnlyDictionary<string, MealSummaryDto>? Meals,
    [property: JsonPropertyName("goals")] IReadOnlyDictionary<string, double?>? Goals);

internal sealed record MealSummaryDto(
    [property: JsonPropertyName("energy_goal")] double? EnergyGoal,
    [property: JsonPropertyName("nutrients")] IReadOnlyDictionary<string, double?>? Nutrients);

/// <remarks>Nutrients are given per one base unit (g or ml).</remarks>
internal sealed record ProductDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("producer")] string? Producer,
    [property: JsonPropertyName("base_unit")] string? BaseUnit,
    [property: JsonPropertyName("nutrients")] IReadOnlyDictionary<string, double?>? Nutrients);

internal sealed record AddConsumedItemsRequest(
    [property: JsonPropertyName("products")] IReadOnlyList<object> Products,
    [property: JsonPropertyName("recipe_portions")] IReadOnlyList<object> RecipePortions,
    [property: JsonPropertyName("simple_products")] IReadOnlyList<NewSimpleProductDto> SimpleProducts);

internal sealed record NewSimpleProductDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("daytime")] string Daytime,
    [property: JsonPropertyName("nutrients")] IReadOnlyDictionary<string, double> Nutrients,
    [property: JsonPropertyName("is_ai_generated")] bool IsAiGenerated);
