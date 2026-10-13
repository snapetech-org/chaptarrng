using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace NzbDrone.Core.Datastore
{
    /// <summary>
    /// Stages a BookshelfNG SQLite conversion in a new native Chaptarr database.
    /// The caller must account for every row in every source table before the
    /// staged database can replace the source.
    /// </summary>
    public sealed class BookshelfDatabaseConverter
    {
        public string Convert(
            string sourceConnectionString,
            Action<string> migrateNativeTarget,
            Func<SqliteConnection, SqliteConnection, SqliteTransaction, BookshelfDatabaseImportReport> importRows)
        {
            if (migrateNativeTarget == null)
            {
                throw new ArgumentNullException(nameof(migrateNativeTarget));
            }

            if (importRows == null)
            {
                throw new ArgumentNullException(nameof(importRows));
            }

            var sourceBuilder = new SqliteConnectionStringBuilder(sourceConnectionString);
            if (sourceBuilder.Mode == SqliteOpenMode.Memory ||
                string.IsNullOrWhiteSpace(sourceBuilder.DataSource) ||
                string.Equals(sourceBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("BookshelfNG conversion requires a file-backed SQLite database.");
            }

            var sourcePath = Path.GetFullPath(sourceBuilder.DataSource);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("The BookshelfNG database does not exist.", sourcePath);
            }

            if (DatabaseLineageGuard.Detect(sourceConnectionString, DatabaseType.SQLite) != DatabaseLineage.BookshelfNG)
            {
                throw new InvalidDataException("The database does not match the supported BookshelfNG SQLite schema.");
            }

            var directory = Path.GetDirectoryName(sourcePath);
            var suffix = Guid.NewGuid().ToString("N");
            var stagePath = sourcePath + ".chaptarr-stage-" + suffix + ".db";
            var snapshotPath = sourcePath + ".bookshelf-snapshot-" + suffix + ".db";
            var backupPath = sourcePath + ".bookshelfng-backup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + suffix + ".db";
            var lockPath = sourcePath + ".bookshelfng-conversion.lock";
            var promoted = false;
            var backupRetained = false;

            using var conversionLock = AcquireConversionLock(lockPath);

            try
            {
                CreateConsistentSnapshot(sourcePath, snapshotPath, sourceBuilder);
                ValidateDatabase(snapshotPath, expectedLineage: DatabaseLineage.BookshelfNG, checkForeignKeys: true);

                var stageConnectionString = BuildConnectionString(sourceBuilder, stagePath, SqliteOpenMode.ReadWriteCreate);
                migrateNativeTarget(stageConnectionString);

                using (var source = OpenConnection(snapshotPath, SqliteOpenMode.ReadOnly, sourceBuilder))
                using (var target = OpenConnection(stagePath, SqliteOpenMode.ReadWrite, sourceBuilder))
                {
                    if (DatabaseLineageGuard.Detect(target.ConnectionString, DatabaseType.SQLite) != DatabaseLineage.Chaptarr)
                    {
                        throw new InvalidDataException("The staged conversion target did not migrate to a native Chaptarr schema.");
                    }

                    using (var transaction = target.BeginTransaction())
                    {
                        var sourceTables = GetSourceTableCounts(source);
                        var report = importRows(source, target, transaction);
                        ValidateImportReport(sourceTables, report);
                        transaction.Commit();
                    }
                }

                ValidateDatabase(stagePath, expectedLineage: DatabaseLineage.Chaptarr, checkForeignKeys: true);

                // Retain a consistent SQLite backup before touching the source file or its WAL.
                File.Move(snapshotPath, backupPath);
                backupRetained = true;

                CheckpointDatabase(sourcePath, sourceBuilder);
                CheckpointDatabase(stagePath, sourceBuilder);
                SqliteConnection.ClearAllPools();
                DeleteSidecars(sourcePath);
                DeleteSidecars(stagePath);

                // Both files are in the same directory, so this replacement is a same-filesystem rename.
                File.Move(stagePath, sourcePath, overwrite: true);
                promoted = true;

                return backupPath;
            }
            finally
            {
                SqliteConnection.ClearAllPools();

                if (!promoted)
                {
                    DeleteDatabaseFiles(stagePath);
                }

                if (!backupRetained)
                {
                    DeleteDatabaseFiles(snapshotPath);
                }
            }
        }

        private static FileStream AcquireConversionLock(string path)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException("Another BookshelfNG database conversion is already using this data directory.", exception);
            }
        }

        private static void CreateConsistentSnapshot(string sourcePath, string snapshotPath, SqliteConnectionStringBuilder sourceBuilder)
        {
            using var source = OpenConnection(sourcePath, SqliteOpenMode.ReadOnly, sourceBuilder);
            using var snapshot = OpenConnection(snapshotPath, SqliteOpenMode.ReadWriteCreate, sourceBuilder);
            source.BackupDatabase(snapshot);
        }

        private static Dictionary<string, long> GetSourceTableCounts(SqliteConnection source)
        {
            var counts = new Dictionary<string, long>(StringComparer.Ordinal);
            using var tablesCommand = source.CreateCommand();
            tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var tables = tablesCommand.ExecuteReader();
            var names = new List<string>();
            while (tables.Read())
            {
                names.Add(tables.GetString(0));
            }

            tables.Close();

            foreach (var tableName in names)
            {
                using var countCommand = source.CreateCommand();
                countCommand.CommandText = $"SELECT COUNT(*) FROM \"{tableName.Replace("\"", "\"\"", StringComparison.Ordinal)}\";";
                counts.Add(tableName, System.Convert.ToInt64(countCommand.ExecuteScalar()));
            }

            return counts;
        }

        private static void ValidateImportReport(
            IReadOnlyDictionary<string, long> sourceTables,
            BookshelfDatabaseImportReport report)
        {
            if (report == null)
            {
                throw new InvalidDataException("BookshelfNG import did not return a row-accounting report; the staged database was not promoted.");
            }

            var unreportedTables = sourceTables.Keys.Except(report.Tables.Keys, StringComparer.Ordinal).OrderBy(x => x).ToArray();
            var unknownTables = report.Tables.Keys.Except(sourceTables.Keys, StringComparer.Ordinal).OrderBy(x => x).ToArray();
            if (unreportedTables.Length > 0 || unknownTables.Length > 0)
            {
                var details = new List<string>();
                if (unreportedTables.Length > 0)
                {
                    details.Add("unreported source tables: " + string.Join(", ", unreportedTables));
                }

                if (unknownTables.Length > 0)
                {
                    details.Add("reported tables absent from source: " + string.Join(", ", unknownTables));
                }

                throw new InvalidDataException("BookshelfNG import row accounting is incomplete (" + string.Join("; ", details) + "); the staged database was not promoted.");
            }

            foreach (var sourceTable in sourceTables)
            {
                var disposition = report.Tables[sourceTable.Key];
                if (disposition.SourceRows != sourceTable.Value || disposition.AccountedRows != sourceTable.Value)
                {
                    throw new InvalidDataException(
                        $"BookshelfNG import row accounting mismatch for source table '{sourceTable.Key}' " +
                        $"(reported source rows {disposition.SourceRows}/{sourceTable.Value}; " +
                        $"classified rows {disposition.AccountedRows}/{sourceTable.Value}); the staged database was not promoted.");
                }
            }
        }

        private static void ValidateDatabase(string path, DatabaseLineage expectedLineage, bool checkForeignKeys)
        {
            var connectionString = BuildConnectionString(new SqliteConnectionStringBuilder(), path, SqliteOpenMode.ReadOnly);
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using (var integrity = connection.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = integrity.ExecuteScalar()?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"SQLite integrity check failed for staged database '{Path.GetFileName(path)}'.");
                }
            }

            if (checkForeignKeys)
            {
                using var foreignKeys = connection.CreateCommand();
                foreignKeys.CommandText = "PRAGMA foreign_key_check;";
                using var reader = foreignKeys.ExecuteReader();
                if (reader.Read())
                {
                    throw new InvalidDataException($"SQLite foreign-key check failed for staged database '{Path.GetFileName(path)}'.");
                }
            }

            if (DatabaseLineageGuard.Detect(connectionString, DatabaseType.SQLite) != expectedLineage)
            {
                throw new InvalidDataException($"The database '{Path.GetFileName(path)}' does not have the expected {expectedLineage} lineage.");
            }
        }

        private static void CheckpointDatabase(string path, SqliteConnectionStringBuilder sourceBuilder)
        {
            if (!File.Exists(path))
            {
                return;
            }

            using var connection = OpenConnection(path, SqliteOpenMode.ReadWrite, sourceBuilder);
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var busy = reader.GetInt32(0);
                var frames = reader.GetInt32(1);
                var checkpointed = reader.GetInt32(2);
                if (busy != 0 || (frames >= 0 && checkpointed < frames))
                {
                    throw new IOException($"SQLite WAL checkpoint was incomplete for '{Path.GetFileName(path)}'; promotion was stopped.");
                }
            }
        }

        private static SqliteConnection OpenConnection(string path, SqliteOpenMode mode, SqliteConnectionStringBuilder template)
        {
            var connection = new SqliteConnection(BuildConnectionString(template, path, mode));
            connection.Open();
            return connection;
        }

        private static string BuildConnectionString(SqliteConnectionStringBuilder template, string path, SqliteOpenMode mode)
        {
            var builder = new SqliteConnectionStringBuilder(template.ConnectionString)
            {
                DataSource = path,
                Mode = mode,
                Pooling = false
            };

            return builder.ConnectionString;
        }

        private static void DeleteSidecars(string databasePath)
        {
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                var path = databasePath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void DeleteDatabaseFiles(string databasePath)
        {
            DeleteSidecars(databasePath);
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    /// <summary>
    /// Per-source-table accounting returned by the importer. Every row must be
    /// classified as preserved, transformed, rebuilt from canonical data, or
    /// deliberately reset with an explicit explanation before promotion.
    /// </summary>
    public sealed class BookshelfDatabaseImportReport
    {
        private readonly Dictionary<string, BookshelfDatabaseTableImportResult> _tables = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, BookshelfDatabaseTableImportResult> Tables => _tables;

        public void RecordTable(
            string sourceTable,
            long sourceRows,
            long preservedRows,
            long transformedRows,
            long rebuiltRows,
            long resetRows,
            string targetMapping)
        {
            if (string.IsNullOrWhiteSpace(sourceTable))
            {
                throw new ArgumentException("A source table name is required.", nameof(sourceTable));
            }

            if (sourceRows < 0 || preservedRows < 0 || transformedRows < 0 || rebuiltRows < 0 || resetRows < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceRows), "Source and disposition row counts must be non-negative.");
            }

            var accountedRows = checked(preservedRows + transformedRows + rebuiltRows + resetRows);
            if (accountedRows > sourceRows)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceRows), "Disposition row counts cannot exceed the source row count.");
            }

            if (string.IsNullOrWhiteSpace(targetMapping))
            {
                throw new ArgumentException("Describe how the source table is represented in the Chaptarr target.", nameof(targetMapping));
            }

            if (!_tables.TryAdd(sourceTable, new BookshelfDatabaseTableImportResult(
                    sourceRows,
                    preservedRows,
                    transformedRows,
                    rebuiltRows,
                    resetRows,
                    targetMapping)))
            {
                throw new InvalidOperationException($"Source table '{sourceTable}' was reported more than once.");
            }
        }
    }

    public sealed class BookshelfDatabaseTableImportResult
    {
        public BookshelfDatabaseTableImportResult(
            long sourceRows,
            long preservedRows,
            long transformedRows,
            long rebuiltRows,
            long resetRows,
            string targetMapping)
        {
            SourceRows = sourceRows;
            PreservedRows = preservedRows;
            TransformedRows = transformedRows;
            RebuiltRows = rebuiltRows;
            ResetRows = resetRows;
            TargetMapping = targetMapping;
        }

        public long SourceRows { get; }
        public long PreservedRows { get; }
        public long TransformedRows { get; }
        public long RebuiltRows { get; }
        public long ResetRows { get; }
        public long AccountedRows => PreservedRows + TransformedRows + RebuiltRows + ResetRows;
        public string TargetMapping { get; }
    }
}
