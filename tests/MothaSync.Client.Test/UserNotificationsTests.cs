namespace MothaSync.Client.Test;

public sealed class UserNotificationsTests
{
    [Fact]
    public void ShowError_RaisesErrorNotification()
    {
        string? title = null;
        string? message = null;
        void Handler(string notificationTitle, string notificationMessage)
        {
            title = notificationTitle;
            message = notificationMessage;
        }

        UserNotifications.Error += Handler;
        try
        {
            UserNotifications.ShowError("Sync failed", "The destination is unavailable.");
        }
        finally
        {
            UserNotifications.Error -= Handler;
        }

        Assert.Equal("Sync failed", title);
        Assert.Equal("The destination is unavailable.", message);
    }
}
