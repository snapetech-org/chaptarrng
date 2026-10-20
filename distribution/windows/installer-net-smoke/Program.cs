using System.IO;
using ChaptarrNG.Installer.Pipeline;

if (args.Contains("--windows-service-round-trip", StringComparer.OrdinalIgnoreCase))
{
    return await WindowsServiceRoundTrip.RunAsync();
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

    Console.WriteLine("PASS: database, WAL, and journal changes are detected; transient -shm changes are ignored.");
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
