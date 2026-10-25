using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ChaptarrNG.Installer.Pipeline;

public sealed class SqliteDatabaseVersionGuard : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly long _initialVersion;

    public SqliteDatabaseVersionGuard(string dataDirectory)
    {
        var databasePath = Path.Combine(dataDirectory, BookshelfDatabaseFiles.ResolveExistingDatabaseName(dataDirectory));
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ConnectionString;

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _initialVersion = ReadVersion();
    }

    public bool HasChanged => ReadVersion() != _initialVersion;

    public void Dispose()
    {
        _connection.Dispose();
    }

    private long ReadVersion()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA data_version;";
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
