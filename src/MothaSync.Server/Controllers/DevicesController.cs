using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MothaSync.Server.Data;
using MothaSync.Server.Metrics;
using MothaSync.Shared.Contracts;

namespace MothaSync.Server.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(SyncDb db, IConfiguration configuration, SyncMetrics metrics) : ControllerBase
{
    /// <summary>Checks an enrollment token without registering a device.</summary>
    [HttpPost("verify-enrollment")]
    [AllowAnonymous]
    public IActionResult VerifyEnrollment(RegisterDeviceRequest request) =>
        IsValidEnrollmentToken(request.EnrollmentToken) ? NoContent() : Unauthorized();

    /// <summary>Registers a device, stores its token hash, and increments the registration counter.</summary>
    /// <returns>New device credentials, 401 if enrollment is invalid or unconfigured, or 400 if the device name is blank.</returns>
    /// <exception cref="Microsoft.Data.Sqlite.SqliteException">A database error prevents registration.</exception>
    [HttpPost("register")]
    [AllowAnonymous]
    public ActionResult<RegisterDeviceResponse> Register(RegisterDeviceRequest request)
    {
        if (!IsValidEnrollmentToken(request.EnrollmentToken))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return BadRequest("device_name is required.");
        }

        var token = SyncDb.NewToken();
        var deviceId = db.RegisterDevice(request, SyncDb.HashToken(token));
        metrics.DeviceRegistered();
        return new RegisterDeviceResponse { DeviceId = deviceId, DeviceToken = token };
    }

    private bool IsValidEnrollmentToken(string token)
    {
        var expected = configuration["Server:EnrollmentToken"];
        return !string.IsNullOrEmpty(expected)
               && CryptographicOperations.FixedTimeEquals(
                   SHA256.HashData(Encoding.UTF8.GetBytes(token)),
                   SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}
