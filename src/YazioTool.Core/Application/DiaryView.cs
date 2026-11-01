using System.Globalization;
using YazioTool.Core.Domain;

namespace YazioTool.Core.Application;

/// <summary>Compact, rounded representation of a diary day for front ends.</summary>
public sealed record DiaryView(
    string Date,
    NutritionView Total,
    NutritionView? Goal,
    NutritionView? Remaining,
    IReadOnlyList<MealView> Meals)
{
    public static DiaryView From(DailyDiary diary) => new(
        diary.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        NutritionView.From(diary.Total),
        diary.Goal is { } goal ? NutritionView.From(goal) : null,
        diary.Goal is { } g ? NutritionView.From(g - diary.Total) : null,
        diary.Meals.Select(m => new MealView(
            m.Meal.ToString().ToLowerInvariant(),
            NutritionView.From(m.Consumed),
            m.EnergyGoal is { } kcal ? Math.Round(kcal) : null,
            diary.Entries.Where(e => e.Meal == m.Meal).Select(EntryView.From).ToList())).ToList());
}

public sealed record MealView(string Meal, NutritionView Consumed, double? KcalGoal, IReadOnlyList<EntryView> Entries);

public sealed record EntryView(string Id, string Time, string Kind, string Name, string? Amount, NutritionView? Nutrition)
{
    public static EntryView From(DiaryEntry entry) => new(
        entry.Id.ToString(),
        entry.ConsumedAt.ToString("HH:mm", CultureInfo.InvariantCulture),
        entry.Kind.ToString(),
        entry.Name,
        entry.Amount,
        entry.Nutrition is { } n ? NutritionView.From(n) : null);
}

public sealed record NutritionView(double Kcal, double Protein, double Fat, double Carbs)
{
    public static NutritionView From(Nutrition n) =>
        new(Math.Round(n.Kcal), Math.Round(n.Protein, 1), Math.Round(n.Fat, 1), Math.Round(n.Carbs, 1));
}
