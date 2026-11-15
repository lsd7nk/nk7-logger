using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System;

namespace Nk7.Logger
{
    public static class SessionLogFiles
    {
        private const string FILE_PREFIX = "session-";
        private const string FILE_EXTENSION = ".log";
        private const string TIMESTAMP_FORMAT = "yyyyMMdd-HHmmss-fffffff";

        public static IReadOnlyList<string> GetSessionFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(directory, FILE_PREFIX + "*" + FILE_EXTENSION)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
        }

        internal static string StartSession(string directory, int maxSessions, DateTime utcNow)
        {
            Directory.CreateDirectory(directory);

            var files = GetSessionFiles(directory);
            var newest = GetNewestTimestamp(files);

            if (newest.HasValue && utcNow <= newest.Value)
            {
                utcNow = newest.Value.AddTicks(1);
            }

            for (var i = maxSessions - 1; i < files.Count; i++)
            {
                File.Delete(files[i]);
            }

            return SessionPath(directory, utcNow);
        }

        private static DateTime? GetNewestTimestamp(IReadOnlyList<string> files)
        {
            for (var i = 0; i < files.Count; i++)
            {
                var name = Path.GetFileNameWithoutExtension(files[i]);

                if (name.Length > FILE_PREFIX.Length && DateTime.TryParseExact(name.Substring(FILE_PREFIX.Length),
                        TIMESTAMP_FORMAT, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
                {
                    return timestamp;
                }
            }

            return null;
        }

        private static string SessionPath(string directory, DateTime utcNow)
        {
            var timestamp = utcNow.ToString(TIMESTAMP_FORMAT, CultureInfo.InvariantCulture);

            return Path.Combine(directory, FILE_PREFIX + timestamp + FILE_EXTENSION);
        }
    }
}
