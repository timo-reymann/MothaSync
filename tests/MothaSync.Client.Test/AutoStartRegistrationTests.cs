namespace MothaSync.Client.Test;

public sealed class AutoStartRegistrationTests
{
    [Fact]
    public void HasAutoStartArgument_WhenArgumentIsTheOnlyOne_ReturnsTrue()
    {
        Assert.True(AutoStartRegistration.HasAutoStartArgument(new[] { "--mothasync-autostart" }));
    }

    [Fact]
    public void HasAutoStartArgument_WhenArgumentIsAmongOthers_ReturnsTrue()
    {
        string[] args = ["--other", "--mothasync-autostart", "file.txt"];

        Assert.True(AutoStartRegistration.HasAutoStartArgument(args));
    }

    [Fact]
    public void HasAutoStartArgument_WhenArgsAreEmpty_ReturnsFalse()
    {
        Assert.False(AutoStartRegistration.HasAutoStartArgument(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("--other")]
    [InlineData("--mothasync-autostart2")]
    [InlineData("--MOTHASYNO-AUTOSTART")]
    [InlineData("--Mothasync-Autostart")]
    public void HasAutoStartArgument_WhenArgumentDoesNotMatch_ReturnsFalse(string arg)
    {
        Assert.False(AutoStartRegistration.HasAutoStartArgument(new[] { arg }));
    }
}
