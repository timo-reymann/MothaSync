using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MothaSync.Server.Auth;
using MothaSync.Server.Data;
using MothaSync.Server.Metrics;
using MothaSync.Shared.Contracts;

namespace MothaSync.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/runs")]
public sealed class RunsController(SyncDb db, SyncMetrics metrics) : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private string DeviceId => User.FindFirst(DeviceAuthentication.DeviceIdClaim)!.Value;

    /// <summary>Creates or updates a run for the authenticated device and records its metrics.</summary>
    /// <remarks>
    /// Ignores the run's device ID. Each accepted report, including retries and updates, adds to the metrics.
    /// Persistence precedes metric recording, so a metrics error can occur after the run has been stored.
    /// </remarks>
    /// <returns>An empty response on success, 400 for a missing run, invalid UUID, missing start time, or unspecified status,
    /// or 409 if the run ID belongs to another device.</returns>
    /// <exception cref="Microsoft.Data.Sqlite.SqliteException">A database error prevents storing the run.</exception>
    /// <exception cref="InvalidOperationException">The start time or supplied duration is not a valid normalized protobuf value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The start time plus duration is outside the DateTimeOffset range.</exception>
    [HttpPost]
    public ActionResult<ReportSyncRunResponse> Report(ReportSyncRunRequest request)
    {
        var run = request.Run;
        if (run is null || !Guid.TryParse(run.Id, out _) || run.StartedAt is null
            || run.Status == SyncRunStatus.Unspecified)
        {
            return BadRequest("run.id (UUID), run.status and run.started_at are required.");
        }

        // device_id always comes from the token, never from the body.
        if (!db.UpsertRun(DeviceId, run))
        {
            return Conflict();
        }

        // The hostname label comes from the registration row for the device.
        metrics.RunReported(DeviceId, db.FindDeviceName(DeviceId) ?? string.Empty, run);
        return new ReportSyncRunResponse();
    }

    [HttpGet]
    public ActionResult<ListSyncRunsResponse> List([FromQuery] ListSyncRunsRequest request)
    {
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);
        var offset = 0;
        if (!string.IsNullOrEmpty(request.PageToken)
            && (!int.TryParse(request.PageToken, NumberStyles.None, CultureInfo.InvariantCulture, out offset)))
        {
            return BadRequest("Invalid page_token.");
        }

        var (runs, hasMore) = db.ListRuns(DeviceId, request.Status, offset, pageSize);
        var response = new ListSyncRunsResponse();
        response.Runs.AddRange(runs);
        if (hasMore)
        {
            response.NextPageToken = (offset + pageSize).ToString(CultureInfo.InvariantCulture);
        }

        return response;
    }
}
