using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using YazioTool.Core.Abstractions;
using YazioTool.Core.Application;
using YazioTool.Core.Yazio;

namespace YazioTool.Mcp.Tools;

[McpServerToolType]
public sealed class DiaryTools(IDiaryReader reader, IDiaryWriter writer, TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetLocalNow().DateTime;

    [McpServerTool(Name = "get_diary", ReadOnly = true, Idempotent = true)]
    [Description("Returns the YAZIO food diary for a day: entries grouped by meal (with ids for removal), totals, goals and what is remaining. Energy in kcal, macros in grams.")]
    public Task<DiaryView> GetDiary(
        [Description("Day in YYYY-MM-DD format. Defaults to today.")] string? date = null,
        CancellationToken cancellationToken = default) =>
        Guard(async () => DiaryView.From(await reader.GetDayAsync(DiaryInput.ParseDate(date, Now), cancellationToken)));

    [McpServerTool(Name = "add_quick_entry", Destructive = false)]
    [Description("Adds a quick entry to the YAZIO diary: a name with calories and macros, without a product from the database. Values are totals for the eaten portion.")]
    public Task<string> AddQuickEntry(
        [Description("What was eaten, e.g. \"Chicken with rice, 350 g\".")] string name,
        [Description("Energy of the whole portion, kcal.")] double kcal,
        [Description("Protein, g.")] double protein,
        [Description("Fat, g.")] double fat,
        [Description("Carbohydrates, g.")] double carbs,
        [Description("Meal: breakfast, lunch, dinner or snack.")] string meal,
        [Description("Day in YYYY-MM-DD format. Defaults to today.")] string? date = null,
        [Description("Time in HH:mm. Defaults to now for today, otherwise a typical time for the meal.")] string? time = null,
        CancellationToken cancellationToken = default) =>
        Guard(async () =>
        {
            var entry = DiaryInput.CreateQuickEntry(name, kcal, protein, fat, carbs, meal, date, time, Now);
            var id = await writer.AddQuickEntryAsync(entry, cancellationToken);
            return $"Added \"{entry.Name}\" ({kcal:0} kcal) to {entry.Meal.ToString().ToLowerInvariant()} on {entry.ConsumedAt:yyyy-MM-dd HH:mm}. Entry id: {id}";
        });

    [McpServerTool(Name = "remove_entries", Destructive = true, Idempotent = true)]
    [Description("Removes entries from the YAZIO diary by their ids (take ids from get_diary).")]
    public Task<string> RemoveEntries(
        [Description("Ids of diary entries to remove.")] string[] entryIds,
        CancellationToken cancellationToken = default) =>
        Guard(async () =>
        {
            var ids = DiaryInput.ParseIds(entryIds);
            await writer.RemoveEntriesAsync(ids, cancellationToken);
            return $"Removed {ids.Count} entr{(ids.Count == 1 ? "y" : "ies")}.";
        });

    /// <summary>Turns expected failures into MCP errors whose message reaches the model.</summary>
    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ArgumentException or YazioApiException or YazioCredentialsMissingException)
        {
            throw new McpException(ex.Message);
        }
    }
}
