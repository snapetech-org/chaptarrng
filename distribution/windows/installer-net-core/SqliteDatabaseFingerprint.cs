using System.Security.Cryptography;
using System.Text;

namespace ChaptarrNG.Installer.Pipeline;

public static class SqliteDatabaseFingerprint
{
    public static string Compute(string dataDirectory)
    {
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // SQLite may rewrite its disposable -shm index while opening a WAL database read-only.
        foreach (var name in new[] { "readarr.db", "readarr.db-journal", "readarr.db-wal" })
        {
            fingerprint.AppendData(Encoding.UTF8.GetBytes(name));
            fingerprint.AppendData(new byte[] { 0 });
            var path = Path.Combine(dataDirectory, name);
            if (!File.Exists(path))
            {
                fingerprint.AppendData(new byte[] { 0 });
                continue;
            }

            using var stream = File.OpenRead(path);
            fingerprint.AppendData(new byte[] { 1 });
            fingerprint.AppendData(SHA256.HashData(stream));
        }

        return Convert.ToHexString(fingerprint.GetHashAndReset());
    }
}
