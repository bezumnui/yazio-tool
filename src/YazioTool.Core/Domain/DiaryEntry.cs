namespace YazioTool.Core.Domain;

public enum DiaryEntryKind
{
    Product,
    QuickEntry,
    Recipe,
}

/// <param name="Nutrition">Null when YAZIO does not expose nutrients for the entry (e.g. recipe portions).</param>
public sealed record DiaryEntry(
    Guid Id,
    DateTime ConsumedAt,
    Meal Meal,
    DiaryEntryKind Kind,
    string Name,
    string? Amount,
    Nutrition? Nutrition);
