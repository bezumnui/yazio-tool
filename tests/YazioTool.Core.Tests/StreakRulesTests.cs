using YazioTool.Core.Application;
using YazioTool.Core.Domain;

namespace YazioTool.Core.Tests;

public sealed class StreakRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Fact]
    public void New_day_continues_previous_streak_and_keeps_freezes()
    {
        var previous = new StreakDay(Today.AddDays(-1), [Meal.Breakfast], 75, 1);

        var day = StreakRules.Track(Today, [Meal.Snack, Meal.Breakfast, Meal.Snack], null, previous);

        Assert.Equal(new[] { Meal.Breakfast, Meal.Snack }, day.Meals);
        Assert.Equal(76, day.StreakCount);
        Assert.Equal(1, day.FreezeCount);
    }

    [Fact]
    public void New_day_after_a_gap_starts_at_one()
    {
        var day = StreakRules.Track(Today, [Meal.Lunch], null, null);

        Assert.Equal(1, day.StreakCount);
        Assert.Equal(0, day.FreezeCount);
    }

    [Fact]
    public void Existing_day_keeps_its_counts_and_merges_meals()
    {
        var existing = new StreakDay(Today, [Meal.Dinner], 1488, 2);

        var day = StreakRules.Track(Today, [Meal.Breakfast], existing, new StreakDay(Today.AddDays(-1), [], 10, 0));

        Assert.Equal(new[] { Meal.Breakfast, Meal.Dinner }, day.Meals);
        Assert.Equal(1488, day.StreakCount);
        Assert.Equal(2, day.FreezeCount);
    }

    [Fact]
    public void Override_replaces_only_given_values()
    {
        var day = StreakRules.Override(new StreakDay(Today, [Meal.Lunch], 5, 1), null, 2);

        Assert.Equal(5, day.StreakCount);
        Assert.Equal(2, day.FreezeCount);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    [InlineData(null, 3)]
    public void Invalid_values_are_rejected(int? streakCount, int? freezeCount) =>
        Assert.Throws<ArgumentException>(() => StreakRules.Validate(streakCount, freezeCount));
}
