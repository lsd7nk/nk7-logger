using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using ZLogger;
using System;

namespace Nk7.Logger
{
    internal sealed class LogOutputProcessor : IAsyncLogProcessor
    {
        private readonly UnityDebugLogProcessor _console;
        private readonly LogLevel _consoleMinimumLevel;
        private readonly BackgroundLogWriter _file;
        private readonly LogLevel _fileMinimumLevel;

        public LogOutputProcessor(UnityDebugLogProcessor console, LogLevel consoleMinimumLevel,
            BackgroundLogWriter file, LogLevel fileMinimumLevel)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
            _consoleMinimumLevel = consoleMinimumLevel;
            _file = file;
            _fileMinimumLevel = fileMinimumLevel;
        }

        public void Post(IZLoggerEntry log)
        {
            var level = log.LogInfo.LogLevel;
            var posted = false;

            try
            {
                if (level == LogLevel.None)
                {
                    return;
                }

                if (level >= _consoleMinimumLevel)
                {
                    _console.Write(log);
                }

                posted = _file != null && level >= _fileMinimumLevel && _file.TryPost(log);
            }
            finally
            {
                if (!posted)
                {
                    log.Return();
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            _file?.Dispose();

            return default;
        }
    }
}
