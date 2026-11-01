using System.Globalization;
using YazioTool.Core.Domain;

namespace YazioTool.Core.Application;

/// <summary>Parses loosely formatted user/model input shared by all front ends (MCP, CLI).</summary>
public static class DiaryInput
{
    private static readonly Dictionary<string, Meal> MealAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["breakfast"] = Meal.Breakfast, ["завтрак"] = Meal.Breakfast,
        ["lunch"] = Meal.Lunch, ["обед"] = Meal.Lunch,
        ["dinner"] = Meal.Dinner, ["ужин"] = Meal.Dinner,
        ["snack"] = Meal.Snack, ["перекус"] = Meal.Snack,
    };

    public static DateOnly ParseDate(string? value, DateTime now) =>
        string.IsNullOrWhiteSpace(value)
            ? DateOnly.FromDateTime(now)
            : DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : throw new ArgumentException($"Invalid date '{value}', expected YYYY-MM-DD.");

    public static Meal ParseMeal(string value) =>
        MealAliases.TryGetValue(value.Trim(), out var meal)
            ? meal
            : throw new ArgumentException($"Invalid meal '{value}', expected breakfast, lunch, dinner or snack.");

    /// <summary>
    /// Explicit time wins; otherwise "now" for today and a typical time of the meal for other days.
    /// </summary>
    public static DateTime ResolveConsumedAt(DateOnly date, string? time, Meal meal, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(time))
        {
            return TimeOnly.TryParseExact(time.Trim(), ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? date.ToDateTime(parsed)
                : throw new ArgumentException($"Invalid time '{time}', expected HH:mm.");
        }

        if (date == DateOnly.FromDateTime(now))
            return date.ToDateTime(TimeOnly.FromDateTime(now));

        return date.ToDateTime(meal switch
        {
            Meal.Breakfast => new TimeOnly(8, 0),
            Meal.Lunch => new TimeOnly(13, 0),
            Meal.Dinner => new TimeOnly(19, 0),
            _ => new TimeOnly(16, 0),
        });
    }

    /// <summary>Builds a validated quick entry from raw arguments.</summary>
    public static QuickEntry CreateQuickEntry(
        string name, double kcal, double protein, double fat, double carbs,
        string meal, string? date, string? time, DateTime now)
    {
        var parsedMeal = ParseMeal(meal);
        var day = ParseDate(date, now);
        return new QuickEntry(
            name,
            new Nutrition(kcal, protein, fat, carbs),
            parsedMeal,
            ResolveConsumedAt(day, time, parsedMeal, now));
    }

    public static IReadOnlyCollection<Guid> ParseIds(IEnumerable<string> values) =>
        values.Select(v => Guid.TryParse(v, out var id) ? id : throw new ArgumentException($"Invalid entry id '{v}'."))
            .Distinct()
            .ToList();
}
