using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MothaSync.Server.Auth;
using MothaSync.Server.Data;
using MothaSync.Shared.Contracts;

namespace MothaSync.Server.Test;

public sealed class DeviceAuthenticationHandlerTests : IDisposable
{
    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private sealed class StaticOptionsMonitor(AuthenticationSchemeOptions options) : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue => options;

        public AuthenticationSchemeOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(SyncDb db, string? authorizationHeader)
    {
        var handler = new DeviceAuthenticationHandler(
            new StaticOptionsMonitor(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            db);

        var httpContext = new DefaultHttpContext();
        if (authorizationHeader is not null)
        {
            httpContext.Request.Headers.Authorization = authorizationHeader;
        }

        await handler.InitializeAsync(
            new AuthenticationScheme(DeviceAuthentication.Scheme, null, typeof(DeviceAuthenticationHandler)),
            httpContext);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task NoAuthorizationHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync(_db.Db, authorizationHeader: null);

        Assert.True(result.None);
        Assert.Null(result.Ticket);
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer")]
    [InlineData("Token abc")]
    public async Task NonBearerAuthorization_ReturnsNoResult(string header)
    {
        var result = await AuthenticateAsync(_db.Db, header);

        Assert.True(result.None);
    }

    [Fact]
    public async Task UnknownBearerToken_ReturnsFailure()
    {
        var result = await AuthenticateAsync(_db.Db, "Bearer unknown-token");

        Assert.False(result.Succeeded);
        Assert.True(result.Failure is not null);
        Assert.Null(result.Ticket);
    }

    [Fact]
    public async Task ValidBearerToken_ReturnsSuccessWithDeviceIdClaim()
    {
        var token = SyncDb.NewToken();
        var deviceId = _db.Db.RegisterDevice(new RegisterDeviceRequest { DeviceName = "laptop" }, SyncDb.HashToken(token));

        var result = await AuthenticateAsync(_db.Db, $"Bearer {token}");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Ticket);
        Assert.Equal(DeviceAuthentication.Scheme, result.Ticket.AuthenticationScheme);
        var claim = Assert.Single(
            result.Ticket.Principal.Claims,
            c => c.Type == DeviceAuthentication.DeviceIdClaim);
        Assert.Equal(deviceId, claim.Value);
    }

    [Fact]
    public async Task BearerSchemeIsCaseInsensitive_AndTokenIsTrimmed()
    {
        var token = SyncDb.NewToken();
        var deviceId = _db.Db.RegisterDevice(new RegisterDeviceRequest { DeviceName = "laptop" }, SyncDb.HashToken(token));

        var result = await AuthenticateAsync(_db.Db, $"bearer  {token}  ");

        Assert.True(result.Succeeded);
        Assert.Equal(
            deviceId,
            result.Ticket.Principal.FindFirstValue(DeviceAuthentication.DeviceIdClaim));
    }
}
