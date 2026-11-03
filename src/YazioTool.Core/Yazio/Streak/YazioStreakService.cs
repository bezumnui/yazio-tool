using System.Globalization;
using System.Text.Json;
using YazioTool.Core.Abstractions;
using YazioTool.Core.Application;
using YazioTool.Core.Domain;
using YazioTool.Core.Yazio.Api;

namespace YazioTool.Core.Yazio.Streak;

internal sealed class YazioStreakService(IYazioApiClient api) : IStreakService
{
    public async Task<IReadOnlyList<StreakDay>> GetDaysAsync(CancellationToken cancellationToken = default)
    {
        var days = await api.GetStreakAsync(cancellationToken);
        return days
            .Select(pair => TryParseDate(pair.Key, out var date) ? ToDomain(date, pair.Value) : null)
            .OfType<StreakDay>()
            .OrderBy(day => day.Date)
            .ToList();
    }

    public async Task<StreakDay> TrackDayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var (existing, previous, origin) = await LoadAsync(date, cancellationToken);
        var day = StreakRules.Track(date, await GetDiaryMealsAsync(date, cancellationToken), existing, previous);
        await SaveAsync(day, origin, cancellationToken);
        return day;
    }

    public async Task<StreakDay> SetDayAsync(DateOnly date, int? streakCount, int? freezeCount, CancellationToken cancellationToken = default)
    {
        StreakRules.Validate(streakCount, freezeCount);

        var (existing, previous, origin) = await LoadAsync(date, cancellationToken);
        var current = existing ?? StreakRules.Track(date, await GetDiaryMealsAsync(date, cancellationToken), null, previous);
        var day = StreakRules.Override(current, streakCount, freezeCount);
        await SaveAsync(day, origin, cancellationToken);
        return day;
    }

    private async Task<(StreakDay? Existing, StreakDay? Previous, JsonElement? Origin)> LoadAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var days = await api.GetStreakAsync(cancellationToken);
        days.TryGetValue(YazioFormats.FormatDate(date), out var existing);
        days.TryGetValue(YazioFormats.FormatDate(date.AddDays(-1)), out var previous);
        return (
            existing is null ? null : ToDomain(date, existing),
            previous is null ? null : ToDomain(date.AddDays(-1), previous),
            existing?.OriginOfRecovery);
    }

    /// <summary>Meals with at least one entry, read from the raw diary (no product lookups needed).</summary>
    private async Task<IReadOnlyList<Meal>> GetDiaryMealsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var items = await api.GetConsumedItemsAsync(date, cancellationToken);
        return (items.Products ?? []).Select(p => p.Daytime)
            .Concat((items.RecipePortions ?? []).Select(r => r.Daytime))
            .Concat((items.SimpleProducts ?? []).Select(s => s.Daytime))
            .Select(YazioFormats.ParseDaytime)
            .Distinct()
            .ToList();
    }

    private Task SaveAsync(StreakDay day, JsonElement? origin, CancellationToken cancellationToken) =>
        api.SetStreakDayAsync(
            day.Date,
            new StreakDayDto(day.Meals.Select(YazioFormats.FormatDaytime).ToList(), day.StreakCount, day.FreezeCount, origin),
            cancellationToken);

    private static StreakDay ToDomain(DateOnly date, StreakDayDto dto) => new(
        date,
        (dto.Daytimes ?? []).Select(YazioFormats.ParseDaytime).Distinct().Order().ToList(),
        dto.StreakCount,
        dto.FreezeCount);

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
