using System.Net;
using System.Text.Json;
using NSubstitute;
using RichardSzalay.MockHttp;
using YazioTool.Core.Yazio.Api;
using YazioTool.Core.Yazio.Streak;

namespace YazioTool.Core.Tests;

/// <summary>Checks the requests sent to YAZIO through the real <see cref="YazioApiClient"/>.</summary>
public sealed class YazioStreakServiceTests
{
    private const string BaseUrl = "https://yazio.test/v22/";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly MockHttpMessageHandler _http = new(BackendDefinitionBehavior.Always);
    private readonly YazioStreakService _service;

    public YazioStreakServiceTests()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(_http, disposeHandler: false) { BaseAddress = new Uri(BaseUrl) });
        _service = new YazioStreakService(new YazioApiClient(factory));
    }

    [Fact]
    public async Task Track_posts_today_with_diary_meals_and_next_count()
    {
        _http.When(HttpMethod.Get, BaseUrl + "user/streak").Respond("application/json",
            """{"2026-11-02":{"daytimes":["breakfast"],"streak_count":75,"freeze_count":1,"origin_of_recovery":null}}""");
        _http.When(HttpMethod.Get, BaseUrl + "user/consumed-items").Respond("application/json",
            """{"products":[],"recipe_portions":[],"simple_products":[{"id":"8d899c45-b78e-44fc-bf6f-58efa6a5a1d0","date":"2026-11-03 12:52:00","daytime":"snack","name":"Candy","nutrients":{}}]}""");

        string? body = null;
        _http.Expect(HttpMethod.Post, BaseUrl + "user/streak/2026-11-03")
            .With(request =>
            {
                body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond(HttpStatusCode.NoContent);

        var day = await _service.TrackDayAsync(new DateOnly(2026, 11, 3), Ct);

        _http.VerifyNoOutstandingExpectation();
        Assert.Equal(76, day.StreakCount);

        using var json = JsonDocument.Parse(body!);
        Assert.Equal("snack", Assert.Single(json.RootElement.GetProperty("daytimes").EnumerateArray()).GetString());
        Assert.Equal(76, json.RootElement.GetProperty("streak_count").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("freeze_count").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("origin_of_recovery").ValueKind);
    }

    [Fact]
    public async Task Get_returns_days_oldest_first()
    {
        _http.When(HttpMethod.Get, BaseUrl + "user/streak").Respond("application/json",
            """{"2026-11-03":{"daytimes":["snack","breakfast"],"streak_count":2,"freeze_count":0,"origin_of_recovery":null},"2026-11-02":{"daytimes":["dinner"],"streak_count":1,"freeze_count":0,"origin_of_recovery":null}}""");

        var days = await _service.GetDaysAsync(Ct);

        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3)], days.Select(d => d.Date));
        Assert.Equal(2, days[1].Meals.Count);
    }
}
