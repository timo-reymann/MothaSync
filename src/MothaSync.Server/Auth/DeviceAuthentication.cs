using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MothaSync.Server.Data;

namespace MothaSync.Server.Auth;

public static class DeviceAuthentication
{
    public const string Scheme = "Device";
    public const string DeviceIdClaim = "device_id";
}

public sealed class DeviceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SyncDb db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header)
            || !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = header.ToString()["Bearer ".Length..].Trim();
        var deviceId = db.FindDeviceByToken(token);
        if (deviceId is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown device token."));
        }

        var identity = new ClaimsIdentity([new Claim(DeviceAuthentication.DeviceIdClaim, deviceId)], DeviceAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), DeviceAuthentication.Scheme)));
    }
}
