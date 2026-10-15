namespace MothaSync.Client.Test;

public sealed class SyncStatusStoreTests
{
    public SyncStatusStoreTests()
    {
        // SyncStatusStore is static; restore the default between tests.
        SyncStatusStore.Set(SyncStatus.Idle);
    }

    [Fact]
    public void Set_UpdatesCurrent_AndRaisesChangedOnce()
    {
        var raised = 0;
        Action handler = () => raised++;
        SyncStatusStore.Changed += handler;
        try
        {
            SyncStatusStore.Set(SyncStatus.Running);

            Assert.Equal(SyncStatus.Running, SyncStatusStore.Current);
            Assert.Equal(1, raised);
        }
        finally
        {
            SyncStatusStore.Changed -= handler;
        }
    }

    [Fact]
    public void Set_SameStatus_DoesNotRaiseChanged()
    {
        SyncStatusStore.Set(SyncStatus.Ok);
        var raised = 0;
        Action handler = () => raised++;
        SyncStatusStore.Changed += handler;
        try
        {
            SyncStatusStore.Set(SyncStatus.Ok);

            Assert.Equal(SyncStatus.Ok, SyncStatusStore.Current);
            Assert.Equal(0, raised);
        }
        finally
        {
            SyncStatusStore.Changed -= handler;
        }
    }

    [Fact]
    public void Set_RaisesHandlerAfterCurrentIsUpdated()
    {
        SyncStatus? seenDuringEvent = null;
        Action handler = () => seenDuringEvent = SyncStatusStore.Current;
        SyncStatusStore.Changed += handler;
        try
        {
            SyncStatusStore.Set(SyncStatus.Error);

            Assert.Equal(SyncStatus.Error, seenDuringEvent);
        }
        finally
        {
            SyncStatusStore.Changed -= handler;
        }
    }

    [Fact]
    public void Set_SequentialTransitions_TrackEachStep()
    {
        foreach (var status in new[] { SyncStatus.Waiting, SyncStatus.Running, SyncStatus.Ok, SyncStatus.Idle })
        {
            SyncStatusStore.Set(status);
            Assert.Equal(status, SyncStatusStore.Current);
        }
    }
}
