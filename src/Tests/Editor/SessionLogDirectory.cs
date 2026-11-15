using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using System.IO;
using System;

namespace Nk7.Logger.Tests
{
    internal sealed class SessionLogDirectory : IDisposable
    {
        private const int WAIT_TIMEOUT_MS = 5000;
        private const int POLL_INTERVAL_MS = 20;

        public string Path { get; }

        public SessionLogDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nk7-logger-tests", Guid.NewGuid().ToString("N"));
        }

        public static string Read(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            return reader.ReadToEnd();
        }

        public static string WaitFor(string file, string expected)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(WAIT_TIMEOUT_MS);
            var content = Read(file);

            while (!content.Contains(expected) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(POLL_INTERVAL_MS);
                content = Read(file);
            }

            return content;
        }

        public static long WaitForStableLength(string file)
        {
            const int stablePolls = 10;

            var deadline = DateTime.UtcNow.AddMilliseconds(WAIT_TIMEOUT_MS);
            var length = new FileInfo(file).Length;
            var unchanged = 0;

            while (unchanged < stablePolls && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(POLL_INTERVAL_MS);

                var current = new FileInfo(file).Length;

                unchanged = current == length ? unchanged + 1 : 0;
                length = current;
            }

            return length;
        }

        public IReadOnlyList<string> FileNames()
        {
            return Directory.Exists(Path)
                ? Directory.GetFiles(Path).Select(System.IO.Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToList()
                : new List<string>();
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
