using Microsoft.Extensions.Logging;
using System.Collections.Generic;
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
        private static readonly Dictionary<ILogger, IDisposable[]> _opened = new();
        private static readonly object _sync = new();

#if UNITY_WEBGL && !UNITY_EDITOR
        internal static bool OwnFileWriter = false;
#else
        internal static bool OwnFileWriter = true;
#endif

        static LoggerFactory()
        {
            _unityDebugProcessor = new UnityDebugLogProcessor(GetUnityDebugFormatter, true);
        }

        public static ILogger GetLogger(LogLevel minimumLogLevel = LogLevel.Error, string loggerFilePath = null)
        {
            if (!OwnFileWriter)
            {
                var factory = GetFactory(loggerFilePath, minimumLogLevel);

                return Track(factory.CreateLogger("Application"), factory);
            }

            var level = minimumLogLevel != LogLevel.None
                ? minimumLogLevel
                : LogLevel.Warning;

            if (string.IsNullOrEmpty(loggerFilePath))
            {
                return Create(level, level, null, null);
            }

            PrepareFile(loggerFilePath);

            var stream = new FileStream(loggerFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            var writer = new BackgroundLogWriter(stream, new PlainTextZLoggerFormatter());

            return Create(level, level, writer, stream);
        }

        public static ILogger GetSessionLogger(LogLevel consoleMinimumLevel, SessionLogOptions sessionLog)
        {
            if (sessionLog == null)
            {
                throw new ArgumentNullException(nameof(sessionLog));
            }

            var path = SessionLogFiles.StartSession(sessionLog.Directory, sessionLog.MaxSessions, DateTime.UtcNow);
            var stream = new CappedFileStream(path, sessionLog.MaxSessionBytes);

            if (OwnFileWriter)
            {
                var writer = new BackgroundLogWriter(stream, GetSessionFormatter());

                return Create(consoleMinimumLevel, sessionLog.MinimumLevel, writer, stream);
            }

            var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(consoleMinimumLevel < sessionLog.MinimumLevel
                    ? consoleMinimumLevel
                    : sessionLog.MinimumLevel);

                builder.AddFilter<ZLoggerLogProcessorLoggerProvider>(null, consoleMinimumLevel);
                builder.AddFilter<ZLoggerStreamLoggerProvider>(null, sessionLog.MinimumLevel);

                builder.AddZLoggerLogProcessor(_unityDebugProcessor);
                builder.AddZLoggerStream(stream, SetSessionFileOptions);
            });

            return Track(factory.CreateLogger("Application"), factory, stream);
        }

        public static void Close(ILogger logger)
        {
            if (logger == null)
            {
                return;
            }

            IDisposable[] resources;

            lock (_sync)
            {
                if (!_opened.TryGetValue(logger, out resources))
                {
                    return;
                }

                _opened.Remove(logger);
            }

            Dispose(resources);
        }

        public static void Shutdown()
        {
            var opened = new List<IDisposable[]>();

            lock (_sync)
            {
                opened.AddRange(_opened.Values);
                _opened.Clear();
            }

            for (var i = 0; i < opened.Count; i++)
            {
                Dispose(opened[i]);
            }
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
                    PrepareFile(loggerFilePath);

                    builder.AddZLoggerFile(loggerFilePath, SetZLoggerFileOptions);
                }
            });
        }

        private static ILogger Create(LogLevel consoleMinimumLevel, LogLevel fileMinimumLevel, BackgroundLogWriter writer, Stream stream)
        {
            var processor = new LogOutputProcessor(_unityDebugProcessor, consoleMinimumLevel, writer, fileMinimumLevel);

            var minimumLevel = writer != null && fileMinimumLevel < consoleMinimumLevel
                ? fileMinimumLevel
                : consoleMinimumLevel;

            var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimumLevel);
                builder.AddZLoggerLogProcessor(processor);
            });

            return Track(factory.CreateLogger("Application"), factory, writer, stream);
        }

        private static void PrepareFile(string loggerFilePath)
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
        }

        private static ILogger Track(ILogger logger, params IDisposable[] resources)
        {
            lock (_sync)
            {
                _opened[logger] = resources;
            }

            return logger;
        }

        private static void Dispose(IDisposable[] resources)
        {
            for (var i = 0; i < resources.Length; i++)
            {
                try
                {
                    resources[i]?.Dispose();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CloseOnQuit()
        {
            Shutdown();

            UnityEngine.Application.quitting -= Shutdown;
            UnityEngine.Application.quitting += Shutdown;
        }

        private static void SetZLoggerFileOptions(ZLoggerOptions options)
        {
            options.UsePlainTextFormatter();
        }

        private static void SetSessionFileOptions(ZLoggerOptions options)
        {
            options.UsePlainTextFormatter(SetSessionPrefix);
        }

        private static IZLoggerFormatter GetSessionFormatter()
        {
            var formatter = new PlainTextZLoggerFormatter();

            SetSessionPrefix(formatter);

            return formatter;
        }

        private static void SetSessionPrefix(PlainTextZLoggerFormatter formatter)
        {
            formatter.SetPrefixFormatter($"{0:utc-longdate} [{1:short}] ",
                (in MessageTemplate template, in LogInfo info) => template.Format(info.Timestamp, info.LogLevel));
        }

        private static IZLoggerFormatter GetUnityDebugFormatter()
        {
            return new PlainTextZLoggerFormatter();
        }
    }
}