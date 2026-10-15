using MothaSync.Client.Views;

namespace MothaSync.Client.Test;

public sealed class RunInfoTests
{
    [Fact]
    public void RunInfo_EqualValuesAreEqual()
    {
        var a = new RunInfo("2026-01-02", "12", "1.5 GB", "3m 05s", Failed: false);
        var b = new RunInfo("2026-01-02", "12", "1.5 GB", "3m 05s", Failed: false);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void RunInfo_DifferentValuesAreNotEqual()
    {
        var baseline = new RunInfo("2026-01-02", "12", "1.5 GB", "3m 05s", Failed: false);

        Assert.NotEqual(baseline with { Failed = true }, baseline);
        Assert.NotEqual(baseline with { Date = "2026-01-03" }, baseline);
        Assert.NotEqual(baseline with { Files = "13" }, baseline);
    }
}
