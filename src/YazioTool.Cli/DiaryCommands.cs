using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using YazioTool.Core.Abstractions;
using YazioTool.Core.Application;
using YazioTool.Core.Yazio;

namespace YazioTool.Cli;

/// <summary>
/// Command definitions. Every command prints one JSON document to stdout;
/// failures print <c>{"error": "..."}</c> to stderr with a non-zero exit code.
/// </summary>
internal sealed class DiaryCommands(IDiaryReader reader, IDiaryWriter writer, TimeProvider timeProvider)
{
    private const int InvalidInput = 2;
    private const int YazioFailure = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private DateTime Now => timeProvider.GetLocalNow().DateTime;

    public RootCommand CreateRootCommand() => new("YAZIO food diary from the command line. Output is JSON.")
    {
        CreateDiaryCommand(),
        CreateAddCommand(),
        CreateRemoveCommand(),
    };

    private Command CreateDiaryCommand()
    {
        var date = DateOption();
        var command = new Command("diary", "Show the diary for a day: entries by meal with ids, totals, goals, remaining.") { date };
        command.SetAction((result, ct) => RunAsync(async () =>
            DiaryView.From(await reader.GetDayAsync(DiaryInput.ParseDate(result.GetValue(date), Now), ct))));
        return command;
    }

    private Command CreateAddCommand()
    {
        var name = new Option<string>("--name", "-n") { Description = "What was eaten.", Required = true };
        var kcal = new Option<double>("--kcal") { Description = "Energy of the whole portion, kcal.", Required = true };
        var protein = new Option<double>("--protein", "-p") { Description = "Protein, g.", Required = true };
        var fat = new Option<double>("--fat", "-f") { Description = "Fat, g.", Required = true };
        var carbs = new Option<double>("--carbs", "-c") { Description = "Carbohydrates, g.", Required = true };
        var meal = new Option<string>("--meal", "-m") { Description = "breakfast | lunch | dinner | snack.", Required = true };
        var date = DateOption();
        var time = new Option<string?>("--time", "-t") { Description = "HH:mm. Defaults to now for today, otherwise a typical time for the meal." };

        var command = new Command("add", "Add a quick entry (name + kcal and macros, no product from the database).")
        {
            name, kcal, protein, fat, carbs, meal, date, time,
        };
        command.SetAction((result, ct) => RunAsync(async () =>
        {
            var entry = DiaryInput.CreateQuickEntry(
                result.GetValue(name)!, result.GetValue(kcal), result.GetValue(protein), result.GetValue(fat),
                result.GetValue(carbs), result.GetValue(meal)!, result.GetValue(date), result.GetValue(time), Now);

            var id = await writer.AddQuickEntryAsync(entry, ct);
            return new
            {
                id,
                entry.Name,
                Meal = entry.Meal.ToString().ToLowerInvariant(),
                ConsumedAt = entry.ConsumedAt.ToString("yyyy-MM-dd HH:mm"),
                Nutrition = NutritionView.From(entry.Nutrition),
            };
        }));
        return command;
    }

    private Command CreateRemoveCommand()
    {
        var ids = new Argument<string[]>("ids") { Description = "Ids of diary entries (from `diary`).", Arity = ArgumentArity.OneOrMore };
        var command = new Command("remove", "Remove diary entries by id.") { ids };
        command.SetAction((result, ct) => RunAsync(async () =>
        {
            var parsed = DiaryInput.ParseIds(result.GetValue(ids)!);
            await writer.RemoveEntriesAsync(parsed, ct);
            return new { removed = parsed };
        }));
        return command;
    }

    private static Option<string?> DateOption() =>
        new("--date", "-d") { Description = "Day in YYYY-MM-DD. Defaults to today." };

    private static async Task<int> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(await action(), Json));
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or OptionsValidationException or YazioCredentialsMissingException)
        {
            return Fail(ex.Message, InvalidInput);
        }
        catch (Exception ex) when (ex is YazioApiException or HttpRequestException or TaskCanceledException)
        {
            return Fail(ex.Message, YazioFailure);
        }
    }

    private static int Fail(string message, int exitCode)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new { error = message }, Json));
        return exitCode;
    }
}
