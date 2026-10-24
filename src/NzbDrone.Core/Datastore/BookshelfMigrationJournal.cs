using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Datastore
{
    /// <summary>
    /// Durable handoff state shared by package hooks and the database converters.
    /// Values are tokens or hashes; secrets and connection strings are never stored.
    /// </summary>
    public sealed class BookshelfMigrationJournal
    {
        private static readonly object Sync = new object();
        private readonly string _directory;

        public BookshelfMigrationJournal(string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                throw new ArgumentException("A BookshelfNG handoff data directory is required.", nameof(dataDirectory));
            }

            _directory = Path.Combine(Path.GetFullPath(dataDirectory), ".chaptarrng-bookshelfng-journal");
        }

        public void RecordState(string state)
        {
            if (state is not ("detected" or "preflighted" or "snapshotted" or "converted" or "promoted" or "healthy" or "rolled-back" or "intervention-required"))
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown BookshelfNG migration state.");
            }

            lock (Sync)
            {
                EnsureDirectories();
                WriteAtomic(Path.Combine(_directory, "states", state), DateTime.UtcNow.ToString("O"));
                WriteAtomic(Path.Combine(_directory, "state"), state);
            }
        }

        public void RecordOutcome(string name, string value)
        {
            ValidateToken(name, nameof(name));
            ValidateToken(value, nameof(value));

            lock (Sync)
            {
                EnsureDirectories();
                WriteAtomic(Path.Combine(_directory, "outcomes", name), value);
            }
        }

        public void RecordConverterStage(string state, string artifact)
        {
            var artifactName = Path.GetFileName(artifact ?? string.Empty);
            if (string.IsNullOrWhiteSpace(artifactName))
            {
                artifactName = "none";
            }

            artifactName = new string(artifactName.Select(character =>
                char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_').ToArray());

            RecordOutcome("converter-phase", state);
            RecordOutcome("converter-artifact", artifactName);
            RecordOutcome("converter-" + state + "-artifact", artifactName);
            if (state is "converted" or "promoted")
            {
                RecordState(state);
            }
            else
            {
                RecordMilestone("converter-" + state);
            }
        }

        private void RecordMilestone(string milestone)
        {
            ValidateToken(milestone, nameof(milestone));
            lock (Sync)
            {
                EnsureDirectories();
                WriteAtomic(Path.Combine(_directory, "states", milestone), DateTime.UtcNow.ToString("O"));
            }
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "states"));
            Directory.CreateDirectory(Path.Combine(_directory, "outcomes"));
            if (!OperatingSystem.IsWindows())
            {
                var ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
                File.SetUnixFileMode(_directory, ownerOnly);
                File.SetUnixFileMode(Path.Combine(_directory, "states"), ownerOnly);
                File.SetUnixFileMode(Path.Combine(_directory, "outcomes"), ownerOnly);
            }
        }

        private static void WriteAtomic(string path, string value)
        {
            var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine(value);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void ValidateToken(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(character =>
                    !char.IsLetterOrDigit(character) && character is not ('.' or '_' or '-' or ':')))
            {
                throw new ArgumentException("Journal values must be non-empty tokens.", parameterName);
            }
        }
    }
}
