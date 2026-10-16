using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using MothaSync.Shared;
using ContractStatus = MothaSync.Shared.Contracts.SyncRunStatus;
using SyncRun = MothaSync.Shared.Contracts.SyncRun;

namespace MothaSync.Client;

public sealed class RunLogStore
{
    private const int RetainedRunCount = 100;
    private readonly string _connectionString;

    public RunLogStore() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MothaSync",
        "history.db"))
    {
    }

    internal RunLogStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false
        }.ToString();

        Initialize();
    }

    public long StartRun(string sourcePath, string destinationPath)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var remoteId = Guid.NewGuid().ToString();
        long runId;
        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                                  INSERT INTO SyncRuns (RemoteId, StartedAtUtc, Status, SourcePath, DestinationPath)
                                  VALUES ($remoteId, $startedAtUtc, $status, $sourcePath, $destinationPath);
                                  SELECT last_insert_rowid();
                                  """;
            command.Parameters.AddWithValue("$remoteId", remoteId);
            command.Parameters.AddWithValue("$startedAtUtc", startedAt.ToString("O"));
            command.Parameters.AddWithValue("$status", SyncRunStatus.Running.ToString());
            command.Parameters.AddWithValue("$sourcePath", sourcePath);
            command.Parameters.AddWithValue("$destinationPath", destinationPath);
            runId = (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Could not create sync run."));
        }

        return runId;
    }

    public void CompleteRun(
        long runId,
        SyncRunStatus status,
        long filesProcessed,
        long bytesTransferred,
        string logText)
    {
        CompleteRunAsync(runId, status, filesProcessed, bytesTransferred, logText, reportToServer: false)
            .GetAwaiter().GetResult();
    }

    public async Task<bool> CompleteRunAsync(
        long runId,
        SyncRunStatus status,
        long filesProcessed,
        long bytesTransferred,
        string logText,
        bool reportToServer = true)
    {
        var completedAt = DateTimeOffset.UtcNow;
        SyncRun report;
        using (var connection = OpenConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                                      UPDATE SyncRuns
                                      SET CompletedAtUtc = $completedAtUtc,
                                          Status = $status,
                                          FilesProcessed = $filesProcessed,
                                          BytesTransferred = $bytesTransferred,
                                          ErrorCount = (SELECT COUNT(*) FROM SyncErrors WHERE RunId = $runId),
                                          LogText = $logText
                                      WHERE Id = $runId;
                                      """;
                command.Parameters.AddWithValue("$completedAtUtc", completedAt.ToString("O"));
                command.Parameters.AddWithValue("$status", status.ToString());
                command.Parameters.AddWithValue("$filesProcessed", filesProcessed);
                command.Parameters.AddWithValue("$bytesTransferred", bytesTransferred);
                command.Parameters.AddWithValue("$logText", logText);
                command.Parameters.AddWithValue("$runId", runId);
                command.ExecuteNonQuery();
            }

            report = BuildReport(connection, runId, status, completedAt, filesProcessed, bytesTransferred);
            TrimRuns(connection);
        }

        if (!reportToServer || report.Id.Length == 0 || !ServerReporter.IsConfigured)
        {
            return true;
        }

        var reported = await ServerReporter.ReportRunAsync(report);
        var reportLine = reported
            ? $"{DateTime.Now:HH:mm:ss} INFO  Server report accepted"
            : $"{DateTime.Now:HH:mm:ss} ERROR Server report failed; the configured server did not accept the run";
        var finalStatus = reported ? status : SyncRunStatus.Failed;
        if (!reported)
        {
            AddError(new SyncErrorRecord(
                runId,
                string.Empty,
                "Configured server did not accept the sync report.",
                DateTimeOffset.UtcNow));
        }

        using var reportConnection = OpenConnection();
        using var reportCommand = reportConnection.CreateCommand();
        reportCommand.CommandText = """
                              UPDATE SyncRuns
                              SET Status = $status,
                                  ErrorCount = (SELECT COUNT(*) FROM SyncErrors WHERE RunId = $runId),
                                  LogText = $logText
                              WHERE Id = $runId;
                              """;
        reportCommand.Parameters.AddWithValue("$status", finalStatus.ToString());
        reportCommand.Parameters.AddWithValue("$logText", $"{logText}\n{reportLine}");
        reportCommand.Parameters.AddWithValue("$runId", runId);
        reportCommand.ExecuteNonQuery();
        return reported;
    }

    private static SyncRun BuildReport(
        SqliteConnection connection,
        long runId,
        SyncRunStatus status,
        DateTimeOffset completedAt,
        long filesProcessed,
        long bytesTransferred)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT RemoteId, StartedAtUtc,
                                     (SELECT Message FROM SyncErrors WHERE RunId = $runId ORDER BY Id LIMIT 1)
                              FROM SyncRuns WHERE Id = $runId;
                              """;
        command.Parameters.AddWithValue("$runId", runId);
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
        {
            // Rows created before server reporting existed have no remote id.
            return new SyncRun();
        }

        var startedAt = DateTimeOffset.Parse(reader.GetString(1));
        return new SyncRun
        {
            Id = reader.GetString(0),
            Status = status switch
            {
                SyncRunStatus.Completed => ContractStatus.Completed,
                SyncRunStatus.Failed => ContractStatus.Failed,
                SyncRunStatus.Cancelled => ContractStatus.Cancelled,
                _ => ContractStatus.Running
            },
            StartedAt = Timestamp.FromDateTimeOffset(startedAt),
            Duration = Duration.FromTimeSpan(completedAt - startedAt),
            FilesChanged = filesProcessed,
            BytesTransferred = bytesTransferred,
            ErrorMessage = status == SyncRunStatus.Failed && !reader.IsDBNull(2) ? reader.GetString(2) : string.Empty
        };
    }

    public void AddItem(SyncItemRecord item)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO SyncItems (RunId, RelativePath, Size, Status, ErrorMessage)
                              VALUES ($runId, $relativePath, $size, $status, $errorMessage);
                              """;
        command.Parameters.AddWithValue("$runId", item.RunId);
        command.Parameters.AddWithValue("$relativePath", item.RelativePath);
        command.Parameters.AddWithValue("$size", item.Size);
        command.Parameters.AddWithValue("$status", item.Status);
        command.Parameters.AddWithValue("$errorMessage", (object?)item.ErrorMessage ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void AddError(SyncErrorRecord error)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO SyncErrors (RunId, RelativePath, Message, CreatedAtUtc)
                              VALUES ($runId, $relativePath, $message, $createdAtUtc);
                              """;
        command.Parameters.AddWithValue("$runId", error.RunId);
        command.Parameters.AddWithValue("$relativePath", error.RelativePath);
        command.Parameters.AddWithValue("$message", error.Message);
        command.Parameters.AddWithValue("$createdAtUtc", error.CreatedAtUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<SyncRunRecord> GetRecentRuns(int limit = 10)
    {
        limit = Math.Clamp(limit, 1, RetainedRunCount);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT Id, StartedAtUtc, CompletedAtUtc, Status, SourcePath, DestinationPath,
                                     FilesProcessed, BytesTransferred, ErrorCount, LogText
                              FROM SyncRuns
                              ORDER BY StartedAtUtc DESC
                              LIMIT $limit;
                              """;
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var runs = new List<SyncRunRecord>();
        while (reader.Read())
        {
            runs.Add(new SyncRunRecord(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1)),
                reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)),
                Enum.Parse<SyncRunStatus>(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt64(6),
                reader.GetInt64(7),
                reader.GetInt32(8),
                reader.GetString(9)));
        }

        return runs;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        command.ExecuteNonQuery();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS SyncRuns (
                                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  StartedAtUtc TEXT NOT NULL,
                                  CompletedAtUtc TEXT,
                                  Status TEXT NOT NULL,
                                  SourcePath TEXT NOT NULL,
                                  DestinationPath TEXT NOT NULL,
                                  FilesProcessed INTEGER NOT NULL DEFAULT 0,
                                  BytesTransferred INTEGER NOT NULL DEFAULT 0,
                                  ErrorCount INTEGER NOT NULL DEFAULT 0,
                                  LogText TEXT NOT NULL DEFAULT ''
                              );

                              CREATE TABLE IF NOT EXISTS SyncItems (
                                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  RunId INTEGER NOT NULL REFERENCES SyncRuns(Id) ON DELETE CASCADE,
                                  RelativePath TEXT NOT NULL,
                                  Size INTEGER NOT NULL,
                                  Status TEXT NOT NULL,
                                  ErrorMessage TEXT
                              );

                              CREATE TABLE IF NOT EXISTS SyncErrors (
                                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  RunId INTEGER NOT NULL REFERENCES SyncRuns(Id) ON DELETE CASCADE,
                                  RelativePath TEXT NOT NULL,
                                  Message TEXT NOT NULL,
                                  CreatedAtUtc TEXT NOT NULL
                              );

                              CREATE INDEX IF NOT EXISTS IX_SyncRuns_StartedAtUtc ON SyncRuns(StartedAtUtc DESC);
                              CREATE INDEX IF NOT EXISTS IX_SyncItems_RunId ON SyncItems(RunId);
                              CREATE INDEX IF NOT EXISTS IX_SyncErrors_RunId ON SyncErrors(RunId);
                              """;
        command.ExecuteNonQuery();

        // Migration: id used to identify the run on the server.
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SyncRuns') WHERE name = 'RemoteId';";
        if ((long)(command.ExecuteScalar() ?? 0L) == 0)
        {
            command.CommandText = "ALTER TABLE SyncRuns ADD COLUMN RemoteId TEXT;";
            command.ExecuteNonQuery();
        }
    }

    private static void TrimRuns(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              DELETE FROM SyncRuns
                              WHERE Id NOT IN (
                                  SELECT Id FROM SyncRuns ORDER BY StartedAtUtc DESC LIMIT $retainedRunCount
                              );
                              """;
        command.Parameters.AddWithValue("$retainedRunCount", RetainedRunCount);
        command.ExecuteNonQuery();
    }
}
