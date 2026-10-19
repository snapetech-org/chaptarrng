using ChaptarrNG.Installer.Pipeline;

var dataDirectory = Path.Combine(Path.GetTempPath(), $"chaptarr-installer-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(dataDirectory);

try
{
    Write("readarr.db", "database");
    Write("readarr.db-wal", "wal");
    Write("readarr.db-journal", "journal");
    Write("readarr.db-shm", "initial-index");

    var baseline = SqliteDatabaseFingerprint.Compute(dataDirectory);
    Write("readarr.db-shm", "rebuilt-index");
    AssertEqual(baseline, SqliteDatabaseFingerprint.Compute(dataDirectory), "-shm changes must not invalidate preview");

    AssertDurableChange("readarr.db", baseline);
    AssertDurableChange("readarr.db-wal", baseline);
    AssertDurableChange("readarr.db-journal", baseline);

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

void AssertDurableChange(string name, string baseline)
{
    Write(name, $"changed-{name}");
    var changed = SqliteDatabaseFingerprint.Compute(dataDirectory);
    if (string.Equals(baseline, changed, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"A change to {name} was not detected.");
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
