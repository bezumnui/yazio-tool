namespace YazioTool.Core.Domain;

/// <summary>A diary entry described only by name and nutrition, without a product from the YAZIO database.</summary>
public sealed record QuickEntry
{
    public QuickEntry(string name, Nutrition nutrition, Meal meal, DateTime consumedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!nutrition.IsNonNegative)
            throw new ArgumentOutOfRangeException(nameof(nutrition), "Nutrition values must not be negative.");

        Name = name.Trim();
        Nutrition = nutrition;
        Meal = meal;
        ConsumedAt = consumedAt;
    }

    public string Name { get; }
    public Nutrition Nutrition { get; }
    public Meal Meal { get; }
    public DateTime ConsumedAt { get; }
}
