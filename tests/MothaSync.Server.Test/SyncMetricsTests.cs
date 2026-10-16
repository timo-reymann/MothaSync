using Google.Protobuf.WellKnownTypes;
using MothaSync.Server.Metrics;
using MothaSync.Shared.Contracts;

namespace MothaSync.Server.Test;

public sealed class SyncMetricsTests
{
    [Fact]
    public async Task RunReported_CompletedRun_RecordsAllRunMetrics()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var metrics = new SyncMetrics(registry);
        var started = DateTimeOffset.UtcNow.AddMinutes(-6).AddSeconds(-12);
        var duration = TimeSpan.FromMinutes(2);
        metrics.RunReported("device-1", new SyncRun
        {
            Id = Guid.NewGuid().ToString(),
            Status = SyncRunStatus.Completed,
            StartedAt = Timestamp.FromDateTimeOffset(started),
            Duration = Duration.FromTimeSpan(duration),
            FilesChanged = 7,
            BytesTransferred = 4096,
        });

        var text = await MetricsTestHelpers.ExportToTextAsync(registry);

        var runs = MetricsTestHelpers.FindLine(text, "mothasync_sync_runs_total", ("device_id", "device-1"), ("status", "completed"));
        Assert.NotNull(runs);
        Assert.Equal(1, MetricsTestHelpers.ParseValue(runs));

        var files = MetricsTestHelpers.FindLine(text, "mothasync_sync_files_changed_total", ("device_id", "device-1"));
        Assert.NotNull(files);
        Assert.Equal(7, MetricsTestHelpers.ParseValue(files));

        var bytes = MetricsTestHelpers.FindLine(text, "mothasync_sync_bytes_transferred_total", ("device_id", "device-1"));
        Assert.NotNull(bytes);
        Assert.Equal(4096, MetricsTestHelpers.ParseValue(bytes));

        var durationCount = MetricsTestHelpers.FindLine(text, "mothasync_sync_run_duration_seconds_count", ("device_id", "device-1"));
        Assert.NotNull(durationCount);
        Assert.Equal(1, MetricsTestHelpers.ParseValue(durationCount));
        var durationSum = MetricsTestHelpers.FindLine(text, "mothasync_sync_run_duration_seconds_sum", ("device_id", "device-1"));
        Assert.NotNull(durationSum);
        Assert.Equal(120, MetricsTestHelpers.ParseValue(durationSum));

        var lastTimestamp = MetricsTestHelpers.FindLine(text, "mothasync_sync_last_run_timestamp_seconds", ("device_id", "device-1"));
        Assert.NotNull(lastTimestamp);
        Assert.Equal(started.Add(duration).ToUnixTimeSeconds(), MetricsTestHelpers.ParseValue(lastTimestamp));

        var lastResult = MetricsTestHelpers.FindLine(text, "mothasync_sync_last_run_result", ("device_id", "device-1"));
        Assert.NotNull(lastResult);
        Assert.Equal(1, MetricsTestHelpers.ParseValue(lastResult));
    }

    [Fact]
    public async Task RunReported_FailedRun_RecordsFailedRunWithZeroResult()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var metrics = new SyncMetrics(registry);
        metrics.RunReported("device-1", new SyncRun
        {
            Id = Guid.NewGuid().ToString(),
            Status = SyncRunStatus.Failed,
            StartedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(-1)),
        });

        var text = await MetricsTestHelpers.ExportToTextAsync(registry);

        var runs = MetricsTestHelpers.FindLine(text, "mothasync_sync_runs_total", ("device_id", "device-1"), ("status", "failed"));
        Assert.NotNull(runs);
        Assert.Equal(1, MetricsTestHelpers.ParseValue(runs));
        Assert.Null(MetricsTestHelpers.FindLine(text, "mothasync_sync_runs_total", ("device_id", "device-1"), ("status", "completed")));

        var lastResult = MetricsTestHelpers.FindLine(text, "mothasync_sync_last_run_result", ("device_id", "device-1"));
        Assert.NotNull(lastResult);
        Assert.Equal(0, MetricsTestHelpers.ParseValue(lastResult));

        // No duration was reported, so the timestamp falls back to the start time.
        var lastTimestamp = MetricsTestHelpers.FindLine(text, "mothasync_sync_last_run_timestamp_seconds", ("device_id", "device-1"));
        Assert.NotNull(lastTimestamp);
    }

    [Fact]
    public async Task RunReported_AggregatesAcrossRunsAndDevices()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var metrics = new SyncMetrics(registry);
        var run = NewRun();

        metrics.RunReported("device-a", run);
        metrics.RunReported("device-a", run);
        metrics.RunReported("device-b", run);

        var text = await MetricsTestHelpers.ExportToTextAsync(registry);

        var deviceA = MetricsTestHelpers.FindLine(text, "mothasync_sync_runs_total", ("device_id", "device-a"), ("status", "completed"));
        Assert.NotNull(deviceA);
        Assert.Equal(2, MetricsTestHelpers.ParseValue(deviceA));

        var deviceB = MetricsTestHelpers.FindLine(text, "mothasync_sync_runs_total", ("device_id", "device-b"), ("status", "completed"));
        Assert.NotNull(deviceB);
        Assert.Equal(1, MetricsTestHelpers.ParseValue(deviceB));

        var bytesA = MetricsTestHelpers.FindLine(text, "mothasync_sync_bytes_transferred_total", ("device_id", "device-a"));
        Assert.NotNull(bytesA);
        Assert.Equal(2000, MetricsTestHelpers.ParseValue(bytesA));
    }

    [Fact]
    public async Task DeviceRegistered_IncrementsCounter()
    {
        var registry = Prometheus.Metrics.NewCustomRegistry();
        var metrics = new SyncMetrics(registry);

        var before = MetricsTestHelpers.FindLine(
            await MetricsTestHelpers.ExportToTextAsync(registry), "mothasync_devices_registered_total");
        Assert.NotNull(before);
        Assert.Equal(0, MetricsTestHelpers.ParseValue(before));

        metrics.DeviceRegistered();
        metrics.DeviceRegistered();

        var line = MetricsTestHelpers.FindLine(
            await MetricsTestHelpers.ExportToTextAsync(registry), "mothasync_devices_registered_total");
        Assert.NotNull(line);
        Assert.Equal(2, MetricsTestHelpers.ParseValue(line));
    }

    private static SyncRun NewRun() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Status = SyncRunStatus.Completed,
        StartedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(-1)),
        Duration = Duration.FromTimeSpan(TimeSpan.FromSeconds(30)),
        FilesChanged = 1,
        BytesTransferred = 1000,
    };
}
