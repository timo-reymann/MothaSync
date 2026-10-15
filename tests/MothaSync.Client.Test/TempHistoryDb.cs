namespace MothaSync.Client.Test;

/// <summary>Creates a real <see cref="RunLogStore"/> backed by a unique temporary SQLite file.</summary>
internal sealed class TempHistoryDb : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"mothasync-history-test-{Guid.NewGuid():N}.db");

    public RunLogStore Store { get; }

    public TempHistoryDb() => Store = new RunLogStore(Path);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(Path);
    }
}
