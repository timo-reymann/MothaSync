using System;

namespace MothaSync.Client;

public enum SyncStatus
{
    Idle,
    Waiting,
    Running,
    Ok,
    Error,
}

public static class SyncStatusStore
{
    public static SyncStatus Current { get; private set; } = SyncStatus.Idle;

    public static event Action? Changed;

    public static void Set(SyncStatus status)
    {
        if (Current == status)
        {
            return;
        }

        Current = status;
        Changed?.Invoke();
    }
}
