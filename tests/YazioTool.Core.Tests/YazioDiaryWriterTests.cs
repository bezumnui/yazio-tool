using System.Net;
using System.Text.Json;
using NSubstitute;
using RichardSzalay.MockHttp;
using YazioTool.Core.Domain;
using YazioTool.Core.Yazio.Api;
using YazioTool.Core.Yazio.Diary;

namespace YazioTool.Core.Tests;

/// <summary>Checks the exact JSON sent to YAZIO through the real <see cref="YazioApiClient"/>.</summary>
public sealed class YazioDiaryWriterTests
{
    private const string BaseUrl = "https://yazio.test/v20/";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly MockHttpMessageHandler _http = new();
    private readonly YazioDiaryWriter _writer;

    public YazioDiaryWriterTests()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(_http, disposeHandler: false) { BaseAddress = new Uri(BaseUrl) });
        _writer = new YazioDiaryWriter(new YazioApiClient(factory));
    }

    [Fact]
    public async Task Quick_entry_is_posted_as_simple_product()
    {
        string? body = null;
        _http.Expect(HttpMethod.Post, BaseUrl + "user/consumed-items")
            .With(request =>
            {
                body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond(HttpStatusCode.NoContent);

        var entry = new QuickEntry(" Plov ", new Nutrition(650, 25, 28, 70), Meal.Lunch, new DateTime(2026, 11, 1, 13, 30, 0));
        var id = await _writer.AddQuickEntryAsync(entry, Ct);

        _http.VerifyNoOutstandingExpectation();
        using var json = JsonDocument.Parse(body!);
        Assert.Equal(0, json.RootElement.GetProperty("products").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("recipe_portions").GetArrayLength());

        var item = Assert.Single(json.RootElement.GetProperty("simple_products").EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("Plov", item.GetProperty("name").GetString());
        Assert.Equal("2026-11-01 13:30:00", item.GetProperty("date").GetString());
        Assert.Equal("lunch", item.GetProperty("daytime").GetString());
        Assert.False(item.GetProperty("is_ai_generated").GetBoolean());

        var nutrients = item.GetProperty("nutrients");
        Assert.Equal(650, nutrients.GetProperty("energy.energy").GetDouble());
        Assert.Equal(25, nutrients.GetProperty("nutrient.protein").GetDouble());
        Assert.Equal(28, nutrients.GetProperty("nutrient.fat").GetDouble());
        Assert.Equal(70, nutrients.GetProperty("nutrient.carb").GetDouble());
    }

    [Fact]
    public async Task Remove_sends_ids_array_with_delete()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        _http.Expect(HttpMethod.Delete, BaseUrl + "user/consumed-items")
            .WithContent(JsonSerializer.Serialize(ids))
            .Respond(HttpStatusCode.NoContent);

        await _writer.RemoveEntriesAsync(ids, Ct);

        _http.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Error_response_becomes_YazioApiException()
    {
        _http.When(HttpMethod.Delete, BaseUrl + "user/consumed-items").Respond(HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<YazioTool.Core.Yazio.YazioApiException>(
            () => _writer.RemoveEntriesAsync([Guid.NewGuid()], Ct));
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }
}
