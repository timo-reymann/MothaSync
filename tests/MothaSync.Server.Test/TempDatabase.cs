using Microsoft.Extensions.Configuration;

namespace MothaSync.Server.Test;

/// <summary>Creates a real <see cref="Data.SyncDb"/> backed by a unique temporary SQLite file.</summary>
internal sealed class TempDatabase : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"mothasync-test-{Guid.NewGuid():N}.db");

    public IConfiguration Configuration { get; }

    public Data.SyncDb Db { get; }

    public TempDatabase()
    {
        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:DatabasePath"] = Path })
            .Build();
        Db = new Data.SyncDb(Configuration);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(Path);
    }
}
