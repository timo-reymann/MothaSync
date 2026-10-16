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
        _runs = factory.CreateCounter("mothasync_sync_runs_total", "Number of sync runs reported by clients.", "device_id", "status");
        _filesChanged = factory.CreateCounter("mothasync_sync_files_changed_total", "Files added, updated or deleted during sync runs.", "device_id");
        _bytesTransferred = factory.CreateCounter("mothasync_sync_bytes_transferred_total", "Bytes transferred during sync runs.", "device_id");
        _duration = factory.CreateHistogram("mothasync_sync_run_duration_seconds", "Wall-clock duration of sync runs.", new P.HistogramConfiguration
        {
            LabelNames = ["device_id"],
            Buckets = DurationBuckets,
        });
        _lastRunTimestamp = factory.CreateGauge("mothasync_sync_last_run_timestamp_seconds", "Completion timestamp (Unix time) of the last reported sync run; falls back to the start time while running.", "device_id");
        _lastRunResult = factory.CreateGauge("mothasync_sync_last_run_result", "1 when the last reported sync run completed, 0 otherwise.", "device_id");
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
    /// <param name="run">The report, with a required start time and optional duration.</param>
    /// <exception cref="InvalidOperationException">The start time or supplied duration is not a valid normalized protobuf value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The start time plus duration is outside the DateTimeOffset range.</exception>
    public void RunReported(string deviceId, SyncRun run)
    {
        var status = run.Status.ToString().ToLowerInvariant();
        _runs.Labels(deviceId, status).Inc();

        if (run.FilesChanged > 0)
        {
            _filesChanged.Labels(deviceId).Inc(run.FilesChanged);
        }

        if (run.BytesTransferred > 0)
        {
            _bytesTransferred.Labels(deviceId).Inc(run.BytesTransferred);
        }

        var completion = run.StartedAt.ToDateTimeOffset();
        if (run.Duration is not null)
        {
            var duration = run.Duration.ToTimeSpan();
            _duration.Labels(deviceId).Observe(duration.TotalSeconds);
            completion += duration;
        }

        _lastRunTimestamp.Labels(deviceId).Set(completion.ToUnixTimeSeconds());
        _lastRunResult.Labels(deviceId).Set(run.Status == SyncRunStatus.Completed ? 1 : 0);
    }

    /// <summary>Records one device registration in the server-wide counter.</summary>
    public void DeviceRegistered() => _devicesRegistered.Inc();
}
