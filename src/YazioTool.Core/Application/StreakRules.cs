using YazioTool.Core.Domain;

namespace YazioTool.Core.Application;

/// <summary>How a streak day is derived, mirroring what the YAZIO app uploads.</summary>
public static class StreakRules
{
    /// <summary>The API rejects larger freeze counts (HTTP 500); the app has two freeze slots.</summary>
    public const int MaxFreezes = 2;

    public static StreakDay Track(DateOnly date, IEnumerable<Meal> diaryMeals, StreakDay? existing, StreakDay? previousDay)
    {
        var meals = diaryMeals.Concat(existing?.Meals ?? []).Distinct().Order().ToList();

        if (existing?.StreakCount is not null)
            return existing with { Meals = meals };

        return new StreakDay(
            date,
            meals,
            (previousDay?.StreakCount ?? 0) + 1,
            previousDay?.FreezeCount ?? existing?.FreezeCount ?? 0);
    }

    public static StreakDay Override(StreakDay day, int? streakCount, int? freezeCount)
    {
        Validate(streakCount, freezeCount);
        return day with
        {
            StreakCount = streakCount ?? day.StreakCount,
            FreezeCount = freezeCount ?? day.FreezeCount,
        };
    }

    public static void Validate(int? streakCount, int? freezeCount)
    {
        if (streakCount < 0)
            throw new ArgumentException("Streak count must not be negative.");
        if (freezeCount is < 0 or > MaxFreezes)
            throw new ArgumentException($"Freeze count must be between 0 and {MaxFreezes}.");
    }
}
