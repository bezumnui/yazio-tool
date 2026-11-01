using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using YazioTool.Core.Yazio;
using YazioTool.Core.Yazio.Api;
using YazioTool.Core.Yazio.Auth;

namespace YazioTool.Core.Tests;

public sealed class YazioAccessTokenProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IYazioTokenClient _tokenClient = Substitute.For<IYazioTokenClient>();
    private readonly ITokenStore _store = Substitute.For<ITokenStore>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly YazioAccessTokenProvider _provider;

    public YazioAccessTokenProviderTests()
    {
        var options = Options.Create(new YazioOptions { Username = "user", Password = "secret" });
        _provider = new YazioAccessTokenProvider(_tokenClient, _store, options, _time);
    }

    [Fact]
    public async Task Reuses_token_until_it_expires()
    {
        _tokenClient.RequestWithPasswordAsync("user", "secret", Arg.Any<CancellationToken>())
            .Returns(new TokenResponse("access-1", "refresh-1", 3600));

        Assert.Equal("access-1", await _provider.GetAccessTokenAsync(Ct));
        Assert.Equal("access-1", await _provider.GetAccessTokenAsync(Ct));

        await _tokenClient.Received(1).RequestWithPasswordAsync("user", "secret", Arg.Any<CancellationToken>());
        await _store.Received(1).SaveAsync(Arg.Is<StoredToken>(t => t.AccessToken == "access-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uses_refresh_token_after_expiry()
    {
        _tokenClient.RequestWithPasswordAsync(default!, default!, default).ReturnsForAnyArgs(new TokenResponse("access-1", "refresh-1", 3600));
        _tokenClient.RequestWithRefreshTokenAsync("refresh-1", Arg.Any<CancellationToken>())
            .Returns(new TokenResponse("access-2", "refresh-2", 3600));

        await _provider.GetAccessTokenAsync(Ct);
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal("access-2", await _provider.GetAccessTokenAsync(Ct));
        await _tokenClient.ReceivedWithAnyArgs(1).RequestWithPasswordAsync(default!, default!, default);
    }

    [Fact]
    public async Task Falls_back_to_password_when_refresh_fails()
    {
        _tokenClient.RequestWithPasswordAsync(default!, default!, default).ReturnsForAnyArgs(
            new TokenResponse("access-1", "refresh-1", 3600),
            new TokenResponse("access-2", "refresh-2", 3600));
        _tokenClient.RequestWithRefreshTokenAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new YazioApiException(HttpStatusCode.BadRequest, "invalid_grant"));

        await _provider.GetAccessTokenAsync(Ct);
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal("access-2", await _provider.GetAccessTokenAsync(Ct));
    }

    [Fact]
    public async Task Uses_stored_token_without_logging_in()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new StoredToken("stored", "refresh", _time.GetUtcNow().AddHours(1)));

        Assert.Equal("stored", await _provider.GetAccessTokenAsync(Ct));
        await _tokenClient.DidNotReceiveWithAnyArgs().RequestWithPasswordAsync(default!, default!, default);
    }

    [Fact]
    public async Task Works_from_stored_token_without_credentials()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new StoredToken("stored", "refresh", _time.GetUtcNow().AddHours(1)));

        Assert.Equal("stored", await CreateProvider(new YazioOptions()).GetAccessTokenAsync(Ct));
    }

    [Fact]
    public async Task Missing_credentials_fail_only_when_login_is_needed()
    {
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new StoredToken("expired", "refresh", _time.GetUtcNow().AddHours(-1)));
        _tokenClient.RequestWithRefreshTokenAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new YazioApiException(HttpStatusCode.BadRequest, "invalid_grant"));

        await Assert.ThrowsAsync<YazioCredentialsMissingException>(
            () => CreateProvider(new YazioOptions()).GetAccessTokenAsync(Ct));
    }

    private YazioAccessTokenProvider CreateProvider(YazioOptions options) =>
        new(_tokenClient, _store, Options.Create(options), _time);

    [Fact]
    public async Task Invalidate_forces_new_token()
    {
        _tokenClient.RequestWithPasswordAsync(default!, default!, default).ReturnsForAnyArgs(new TokenResponse("access-1", "refresh-1", 3600));
        _tokenClient.RequestWithRefreshTokenAsync("refresh-1", Arg.Any<CancellationToken>())
            .Returns(new TokenResponse("access-2", "refresh-2", 3600));

        await _provider.GetAccessTokenAsync(Ct);
        _provider.Invalidate();

        Assert.Equal("access-2", await _provider.GetAccessTokenAsync(Ct));
    }
}
