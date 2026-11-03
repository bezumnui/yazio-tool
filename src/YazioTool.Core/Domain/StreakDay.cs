namespace YazioTool.Core.Domain;

/// <summary>One day of the YAZIO streak (the "flame").</summary>
/// <param name="Meals">Meals tracked on that day.</param>
/// <param name="StreakCount">Streak length including this day. YAZIO stores what the client sends and does not compute it.</param>
/// <param name="FreezeCount">Streak freezes in stock after this day (0..<see cref="Application.StreakRules.MaxFreezes"/>).</param>
public sealed record StreakDay(DateOnly Date, IReadOnlyList<Meal> Meals, int? StreakCount, int? FreezeCount);
