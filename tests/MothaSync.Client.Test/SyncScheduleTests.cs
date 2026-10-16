namespace MothaSync.Client.Test;

public sealed class SyncScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsDue_WhenNeverSynced_ReturnsTrue()
    {
        Assert.True(SyncSchedule.IsDue(null, Now));
    }

    [Fact]
    public void IsDue_ReturnsFalseWithinAnHour_AndTrueAfter()
    {
        Assert.False(SyncSchedule.IsDue(Now.AddMinutes(-59), Now));
        Assert.True(SyncSchedule.IsDue(Now.AddMinutes(-61), Now));
    }

    [Theory]
    [InlineData(0, "60:00")]
    [InlineData(17.5, "42:30")]
    [InlineData(59, "1:00")]
    public void FormatCountdown_ShowsMinutesAndSeconds(double minutesAgo, string expected)
    {
        var remaining = SyncSchedule.Remaining(Now.AddMinutes(-minutesAgo), Now);

        Assert.Equal(expected, SyncSchedule.FormatCountdown(remaining));
    }

    [Fact]
    public void FormatCountdown_ClampsNegativeToZero()
    {
        Assert.Equal("0:00", SyncSchedule.FormatCountdown(TimeSpan.FromSeconds(-5)));
    }

    [Theory]
    [InlineData(0.2, "Last sync: just now")]
    [InlineData(12, "Last sync: 12 min ago")]
    [InlineData(180, "Last sync: 3 h ago")]
    [InlineData(3000, "Last sync: 2 d ago")]
    public void FormatLastSync_UsesRelativeUnits(double minutesAgo, string expected)
    {
        Assert.Equal(expected, SyncSchedule.FormatLastSync(Now.AddMinutes(-minutesAgo), Now));
    }

    [Fact]
    public void FormatLastSync_WhenNever()
    {
        Assert.Equal("Last sync: never", SyncSchedule.FormatLastSync(null, Now));
    }
}
