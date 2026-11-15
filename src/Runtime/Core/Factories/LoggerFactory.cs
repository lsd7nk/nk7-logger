using Microsoft.Extensions.Logging;
using ZLogger.Formatters;
using ZLogger.Providers;
using System.IO;
using ZLogger;
using System;

namespace Nk7.Logger
{
    public static class LoggerFactory
    {
        private static readonly UnityDebugLogProcessor _unityDebugProcessor;

        static LoggerFactory()
        {
            _unityDebugProcessor = new UnityDebugLogProcessor(GetUnityDebugFormatter);
        }

        public static ILogger GetLogger(LogLevel minimumLogLevel = LogLevel.Error, string loggerFilePath = null)
        {
            return GetFactory(loggerFilePath, minimumLogLevel)
                .CreateLogger("Application");
        }

        public static ILogger GetSessionLogger(LogLevel consoleMinimumLevel, SessionLogOptions sessionLog)
        {
            if (sessionLog == null)
            {
                throw new ArgumentNullException(nameof(sessionLog));
            }

            var path = SessionLogFiles.StartSession(sessionLog.Directory, sessionLog.MaxSessions, DateTime.UtcNow);
            var stream = new CappedFileStream(path, sessionLog.MaxSessionBytes);

            return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(consoleMinimumLevel < sessionLog.MinimumLevel
                    ? consoleMinimumLevel
                    : sessionLog.MinimumLevel);

                builder.AddFilter<ZLoggerLogProcessorLoggerProvider>(null, consoleMinimumLevel);
                builder.AddFilter<ZLoggerStreamLoggerProvider>(null, sessionLog.MinimumLevel);

                builder.AddZLoggerLogProcessor(_unityDebugProcessor);
                builder.AddZLoggerStream(stream, SetSessionFileOptions);
            })
                .CreateLogger("Application");
        }

        private static ILoggerFactory GetFactory(string loggerFilePath, LogLevel minimunLogLevel = LogLevel.None)
        {
            return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimunLogLevel != LogLevel.None
                    ? minimunLogLevel
                    : LogLevel.Warning);

                builder.AddZLoggerLogProcessor(_unityDebugProcessor);

                if (!string.IsNullOrEmpty(loggerFilePath))
                {
                    string directoryName = Path.GetDirectoryName(loggerFilePath);

                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }

                    if (File.Exists(loggerFilePath))
                    {
                        File.Delete(loggerFilePath);
                    }

                    builder.AddZLoggerFile(loggerFilePath, SetZLoggerFileOptions);
                }
            });
        }

        private static void SetZLoggerFileOptions(ZLoggerOptions options)
        {
            options.UsePlainTextFormatter();
        }

        private static void SetSessionFileOptions(ZLoggerOptions options)
        {
            options.UsePlainTextFormatter(formatter =>
                formatter.SetPrefixFormatter($"{0:utc-longdate} [{1:short}] ",
                    (in MessageTemplate template, in LogInfo info) => template.Format(info.Timestamp, info.LogLevel)));
        }

        private static IZLoggerFormatter GetUnityDebugFormatter()
        {
            return new PlainTextZLoggerFormatter();
        }
    }
}