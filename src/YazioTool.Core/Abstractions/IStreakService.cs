using YazioTool.Core.Domain;

namespace YazioTool.Core.Abstractions;

/// <summary>
/// The YAZIO streak. The official app computes each day on the device and uploads it;
/// the server only stores it, so writing here is what makes the flame appear.
/// The app caches the streak locally and shows server-side changes only after a re-login.
/// </summary>
public interface IStreakService
{
    /// <returns>All tracked days, oldest first.</returns>
    Task<IReadOnlyList<StreakDay>> GetDaysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks <paramref name="date"/> as tracked with the meals present in the diary, like the app does after logging food.
    /// A day without a count continues yesterday's streak (or starts at 1); an existing count is kept.
    /// </summary>
    Task<StreakDay> TrackDayAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Overwrites the streak count and/or freeze count of a day; null keeps the current (or computed) value.</summary>
    Task<StreakDay> SetDayAsync(DateOnly date, int? streakCount, int? freezeCount, CancellationToken cancellationToken = default);
}
