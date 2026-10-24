using MothaSync.Shared.Contracts;
using P = Prometheus;

namespace MothaSync.Server.Metrics;

/// <summary>Business metrics for sync runs and device registrations.</summary>
public sealed class SyncMetrics
{
    private static readonly double[] DurationBuckets = [1, 2, 5, 10, 30, 60, 120, 300, 600, 1800, 3600];

    private readonly P.Counter _runs;
    private readonly P.Counter _filesChanged;
    private readonly P.Counter _bytesTransferred;
    private readonly P.Histogram _duration;
    private readonly P.Gauge _lastRunTimestamp;
    private readonly P.Gauge _lastRunResult;
    private readonly P.Counter _devicesRegistered;

    /// <summary>Registers business metric collectors in the default Prometheus registry.</summary>
    public SyncMetrics() : this(P.Metrics.DefaultRegistry)
    {
    }

    /// <summary>Registers business metric collectors in the supplied Prometheus registry.</summary>
    public SyncMetrics(P.CollectorRegistry registry)
    {
        var factory = P.Metrics.WithCustomRegistry(registry);
        _runs = factory.CreateCounter("mothasync_sync_runs_total", "Number of sync runs reported by clients.", "device_id", "hostname", "status");
        _filesChanged = factory.CreateCounter("mothasync_sync_files_changed_total", "Files added, updated or deleted during sync runs.", "device_id", "hostname");
        _bytesTransferred = factory.CreateCounter("mothasync_sync_bytes_transferred_total", "Bytes transferred during sync runs.", "device_id", "hostname");
        _duration = factory.CreateHistogram("mothasync_sync_run_duration_seconds", "Wall-clock duration of sync runs.", new P.HistogramConfiguration
        {
            LabelNames = ["device_id", "hostname"],
            Buckets = DurationBuckets,
        });
        _lastRunTimestamp = factory.CreateGauge("mothasync_sync_last_run_timestamp_seconds", "Completion timestamp (Unix time) of the last reported sync run; falls back to the device registration time until the first report.", "device_id", "hostname");
        _lastRunResult = factory.CreateGauge("mothasync_sync_last_run_result", "1 when the last reported sync run completed, 0 otherwise.", "device_id", "hostname");
        _devicesRegistered = factory.CreateCounter("mothasync_devices_registered_total", "Devices registered with the server.");
    }

    /// <summary>Records a report in the device's counters, duration histogram, and last-run gauges.</summary>
    /// <remarks>
    /// Each call counts a report by status and adds positive file and byte totals, even for a repeated run ID.
    /// A supplied duration is observed in seconds. The last-run timestamp is the start time plus duration,
    /// or just the start time when duration is absent, in Unix seconds. The result is 1 only for Completed, otherwise 0.
    /// Counters already updated are not rolled back if time conversion fails.
    /// </remarks>
    /// <param name="deviceId">The metric label identifying the reporting device; the run's device ID is ignored.</param>
    /// <param name="hostname">The registered name of the reporting device, as a second metric label.</param>
    /// <param name="run">The report, with a required start time and optional duration.</param>
    /// <exception cref="InvalidOperationException">The start time or supplied duration is not a valid normalized protobuf value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The start time plus duration is outside the DateTimeOffset range.</exception>
    public void RunReported(string deviceId, string hostname, SyncRun run)
    {
        var status = run.Status.ToString().ToLowerInvariant();
        _runs.Labels(deviceId, hostname, status).Inc();

        if (run.FilesChanged > 0)
        {
            _filesChanged.Labels(deviceId, hostname).Inc(run.FilesChanged);
        }

        if (run.BytesTransferred > 0)
        {
            _bytesTransferred.Labels(deviceId, hostname).Inc(run.BytesTransferred);
        }

        var completion = run.StartedAt.ToDateTimeOffset();
        if (run.Duration is not null)
        {
            var duration = run.Duration.ToTimeSpan();
            _duration.Labels(deviceId, hostname).Observe(duration.TotalSeconds);
            completion += duration;
        }

        UpdateLastRunGauges(deviceId, hostname, run.Status, completion);
    }

    /// <summary>Records one device registration in the server-wide counter and creates the per-device last-run gauges.</summary>
    /// <remarks>
    /// The last-run timestamp starts at the current time, so an inactivity alert on the gauges
    /// covers the device before its first report arrives.
    /// </remarks>
    /// <param name="deviceId">The server-generated ID of the registered device.</param>
    /// <param name="hostname">The registered name of the device.</param>
    public void DeviceRegistered(string deviceId, string hostname)
    {
        _devicesRegistered.Inc();
        UpdateLastRunGauges(deviceId, hostname, SyncRunStatus.Unspecified, DateTimeOffset.UtcNow);
    }

    /// <summary>Restores the per-device last-run gauges from persisted state, without incrementing any counter.</summary>
    /// <remarks>
    /// A device with a persisted run uses that run's completion time and status.
    /// A device without runs uses its registration time, so the inactivity query
    /// keeps matching the device after a restart.
    /// </remarks>
    /// <param name="deviceId">The device ID whose gauge series to restore.</param>
    /// <param name="hostname">The registered name of the device.</param>
    /// <param name="lastRun">The last persisted run of the device, or null when the device has none.</param>
    /// <param name="registrationTime">The persisted registration time as the fallback timestamp.</param>
    public void DeviceRestored(string deviceId, string hostname, SyncRun? lastRun, DateTimeOffset registrationTime)
    {
        var completion = registrationTime;
        if (lastRun is not null)
        {
            completion = lastRun.StartedAt.ToDateTimeOffset();
            if (lastRun.Duration is not null)
            {
                completion += lastRun.Duration.ToTimeSpan();
            }
        }

        UpdateLastRunGauges(deviceId, hostname, lastRun?.Status ?? SyncRunStatus.Unspecified, completion);
    }

    /// <summary>Sets both last-run gauges for a device; the entry point for the series of that device.</summary>
    private void UpdateLastRunGauges(string deviceId, string hostname, SyncRunStatus status, DateTimeOffset completion)
    {
        _lastRunTimestamp.Labels(deviceId, hostname).Set(completion.ToUnixTimeSeconds());
        _lastRunResult.Labels(deviceId, hostname).Set(status == SyncRunStatus.Completed ? 1 : 0);
    }
}
