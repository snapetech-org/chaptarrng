using System.IO;
using System.Security.Cryptography;
using System.Text;
using ChaptarrNG.Installer.Pipeline;
using Microsoft.Data.Sqlite;

if (RunBookshelfAcceptanceCommand(args) is int acceptanceResult)
{
    return acceptanceResult;
}

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
    AssertServiceAccountPolicy();
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

void AssertServiceAccountPolicy()
{
    if (ServiceAccountPolicy.RequiresPassword("NT AUTHORITY\\LocalService") ||
        ServiceAccountPolicy.RequiresPassword("NetworkService") ||
        ServiceAccountPolicy.RequiresPassword("DOMAIN\\BookshelfGmsa$") ||
        !ServiceAccountPolicy.RequiresPassword("DOMAIN\\bookshelf-service") ||
        !ServiceAccountPolicy.AreEquivalent("NetworkService", "NT AUTHORITY\\NetworkService") ||
        ServiceAccountPolicy.GetUnsupportedReason("NT SERVICE\\Readarr") is null ||
        ServiceAccountPolicy.GetUnsupportedReason("DOMAIN\\bookshelf-service") is not null)
    {
        throw new InvalidOperationException("The BookshelfNG Windows service-account policy does not preserve supported account types safely.");
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

int? RunBookshelfAcceptanceCommand(string[] arguments)
{
    var commands = new[]
    {
        "--seed-bookshelfng",
        "--bookshelf-data-canary",
        "--verify-bookshelfng-migration"
    };
    var command = commands.FirstOrDefault(arguments.Contains);
    if (command is null)
    {
        return null;
    }

    var index = Array.IndexOf(arguments, command);
    if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
    {
        Console.Error.WriteLine($"FAIL: {command} requires an app-data directory.");
        return 2;
    }

    try
    {
        var targetDirectory = Path.GetFullPath(arguments[index + 1]);
        switch (command)
        {
            case "--seed-bookshelfng":
                SeedBookshelfDatabase(targetDirectory);
                Console.WriteLine("PASS: seeded the BookshelfNG migration-158 fixture with author, book, edition, file, and setting canaries.");
                break;
            case "--bookshelf-data-canary":
            {
                var candidateDatabase = Path.Combine(targetDirectory, "chaptarr.db");
                var sourceDatabase = Path.Combine(targetDirectory, "readarr.db");
                Console.WriteLine(GetBookshelfDataCanary(File.Exists(candidateDatabase) ? candidateDatabase : sourceDatabase));
                break;
            }
            case "--verify-bookshelfng-migration":
                VerifyBookshelfMigration(targetDirectory);
                Console.WriteLine("PASS: converted BookshelfNG fixture records, SQLite integrity, foreign keys, and retained source backup verified.");
                break;
        }

        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL: {exception.Message}");
        return 1;
    }
}

void SeedBookshelfDatabase(string targetDirectory)
{
    if (!Directory.Exists(targetDirectory))
    {
        throw new DirectoryNotFoundException($"BookshelfNG app-data directory does not exist: {targetDirectory}");
    }

    foreach (var name in new[] { "readarr.db", "audioarr.db" })
    {
        var existingPath = Path.Combine(targetDirectory, name);
        if (!File.Exists(existingPath))
        {
            continue;
        }

        using var existing = new SqliteConnection(CreateConnectionString(existingPath, SqliteOpenMode.ReadOnly));
        existing.Open();
        if (CountRowsIfTableExists(existing, "Authors") != 0 ||
            CountRowsIfTableExists(existing, "Books") != 0 ||
            CountRowsIfTableExists(existing, "BookFiles") != 0)
        {
            throw new InvalidOperationException($"Refusing to replace populated BookshelfNG database {existingPath}.");
        }

        existing.Close();
        var retainedPath = existingPath + ".pre-migration-acceptance-" + Guid.NewGuid().ToString("N") + ".db";
        File.Move(existingPath, retainedPath);
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            var sidecar = existingPath + suffix;
            if (File.Exists(sidecar))
            {
                File.Move(sidecar, retainedPath + suffix);
            }
        }
    }

    var fixtureDirectory = Path.Combine(targetDirectory, "migration-acceptance-library");
    Directory.CreateDirectory(Path.Combine(fixtureDirectory, "Fixture Author"));
    File.WriteAllText(Path.Combine(fixtureDirectory, "Fixture Author", "book.m4b"), "BookshelfNG migration fixture audio");
    File.WriteAllText(Path.Combine(fixtureDirectory, "Fixture Author", "book.epub"), "BookshelfNG migration fixture ebook");
    var portableLibraryPath = fixtureDirectory.Replace('\\', '/');

    var databasePath = Path.Combine(targetDirectory, "readarr.db");
    using var connection = new SqliteConnection(CreateConnectionString(databasePath, SqliteOpenMode.ReadWriteCreate));
    connection.Open();
    ExecuteFixture(connection, "Fixtures/BookshelfNG.Schema.158.sql", portableLibraryPath);
    ExecuteFixture(connection, "Fixtures/BookshelfNG.PopulatedMigration158.sql", portableLibraryPath);

    if (GetBookshelfDataCanary(databasePath) != GetExpectedFixtureCanary(portableLibraryPath))
    {
        throw new InvalidOperationException("The seeded BookshelfNG fixture did not retain its expected canary rows.");
    }
}

void VerifyBookshelfMigration(string targetDirectory)
{
    var targetPath = Path.Combine(targetDirectory, "chaptarr.db");
    if (!File.Exists(targetPath))
    {
        throw new FileNotFoundException("The ChaptarrNG target database was not created.", targetPath);
    }

    var expected = GetExpectedFixtureCanary(Path.Combine(targetDirectory, "migration-acceptance-library").Replace('\\', '/'));
    var actual = GetBookshelfDataCanary(targetPath);
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The migrated ChaptarrNG database did not preserve the seeded BookshelfNG author, book, edition, file, and setting canaries.");
    }

    using (var connection = new SqliteConnection(CreateConnectionString(targetPath, SqliteOpenMode.ReadOnly)))
    {
        connection.Open();
        if (QueryScalar<string>(connection, "PRAGMA integrity_check;") != "ok")
        {
            throw new InvalidDataException("The converted ChaptarrNG database failed SQLite integrity_check.");
        }

        if (QueryScalar<long>(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;") != 0)
        {
            throw new InvalidDataException("The converted ChaptarrNG database contains foreign-key violations.");
        }
    }

    var backups = Directory.GetFiles(targetDirectory, "readarr.db.bookshelfng-backup-*.db");
    if (backups.Length == 0)
    {
        throw new FileNotFoundException("No retained BookshelfNG source database backup was found.", targetDirectory);
    }

    using var backup = new SqliteConnection(CreateConnectionString(backups[0], SqliteOpenMode.ReadOnly));
    backup.Open();
    if (QueryScalar<string>(backup, "SELECT Title FROM Books WHERE Id = 30;") != "Fixture Book")
    {
        throw new InvalidDataException("The retained BookshelfNG source backup did not preserve its fixture book.");
    }
}

string GetBookshelfDataCanary(string databasePath)
{
    if (!File.Exists(databasePath))
    {
        throw new FileNotFoundException("The BookshelfNG migration source database was not found.", databasePath);
    }

    using var connection = new SqliteConnection(CreateConnectionString(databasePath, SqliteOpenMode.ReadOnly));
    connection.Open();
    var values = new[]
    {
        QueryScalar<string>(connection, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"),
        QueryScalar<string>(connection, "SELECT Name FROM AuthorMetadata WHERE Id = 10;"),
        QueryScalar<string>(connection, "SELECT Path FROM Authors WHERE Id = 20;"),
        QueryScalar<string>(connection, "SELECT EbookPath FROM Authors WHERE Id = 20;"),
        QueryScalar<string>(connection, "SELECT AudiobookPath FROM Authors WHERE Id = 20;"),
        QueryScalar<string>(connection, "SELECT Title FROM Books WHERE Id = 30;"),
        QueryScalar<string>(connection, "SELECT Title FROM Editions WHERE Id = 40;"),
        QueryScalar<string>(connection, "SELECT Title FROM Editions WHERE Id = 41;"),
        QueryScalar<string>(connection, "SELECT Path FROM BookFiles WHERE Id = 50;"),
        QueryScalar<string>(connection, "SELECT Path FROM BookFiles WHERE Id = 51;")
    };

    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values)))).ToLowerInvariant();
}

string GetExpectedFixtureCanary(string portableLibraryPath)
{
    var values = new[]
    {
        "preserved",
        "Fixture Author",
        portableLibraryPath + "/Fixture Author",
        portableLibraryPath + "/Fixture Author/Ebooks",
        portableLibraryPath + "/Fixture Author/Audiobooks",
        "Fixture Book",
        "Fixture Audiobook",
        "Fixture Ebook",
        portableLibraryPath + "/Fixture Author/book.m4b",
        portableLibraryPath + "/Fixture Author/book.epub"
    };

    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values)))).ToLowerInvariant();
}

void ExecuteFixture(SqliteConnection connection, string relativePath, string portableLibraryPath)
{
    var fixturePath = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    var sql = File.ReadAllText(fixturePath).Replace("/library", portableLibraryPath, StringComparison.Ordinal);
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.ExecuteNonQuery();
}

static long CountRowsIfTableExists(SqliteConnection connection, string tableName)
{
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
    command.Parameters.AddWithValue("$table", tableName);
    if (Convert.ToInt64(command.ExecuteScalar()) == 0)
    {
        return 0;
    }

    command.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\";";
    return Convert.ToInt64(command.ExecuteScalar());
}

static string CreateConnectionString(string databasePath, SqliteOpenMode mode)
{
    return new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = mode,
        Pooling = false
    }.ConnectionString;
}

static T QueryScalar<T>(SqliteConnection connection, string sql)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    var value = command.ExecuteScalar();
    if (value is null or DBNull)
    {
        throw new InvalidDataException($"A migration canary query returned no value: {sql}");
    }

    return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
}
