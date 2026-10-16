using System;

namespace MothaSync.Client;

public static class SyncSchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public static bool IsDue(DateTimeOffset? lastCompleted, DateTimeOffset now)
    {
        return Remaining(lastCompleted, now) <= TimeSpan.Zero;
    }

    public static TimeSpan Remaining(DateTimeOffset? lastCompleted, DateTimeOffset now)
    {
        return lastCompleted is null ? TimeSpan.Zero : lastCompleted.Value + Interval - now;
    }

    public static string FormatCountdown(TimeSpan remaining)
    {
        var total = (int)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        return $"{total / 60}:{total % 60:00}";
    }

    public static string FormatLastSync(DateTimeOffset? lastCompleted, DateTimeOffset now)
    {
        if (lastCompleted is null)
        {
            return "Last sync: never";
        }

        var age = now - lastCompleted.Value;
        return age.TotalMinutes switch
        {
            < 1 => "Last sync: just now",
            < 60 => $"Last sync: {(int)age.TotalMinutes} min ago",
            < 24 * 60 => $"Last sync: {(int)age.TotalHours} h ago",
            _ => $"Last sync: {(int)age.TotalDays} d ago",
        };
    }
}
