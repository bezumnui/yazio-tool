using System.Globalization;
using YazioTool.Core.Domain;

namespace YazioTool.Core.Yazio.Api;

/// <summary>Conversions between domain types and YAZIO wire formats.</summary>
internal static class YazioFormats
{
    public const string Energy = "energy.energy";
    public const string Protein = "nutrient.protein";
    public const string Fat = "nutrient.fat";
    public const string Carbs = "nutrient.carb";

    public static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTime dateTime) =>
        dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static DateTime ParseDateTime(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result)
            ? result
            : DateTime.MinValue;

    public static string FormatDaytime(Meal meal) => meal switch
    {
        Meal.Breakfast => "breakfast",
        Meal.Lunch => "lunch",
        Meal.Dinner => "dinner",
        Meal.Snack => "snack",
        _ => throw new ArgumentOutOfRangeException(nameof(meal), meal, null),
    };

    public static Meal ParseDaytime(string? daytime) => daytime switch
    {
        "breakfast" => Meal.Breakfast,
        "lunch" => Meal.Lunch,
        "dinner" => Meal.Dinner,
        _ => Meal.Snack,
    };

    public static Nutrition ToNutrition(IReadOnlyDictionary<string, double?>? nutrients) =>
        nutrients is null
            ? Nutrition.Zero
            : new(Get(nutrients, Energy), Get(nutrients, Protein), Get(nutrients, Fat), Get(nutrients, Carbs));

    public static IReadOnlyDictionary<string, double> FromNutrition(Nutrition nutrition) => new Dictionary<string, double>
    {
        [Energy] = nutrition.Kcal,
        [Protein] = nutrition.Protein,
        [Fat] = nutrition.Fat,
        [Carbs] = nutrition.Carbs,
    };

    private static double Get(IReadOnlyDictionary<string, double?> nutrients, string key) =>
        nutrients.TryGetValue(key, out var value) ? value ?? 0 : 0;
}
