using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using System;

namespace Nk7.Logger
{
    internal sealed class LocatorLoggerService : ILoggerService
    {
        public bool IsEnabled(LogLevel logLevel) => LoggerLocator.Current.IsEnabled(logLevel);

        public void Trace(string message) => LoggerLocator.Current.Trace(message);

        public void Debug(string message) => LoggerLocator.Current.Debug(message);

        public void Information(string message) => LoggerLocator.Current.Information(message);

        public void Warning(string message) => LoggerLocator.Current.Warning(message);

        public void Error(string message) => LoggerLocator.Current.Error(message);

        public void Critical(string message) => LoggerLocator.Current.Critical(message);

        public void Trace(Exception exception) => LoggerLocator.Current.Trace(exception);

        public void Debug(Exception exception) => LoggerLocator.Current.Debug(exception);

        public void Information(Exception exception) => LoggerLocator.Current.Information(exception);

        public void Warning(Exception exception) => LoggerLocator.Current.Warning(exception);

        public void Error(Exception exception) => LoggerLocator.Current.Error(exception);

        public void Critical(Exception exception) => LoggerLocator.Current.Critical(exception);

        public void Trace(Exception exception, string message) => LoggerLocator.Current.Trace(exception, message);

        public void Debug(Exception exception, string message) => LoggerLocator.Current.Debug(exception, message);

        public void Information(Exception exception, string message) => LoggerLocator.Current.Information(exception, message);

        public void Warning(Exception exception, string message) => LoggerLocator.Current.Warning(exception, message);

        public void Error(Exception exception, string message) => LoggerLocator.Current.Error(exception, message);

        public void Critical(Exception exception, string message) => LoggerLocator.Current.Critical(exception, message);

        public void Trace([InterpolatedStringHandlerArgument("")] ref TraceLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Trace(ref handler);

        public void Debug([InterpolatedStringHandlerArgument("")] ref DebugLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Debug(ref handler);

        public void Information([InterpolatedStringHandlerArgument("")] ref InformationLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Information(ref handler);

        public void Warning([InterpolatedStringHandlerArgument("")] ref WarningLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Warning(ref handler);

        public void Error([InterpolatedStringHandlerArgument("")] ref ErrorLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Error(ref handler);

        public void Critical([InterpolatedStringHandlerArgument("")] ref CriticalLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Critical(ref handler);

        public void Trace(Exception exception, [InterpolatedStringHandlerArgument("")] ref TraceLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Trace(exception, ref handler);

        public void Debug(Exception exception, [InterpolatedStringHandlerArgument("")] ref DebugLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Debug(exception, ref handler);

        public void Information(Exception exception, [InterpolatedStringHandlerArgument("")] ref InformationLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Information(exception, ref handler);

        public void Warning(Exception exception, [InterpolatedStringHandlerArgument("")] ref WarningLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Warning(exception, ref handler);

        public void Error(Exception exception, [InterpolatedStringHandlerArgument("")] ref ErrorLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Error(exception, ref handler);

        public void Critical(Exception exception, [InterpolatedStringHandlerArgument("")] ref CriticalLogInterpolatedStringHandler handler) =>
            LoggerLocator.Current.Critical(exception, ref handler);
    }
}
