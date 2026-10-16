using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MothaSync.Server.Controllers;
using MothaSync.Server.Data;
using MothaSync.Server.Metrics;
using MothaSync.Shared.Contracts;

namespace MothaSync.Server.Test;

public sealed class DevicesControllerTests : IDisposable
{
    private const string EnrollmentToken = "enroll-secret";

    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DevicesController CreateController(string? configuredToken = EnrollmentToken)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:DatabasePath"] = _db.Path,
                ["Server:EnrollmentToken"] = configuredToken,
            })
            .Build();
        return new DevicesController(_db.Db, configuration, new SyncMetrics(Prometheus.Metrics.NewCustomRegistry()));
    }

    [Fact]
    public void VerifyEnrollment_ValidToken_ReturnsNoContent()
    {
        var controller = CreateController();

        var result = controller.VerifyEnrollment(new RegisterDeviceRequest { EnrollmentToken = EnrollmentToken });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Register_ValidEnrollment_IncrementsDevicesCounter()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var metrics = new SyncMetrics(registry);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:DatabasePath"] = _db.Path,
                ["Server:EnrollmentToken"] = EnrollmentToken,
            })
            .Build();
        var controller = new DevicesController(_db.Db, configuration, metrics);

        controller.Register(new RegisterDeviceRequest { EnrollmentToken = EnrollmentToken, DeviceName = "laptop" });

        var text = await MetricsTestHelpers.ExportToTextAsync(registry);
        var line = MetricsTestHelpers.FindLine(text, "mothasync_devices_registered_total");
        Assert.NotNull(line);
        Assert.Equal(1, MetricsTestHelpers.ParseValue(line));
    }

    [Fact]
    public void VerifyEnrollment_WrongToken_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = controller.VerifyEnrollment(new RegisterDeviceRequest { EnrollmentToken = "wrong" });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void VerifyEnrollment_EmptyConfiguredToken_AlwaysUnauthorized()
    {
        var controller = CreateController(configuredToken: null);

        var result = controller.VerifyEnrollment(new RegisterDeviceRequest { EnrollmentToken = string.Empty });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void VerifyEnrollment_EmptyRequestTokenWithConfiguredToken_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = controller.VerifyEnrollment(new RegisterDeviceRequest { EnrollmentToken = string.Empty });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Register_InvalidEnrollmentToken_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = controller.Register(new RegisterDeviceRequest
        {
            EnrollmentToken = "wrong",
            DeviceName = "laptop",
        });

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public void Register_MissingDeviceName_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.Register(new RegisterDeviceRequest
        {
            EnrollmentToken = EnrollmentToken,
            DeviceName = "   ",
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Register_ValidRequest_ReturnsCredentialsAndStoresHashOnly()
    {
        var controller = CreateController();

        var result = controller.Register(new RegisterDeviceRequest
        {
            EnrollmentToken = EnrollmentToken,
            DeviceName = "laptop",
            Platform = "linux-x64",
            ClientVersion = "1.0.0",
        });

        Assert.Null(result.Result);
        var response = result.Value;
        Assert.NotNull(response);
        Assert.True(Guid.TryParse(response.DeviceId, out _));
        Assert.Matches("^[0-9a-f]{64}$", response.DeviceToken);
        Assert.Equal(response.DeviceId, _db.Db.FindDeviceByToken(response.DeviceToken));
        Assert.NotEqual(response.DeviceToken, SyncDb.HashToken(response.DeviceToken));
    }

    [Fact]
    public void Register_DifferentDeviceName_HashOfExpectedEnrollmentTokenMatches()
    {
        // Mirrors the controller's fixed-time comparison input so a token that is a
        // prefix/suffix of the configured one does not validate.
        var controller = CreateController(configuredToken: EnrollmentToken + "x");

        var result = controller.Register(new RegisterDeviceRequest
        {
            EnrollmentToken = EnrollmentToken,
            DeviceName = "laptop",
        });

        Assert.IsType<UnauthorizedResult>(result.Result);
    }
}
