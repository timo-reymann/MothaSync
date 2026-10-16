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

        metrics.RunReported(DeviceId, run);
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
