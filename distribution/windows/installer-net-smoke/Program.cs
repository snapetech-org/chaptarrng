using System.IO;
using ChaptarrNG.Installer.Pipeline;
using Microsoft.Data.Sqlite;

if (args.Contains("--windows-service-round-trip", StringComparer.OrdinalIgnoreCase))
{
    return await WindowsServiceRoundTrip.RunAsync();
}

if (args.Contains("--windows-service-round-trip-worker", StringComparer.OrdinalIgnoreCase))
{
    return await WindowsServiceRoundTrip.RunWorkerAsync();
}

var dataDirectory = Path.Combine(Path.GetTempPath(), $"chaptarr-installer-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(dataDirectory);

try
{
    Write("readarr.db", "database");
    Write("readarr.db-wal", "wal");
    Write("readarr.db-journal", "journal");
    Write("readarr.db-shm", "initial-index");

    var baselineSnapshot = SqliteDatabaseFingerprint.Capture(dataDirectory);
    var baseline = baselineSnapshot.Hash;
    Write("readarr.db-shm", "rebuilt-index");
    var shmSnapshot = SqliteDatabaseFingerprint.Capture(dataDirectory);
    AssertEqual(baseline, shmSnapshot.Hash, "-shm changes must not invalidate preview");
    if (SqliteDatabaseFingerprint.GetChangedFiles(baselineSnapshot, shmSnapshot).Count != 0)
    {
        throw new InvalidOperationException("-shm changes must not be reported as durable database changes.");
    }

    AssertDurableChange("readarr.db", baselineSnapshot);
    AssertDurableChange("readarr.db-wal", baselineSnapshot);
    AssertDurableChange("readarr.db-journal", baselineSnapshot);
    AssertDatabaseVersionGuard();

    Console.WriteLine("PASS: database, WAL, and journal changes are reported; transient -shm changes are ignored; committed changes are detected through SQLite data_version.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    return 1;
}
finally
{
    Directory.Delete(dataDirectory, recursive: true);
}

void AssertDurableChange(string name, SqliteDatabaseFingerprint.Snapshot baseline)
{
    Write(name, $"changed-{name}");
    var changed = SqliteDatabaseFingerprint.Capture(dataDirectory);
    if (string.Equals(baseline.Hash, changed.Hash, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"A change to {name} was not detected.");
    }

    if (!SqliteDatabaseFingerprint.GetChangedFiles(baseline, changed).SequenceEqual(new[] { name }, StringComparer.Ordinal))
    {
        throw new InvalidOperationException($"A change to {name} did not report the changed SQLite component precisely.");
    }

    Write(name, name switch
    {
        "readarr.db" => "database",
        "readarr.db-wal" => "wal",
        _ => "journal"
    });
}

void AssertDatabaseVersionGuard()
{
    var guardDirectory = Path.Combine(dataDirectory, "version-guard");
    Directory.CreateDirectory(guardDirectory);
    var databasePath = Path.Combine(guardDirectory, "readarr.db");
    var connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = false
    }.ConnectionString;

    using (var setup = new SqliteConnection(connectionString))
    {
        setup.Open();
        using var command = setup.CreateCommand();
        command.CommandText = "CREATE TABLE GuardProbe (Value INTEGER NOT NULL); INSERT INTO GuardProbe VALUES (0);";
        command.ExecuteNonQuery();
    }

    using var guard = new SqliteDatabaseVersionGuard(guardDirectory);
    if (guard.HasChanged)
    {
        throw new InvalidOperationException("The SQLite data-version guard changed without a source commit.");
    }

    using (var writer = new SqliteConnection(connectionString))
    {
        writer.Open();
        using var command = writer.CreateCommand();
        command.CommandText = "UPDATE GuardProbe SET Value = 1;";
        command.ExecuteNonQuery();
    }

    if (!guard.HasChanged)
    {
        throw new InvalidOperationException("The SQLite data-version guard did not detect a commit from another connection.");
    }
}

void AssertEqual(string expected, string actual, string message)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(message);
    }
}

void Write(string name, string value)
{
    File.WriteAllText(Path.Combine(dataDirectory, name), value);
}
