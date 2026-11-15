using Microsoft.Extensions.Logging;
using ZLogger.Formatters;
using System.IO;
using ZLogger;

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

        private static IZLoggerFormatter GetUnityDebugFormatter()
        {
            return new PlainTextZLoggerFormatter();
        }
    }
}