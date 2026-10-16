using System;

namespace MothaSync.Client;

public static class UserNotifications
{
    public static event Action<string, string>? Error;

    public static void ShowError(string title, string message) => Error?.Invoke(title, message);
}
