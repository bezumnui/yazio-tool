using YazioTool.Core.Application;
using YazioTool.Core.Domain;

namespace YazioTool.Core.Tests;

public sealed class DiaryInputTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 14, 42, 10);

    [Theory]
    [InlineData("lunch", Meal.Lunch)]
    [InlineData(" Breakfast ", Meal.Breakfast)]
    [InlineData("ужин", Meal.Dinner)]
    [InlineData("Перекус", Meal.Snack)]
    public void Parses_meal_aliases(string input, Meal expected) =>
        Assert.Equal(expected, DiaryInput.ParseMeal(input));

    [Fact]
    public void Rejects_unknown_meal() =>
        Assert.Throws<ArgumentException>(() => DiaryInput.ParseMeal("brunch"));

    [Fact]
    public void Defaults_to_now_for_today() =>
        Assert.Equal(new DateTime(2026, 11, 1, 14, 42, 10),
            DiaryInput.ResolveConsumedAt(DateOnly.FromDateTime(Now), null, Meal.Lunch, Now));

    [Fact]
    public void Defaults_to_meal_time_for_other_days() =>
        Assert.Equal(new DateTime(2026, 10, 30, 19, 0, 0),
            DiaryInput.ResolveConsumedAt(new DateOnly(2026, 10, 30), null, Meal.Dinner, Now));

    [Fact]
    public void Explicit_time_wins() =>
        Assert.Equal(new DateTime(2026, 11, 1, 9, 5, 0),
            DiaryInput.ResolveConsumedAt(new DateOnly(2026, 11, 1), "9:05", Meal.Lunch, Now));

    [Theory]
    [InlineData("01.11.2026")]
    [InlineData("tomorrow")]
    public void Rejects_bad_dates(string input) =>
        Assert.Throws<ArgumentException>(() => DiaryInput.ParseDate(input, Now));

    [Fact]
    public void Quick_entry_rejects_negative_values() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DiaryInput.CreateQuickEntry("x", -1, 0, 0, 0, "lunch", null, null, Now));

    [Fact]
    public void Quick_entry_rejects_empty_name() =>
        Assert.Throws<ArgumentException>(() =>
            DiaryInput.CreateQuickEntry(" ", 100, 0, 0, 0, "lunch", null, null, Now));
}
