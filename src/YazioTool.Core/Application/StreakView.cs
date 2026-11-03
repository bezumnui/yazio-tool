using System.Globalization;
using YazioTool.Core.Domain;

namespace YazioTool.Core.Application;

/// <summary>Streak overview for front ends.</summary>
/// <param name="CurrentStreak">Count of the last tracked day if it is today or yesterday, otherwise 0 (the streak is broken).</param>
public sealed record StreakView(int CurrentStreak, int? Freezes, string? LastTrackedDay, bool TrackedToday, IReadOnlyList<StreakDayView> Days)
{
    public static StreakView From(IReadOnlyList<StreakDay> days, DateOnly today, int recentDays)
    {
        var last = days.Count > 0 ? days[^1] : null;
        var alive = last is not null && last.Date >= today.AddDays(-1);
        return new(
            alive ? last!.StreakCount ?? 0 : 0,
            last?.FreezeCount,
            last is null ? null : StreakDayView.FormatDate(last.Date),
            last?.Date == today,
            days.TakeLast(Math.Max(recentDays, 0)).Select(StreakDayView.From).ToList());
    }
}

public sealed record StreakDayView(string Date, IReadOnlyList<string> Meals, int? StreakCount, int? FreezeCount)
{
    public static StreakDayView From(StreakDay day) => new(
        FormatDate(day.Date),
        day.Meals.Select(m => m.ToString().ToLowerInvariant()).ToList(),
        day.StreakCount,
        day.FreezeCount);

    internal static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
