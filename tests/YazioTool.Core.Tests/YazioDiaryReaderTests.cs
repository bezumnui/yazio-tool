using NSubstitute;
using YazioTool.Core.Domain;
using YazioTool.Core.Yazio.Api;
using YazioTool.Core.Yazio.Diary;
using YazioTool.Core.Yazio.Products;

namespace YazioTool.Core.Tests;

public sealed class YazioDiaryReaderTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IYazioApiClient _api = Substitute.For<IYazioApiClient>();
    private readonly IProductCatalog _products = Substitute.For<IProductCatalog>();

    [Fact]
    public async Task Maps_entries_and_takes_totals_from_summary()
    {
        var productId = Guid.NewGuid();
        var productEntryId = Guid.NewGuid();
        var quickEntryId = Guid.NewGuid();

        _api.GetConsumedItemsAsync(Day, Arg.Any<CancellationToken>()).Returns(new ConsumedItemsDto(
            [new ConsumedProductDto(productEntryId, "2026-11-01 08:15:00", "breakfast", productId, 150, null, null)],
            [],
            [new SimpleProductDto(quickEntryId, "2026-11-01 13:00:00", "lunch", "Plov",
                new Dictionary<string, double?> { ["energy.energy"] = 650, ["nutrient.protein"] = 25, ["nutrient.fat"] = 28, ["nutrient.carb"] = 70 })]));

        _api.GetDailySummaryAsync(Day, Arg.Any<CancellationToken>()).Returns(new DailySummaryDto(
            new Dictionary<string, MealSummaryDto>
            {
                ["breakfast"] = new(500, Nutrients(80, 6, 0.5, 18)),
                ["lunch"] = new(800, Nutrients(650, 25, 28, 70)),
            },
            new Dictionary<string, double?> { ["energy.energy"] = 2000, ["nutrient.protein"] = 120, ["nutrient.fat"] = 70, ["nutrient.carb"] = 220 }));

        _products.FindAsync(productId, Arg.Any<CancellationToken>())
            .Returns(new ProductInfo(productId, "Skyr", "g", new Nutrition(0.6, 0.11, 0.002, 0.04)));

        var diary = await new YazioDiaryReader(_api, _products).GetDayAsync(Day, Ct);

        Assert.Equal(2, diary.Entries.Count);
        var product = diary.Entries[0];
        Assert.Equal((productEntryId, Meal.Breakfast, DiaryEntryKind.Product, "Skyr", "150 g"),
            (product.Id, product.Meal, product.Kind, product.Name, product.Amount));
        Assert.Equal(90, product.Nutrition!.Value.Kcal, 3);
        Assert.Equal(16.5, product.Nutrition!.Value.Protein, 3);
        Assert.Equal(new DateTime(2026, 11, 1, 8, 15, 0), product.ConsumedAt);

        var quick = diary.Entries[1];
        Assert.Equal((quickEntryId, Meal.Lunch, DiaryEntryKind.QuickEntry), (quick.Id, quick.Meal, quick.Kind));
        Assert.Equal(new Nutrition(650, 25, 28, 70), quick.Nutrition);

        Assert.Equal(new Nutrition(730, 31, 28.5, 88), diary.Total);
        Assert.Equal(new Nutrition(2000, 120, 70, 220), diary.Goal);
        Assert.Equal(4, diary.Meals.Count);
        Assert.Equal(800, diary.Meals.Single(m => m.Meal == Meal.Lunch).EnergyGoal);
    }

    [Fact]
    public async Task Unknown_product_keeps_entry_without_nutrition()
    {
        _api.GetConsumedItemsAsync(Day, Arg.Any<CancellationToken>()).Returns(new ConsumedItemsDto(
            [new ConsumedProductDto(Guid.NewGuid(), "2026-11-01 08:00:00", "snack", Guid.NewGuid(), 2, "piece", 2)], null, null));
        _api.GetDailySummaryAsync(Day, Arg.Any<CancellationToken>()).Returns(new DailySummaryDto(null, null));

        var diary = await new YazioDiaryReader(_api, _products).GetDayAsync(Day, Ct);

        var entry = Assert.Single(diary.Entries);
        Assert.Null(entry.Nutrition);
        Assert.Equal("2 × piece (2 g)", entry.Amount);
        Assert.Null(diary.Goal);
    }

    private static Dictionary<string, double?> Nutrients(double kcal, double protein, double fat, double carbs) => new()
    {
        ["energy.energy"] = kcal,
        ["nutrient.protein"] = protein,
        ["nutrient.fat"] = fat,
        ["nutrient.carb"] = carbs,
    };
}
