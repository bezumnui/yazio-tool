namespace YazioTool.Core.Domain;

public sealed record MealSummary(Meal Meal, Nutrition Consumed, double? EnergyGoal);

public sealed record DailyDiary(
    DateOnly Date,
    IReadOnlyList<DiaryEntry> Entries,
    IReadOnlyList<MealSummary> Meals,
    Nutrition Total,
    Nutrition? Goal);
