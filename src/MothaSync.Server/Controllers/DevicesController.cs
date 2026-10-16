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
