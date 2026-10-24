using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using MothaSync.Shared.Contracts;

namespace MothaSync.Server.Data;

public sealed class SyncDb
{
    private readonly string _connectionString;

    public SyncDb(IConfiguration configuration)
    {
        var path = configuration["Server:DatabasePath"] ?? "mothasync-server.db";
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        Initialize();
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public string RegisterDevice(RegisterDeviceRequest request, string tokenHash)
    {
        var deviceId = Guid.NewGuid().ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO Devices (Id, TokenHash, Name, Platform, ClientVersion, RegisteredAtUtc)
                              VALUES ($id, $hash, $name, $platform, $version, $now);
                              """;
        command.Parameters.AddWithValue("$id", deviceId);
        command.Parameters.AddWithValue("$hash", tokenHash);
        command.Parameters.AddWithValue("$name", request.DeviceName);
        command.Parameters.AddWithValue("$platform", request.Platform);
        command.Parameters.AddWithValue("$version", request.ClientVersion);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        return deviceId;
    }

    public string? FindDeviceByToken(string token)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Devices WHERE TokenHash = $hash;";
        command.Parameters.AddWithValue("$hash", HashToken(token));
        return command.ExecuteScalar() as string;
    }

    /// <summary>Returns the persisted name of the device, or null when the ID is unknown.</summary>
    public string? FindDeviceName(string deviceId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Devices WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", deviceId);
        return command.ExecuteScalar() as string;
    }

    /// <summary>Creates or updates a run owned by <paramref name="deviceId"/>. Returns false if the id belongs to another device.</summary>
    public bool UpsertRun(string deviceId, SyncRun run)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO SyncRuns (Id, DeviceId, Status, StartedAtUtc, DurationMs, FilesChanged, BytesTransferred, ErrorMessage)
                              VALUES ($id, $device, $status, $started, $duration, $files, $bytes, $error)
                              ON CONFLICT(Id) DO UPDATE SET
                                  Status = excluded.Status,
                                  StartedAtUtc = excluded.StartedAtUtc,
                                  DurationMs = excluded.DurationMs,
                                  FilesChanged = excluded.FilesChanged,
                                  BytesTransferred = excluded.BytesTransferred,
                                  ErrorMessage = excluded.ErrorMessage
                              WHERE SyncRuns.DeviceId = excluded.DeviceId;
                              """;
        command.Parameters.AddWithValue("$id", run.Id);
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$status", (int)run.Status);
        command.Parameters.AddWithValue("$started", run.StartedAt.ToDateTimeOffset().ToString("O"));
        command.Parameters.AddWithValue("$duration", run.Duration is null ? DBNull.Value : (long)run.Duration.ToTimeSpan().TotalMilliseconds);
        command.Parameters.AddWithValue("$files", run.FilesChanged);
        command.Parameters.AddWithValue("$bytes", run.BytesTransferred);
        command.Parameters.AddWithValue("$error", run.ErrorMessage);
        return command.ExecuteNonQuery() > 0;
    }

    public (List<SyncRun> Runs, bool HasMore) ListRuns(string deviceId, SyncRunStatus status, int offset, int limit)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT Id, Status, StartedAtUtc, DurationMs, FilesChanged, BytesTransferred, ErrorMessage
                              FROM SyncRuns
                              WHERE DeviceId = $device AND ($status = 0 OR Status = $status)
                              ORDER BY StartedAtUtc DESC
                              LIMIT $limit OFFSET $offset;
                              """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$limit", limit + 1);
        command.Parameters.AddWithValue("$offset", offset);

        var runs = new List<SyncRun>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var run = new SyncRun
            {
                Id = reader.GetString(0),
                Status = (SyncRunStatus)reader.GetInt32(1),
                StartedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.Parse(reader.GetString(2))),
                FilesChanged = reader.GetInt64(4),
                BytesTransferred = reader.GetInt64(5),
                ErrorMessage = reader.GetString(6),
                DeviceId = deviceId
            };
            if (!reader.IsDBNull(3))
            {
                run.Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(TimeSpan.FromMilliseconds(reader.GetInt64(3)));
            }

            runs.Add(run);
        }

        var hasMore = runs.Count > limit;
        if (hasMore)
        {
            runs.RemoveAt(runs.Count - 1);
        }

        return (runs, hasMore);
    }

    /// <summary>The name, the registration time, and the most recent run, if any, per device.</summary>
    /// <remarks>Used to restore the per-device metrics after a server restart.</remarks>
    public List<(string DeviceId, string Name, DateTimeOffset RegistrationTime, SyncRun? LastRun)> ListDeviceRunStates()
    {
        var states = new List<(string DeviceId, string Name, DateTimeOffset RegistrationTime, SyncRun? LastRun)>();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT d.Id, d.Name, d.RegisteredAtUtc, r.Status, r.StartedAtUtc, r.DurationMs
                              FROM Devices d
                              LEFT JOIN SyncRuns r ON r.Id = (
                                  SELECT Id FROM SyncRuns WHERE DeviceId = d.Id
                                  ORDER BY StartedAtUtc DESC, Id DESC LIMIT 1
                              );
                              """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            var registrationTime = DateTimeOffset.Parse(reader.GetString(2));
            SyncRun? lastRun = null;
            if (!reader.IsDBNull(3))
            {
                lastRun = new SyncRun
                {
                    Status = (SyncRunStatus)reader.GetInt32(3),
                    StartedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.Parse(reader.GetString(4))),
                };
                if (!reader.IsDBNull(5))
                {
                    lastRun.Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(TimeSpan.FromMilliseconds(reader.GetInt64(5)));
                }
            }

            states.Add((reader.GetString(0), name, registrationTime, lastRun));
        }

        return states;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS Devices (
                                  Id TEXT PRIMARY KEY,
                                  TokenHash TEXT NOT NULL UNIQUE,
                                  Name TEXT NOT NULL,
                                  Platform TEXT NOT NULL,
                                  ClientVersion TEXT NOT NULL,
                                  RegisteredAtUtc TEXT NOT NULL
                              );

                              CREATE TABLE IF NOT EXISTS SyncRuns (
                                  Id TEXT PRIMARY KEY,
                                  DeviceId TEXT NOT NULL REFERENCES Devices(Id) ON DELETE CASCADE,
                                  Status INTEGER NOT NULL,
                                  StartedAtUtc TEXT NOT NULL,
                                  DurationMs INTEGER,
                                  FilesChanged INTEGER NOT NULL,
                                  BytesTransferred INTEGER NOT NULL,
                                  ErrorMessage TEXT NOT NULL
                              );

                              CREATE INDEX IF NOT EXISTS IX_SyncRuns_Device_Started ON SyncRuns(DeviceId, StartedAtUtc DESC);
                              """;
        command.ExecuteNonQuery();
    }
}
