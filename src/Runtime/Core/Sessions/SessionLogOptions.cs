using Microsoft.Extensions.Logging;
using System;

namespace Nk7.Logger
{
    public sealed class SessionLogOptions
    {
        public const int DEFAULT_MAX_SESSIONS = 5;
        public const long DEFAULT_MAX_SESSION_BYTES = 5 * 1024 * 1024;

        public string Directory { get; }
        public LogLevel MinimumLevel { get; }
        public int MaxSessions { get; }
        public long MaxSessionBytes { get; }

        public SessionLogOptions(string directory, LogLevel minimumLevel,
            int maxSessions = DEFAULT_MAX_SESSIONS, long maxSessionBytes = DEFAULT_MAX_SESSION_BYTES)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Session log directory is required.", nameof(directory));
            }

            if (maxSessions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSessions), maxSessions, "At least one session must be kept.");
            }

            if (maxSessionBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSessionBytes), maxSessionBytes, "A session must hold at least one byte.");
            }

            Directory = directory;
            MinimumLevel = minimumLevel;
            MaxSessions = maxSessions;
            MaxSessionBytes = maxSessionBytes;
        }
    }
}
