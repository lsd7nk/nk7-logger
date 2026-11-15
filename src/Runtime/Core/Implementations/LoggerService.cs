using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using System;

namespace Nk7.Logger
{
    public sealed class LoggerService : ILoggerService
    {
        private readonly ILogger _logger;

        public LoggerService(ILogger logger)
        {
            _logger = logger;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        public void Trace(string message)
        {
            if (!IsEnabled(LogLevel.Trace))
            {
                return;
            }

            _logger.LogTrace(message);
        }

        public void Debug(string message)
        {
            if (!IsEnabled(LogLevel.Debug))
            {
                return;
            }

            _logger.LogDebug(message);
        }

        public void Information(string message)
        {
            if (!IsEnabled(LogLevel.Information))
            {
                return;
            }

            _logger.LogInformation(message);
        }

        public void Warning(string message)
        {
            if (!IsEnabled(LogLevel.Warning))
            {
                return;
            }

            _logger.LogWarning(message);
        }

        public void Error(string message)
        {
            if (!IsEnabled(LogLevel.Error))
            {
                return;
            }

            _logger.LogError(message);
        }

        public void Critical(string message)
        {
            if (!IsEnabled(LogLevel.Critical))
            {
                return;
            }

            _logger.LogCritical(message);
        }

        public void Trace(Exception exception)
        {
            if (!IsEnabled(LogLevel.Trace))
            {
                return;
            }

            _logger.LogTrace(exception, exception.Message);
        }

        public void Debug(Exception exception)
        {
            if (!IsEnabled(LogLevel.Debug))
            {
                return;
            }

            _logger.LogDebug(exception, exception.Message);
        }

        public void Information(Exception exception)
        {
            if (!IsEnabled(LogLevel.Information))
            {
                return;
            }

            _logger.LogInformation(exception, exception.Message);
        }

        public void Warning(Exception exception)
        {
            if (!IsEnabled(LogLevel.Warning))
            {
                return;
            }

            _logger.LogWarning(exception, exception.Message);
        }

        public void Error(Exception exception)
        {
            if (!IsEnabled(LogLevel.Error))
            {
                return;
            }

            _logger.LogError(exception, exception.Message);
        }

        public void Critical(Exception exception)
        {
            if (!IsEnabled(LogLevel.Critical))
            {
                return;
            }

            _logger.LogCritical(exception, exception.Message);
        }

        public void Trace(Exception exception, string message)
        {
            if (!IsEnabled(LogLevel.Trace))
            {
                return;
            }

            _logger.LogTrace(exception, message);
        }

        public void Debug(Exception exception, string message)
        {
            if (!IsEnabled(LogLevel.Debug))
            {
                return;
            }

            _logger.LogDebug(exception, message);
        }

        public void Information(Exception exception, string message)
        {
            if (!IsEnabled(LogLevel.Information))
            {
                return;
            }

            _logger.LogInformation(exception, message);
        }

        public void Warning(Exception exception, string message)
        {
            if (!IsEnabled(LogLevel.Warning))
            {
                return;
            }

            _logger.LogWarning(exception, message);
        }

        public void Error(Exception exception, string message)
        {
            if (!IsEnabled(LogLevel.Error))
            {
                return;
            }

            _logger.LogError(exception, message);
        }

        public void Critical(Exception exception, string message)
        {
            if (!IsEnabled(LogLevel.Critical))
            {
                return;
            }

            _logger.LogCritical(exception, message);
        }

        public void Trace([InterpolatedStringHandlerArgument("")] ref TraceLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Trace, null, handler.ToStateAndClear());
        }

        public void Debug([InterpolatedStringHandlerArgument("")] ref DebugLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Debug, null, handler.ToStateAndClear());
        }

        public void Information([InterpolatedStringHandlerArgument("")] ref InformationLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Information, null, handler.ToStateAndClear());
        }

        public void Warning([InterpolatedStringHandlerArgument("")] ref WarningLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Warning, null, handler.ToStateAndClear());
        }

        public void Error([InterpolatedStringHandlerArgument("")] ref ErrorLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Error, null, handler.ToStateAndClear());
        }

        public void Critical([InterpolatedStringHandlerArgument("")] ref CriticalLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Critical, null, handler.ToStateAndClear());
        }

        public void Trace(Exception exception, [InterpolatedStringHandlerArgument("")] ref TraceLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Trace, exception, handler.ToStateAndClear());
        }

        public void Debug(Exception exception, [InterpolatedStringHandlerArgument("")] ref DebugLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Debug, exception, handler.ToStateAndClear());
        }

        public void Information(Exception exception, [InterpolatedStringHandlerArgument("")] ref InformationLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Information, exception, handler.ToStateAndClear());
        }

        public void Warning(Exception exception, [InterpolatedStringHandlerArgument("")] ref WarningLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Warning, exception, handler.ToStateAndClear());
        }

        public void Error(Exception exception, [InterpolatedStringHandlerArgument("")] ref ErrorLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Error, exception, handler.ToStateAndClear());
        }

        public void Critical(Exception exception, [InterpolatedStringHandlerArgument("")] ref CriticalLogInterpolatedStringHandler handler)
        {
            if (!handler.Enabled)
            {
                return;
            }

            Log(LogLevel.Critical, exception, handler.ToStateAndClear());
        }

        private void Log(LogLevel level, Exception exception, TextLogState state)
        {
            try
            {
                _logger.Log(level, default, state, exception, TextLogState.Formatter);
            }
            finally
            {
                state.Release();
            }
        }
    }
}
