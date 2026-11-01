using YazioTool.Core.Abstractions;
using YazioTool.Core.Domain;
using YazioTool.Core.Yazio.Api;
using YazioTool.Core.Yazio.Products;

namespace YazioTool.Core.Yazio.Diary;

internal sealed class YazioDiaryReader(IYazioApiClient api, IProductCatalog products) : IDiaryReader
{
    public async Task<DailyDiary> GetDayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var itemsTask = api.GetConsumedItemsAsync(date, cancellationToken);
        var summaryTask = api.GetDailySummaryAsync(date, cancellationToken);
        var items = await itemsTask;
        var summary = await summaryTask;

        var productEntries = await Task.WhenAll(
            (items.Products ?? []).Select(p => MapProductAsync(p, cancellationToken)));

        var entries = productEntries
            .Concat((items.SimpleProducts ?? []).Select(MapSimpleProduct))
            .Concat((items.RecipePortions ?? []).Select(MapRecipePortion))
            .OrderBy(e => e.Meal)
            .ThenBy(e => e.ConsumedAt)
            .ToList();

        var meals = Enum.GetValues<Meal>().Select(meal => MapMeal(meal, summary)).ToList();
        var total = summary.Meals is null
            ? entries.Aggregate(Nutrition.Zero, (sum, e) => sum + (e.Nutrition ?? Nutrition.Zero))
            : meals.Aggregate(Nutrition.Zero, (sum, m) => sum + m.Consumed);
        Nutrition? goal = summary.Goals is null ? null : YazioFormats.ToNutrition(summary.Goals);

        return new DailyDiary(date, entries, meals, total, goal);
    }

    private async Task<DiaryEntry> MapProductAsync(ConsumedProductDto item, CancellationToken cancellationToken)
    {
        var product = await products.FindAsync(item.ProductId, cancellationToken);
        var amount = item is { Serving: { } serving, ServingQuantity: { } quantity }
            ? $"{quantity:0.##} × {serving} ({item.Amount:0.#} {product?.BaseUnit ?? "g"})"
            : $"{item.Amount:0.#} {product?.BaseUnit ?? "g"}";

        return new DiaryEntry(
            item.Id,
            YazioFormats.ParseDateTime(item.Date),
            YazioFormats.ParseDaytime(item.Daytime),
            DiaryEntryKind.Product,
            product?.Name ?? $"Unknown product {item.ProductId}",
            amount,
            product?.PerBaseUnit.Scale(item.Amount));
    }

    private static DiaryEntry MapSimpleProduct(SimpleProductDto item) => new(
        item.Id,
        YazioFormats.ParseDateTime(item.Date),
        YazioFormats.ParseDaytime(item.Daytime),
        DiaryEntryKind.QuickEntry,
        item.Name,
        null,
        YazioFormats.ToNutrition(item.Nutrients));

    private static DiaryEntry MapRecipePortion(RecipePortionDto item) => new(
        item.Id,
        YazioFormats.ParseDateTime(item.Date),
        YazioFormats.ParseDaytime(item.Daytime),
        DiaryEntryKind.Recipe,
        $"Recipe {item.RecipeId}",
        $"{item.PortionCount:0.##} portion(s)",
        null);

    private static MealSummary MapMeal(Meal meal, DailySummaryDto summary) =>
        summary.Meals?.TryGetValue(YazioFormats.FormatDaytime(meal), out var dto) == true
            ? new MealSummary(meal, YazioFormats.ToNutrition(dto.Nutrients), dto.EnergyGoal)
            : new MealSummary(meal, Nutrition.Zero, null);
}
