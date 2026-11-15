using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using ZLogger;
using System;

namespace Nk7.Logger
{
    internal sealed class UnityDebugLogProcessor : IAsyncLogProcessor
    {
        private readonly Func<IZLoggerFormatter> _formatterFactory;
        private readonly bool _plainMessages;

        [ThreadStatic] private static Utf8Buffer _buffer;
        [ThreadStatic] private static IZLoggerFormatter _formatter;

        public UnityDebugLogProcessor(Func<IZLoggerFormatter> formatterFactory, bool plainMessages = false)
        {
            _formatterFactory = formatterFactory ?? throw new ArgumentNullException(nameof(formatterFactory));
            _plainMessages = plainMessages;
        }

        public void Post(IZLoggerEntry log)
        {
            try
            {
                Write(log);
            }
            finally
            {
                log.Return();
            }
        }

        public void Write(IZLoggerEntry log)
        {
            var level = log.LogInfo.LogLevel;

            if (TryFormat(log, out var message))
            {
                Write(level, message);
            }
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }

        private bool TryFormat(IZLoggerEntry log, out string message)
        {
            if (_plainMessages && log.LogInfo.Exception == null)
            {
                message = TrimLineBreaks(log.ToString());
                return message.Length > 0;
            }

            var buffer = _buffer ??= new Utf8Buffer();
            var formatter = _formatter ??= _formatterFactory();

            buffer.Clear();
            formatter.FormatLogEntry(buffer, log);

            var bytes = buffer.Array;
            var length = buffer.Count;

            while (length > 0)
            {
                var last = bytes[length - 1];

                if (last != (byte)'\n' && last != (byte)'\r')
                {
                    break;
                }

                --length;
            }

            if (length == 0)
            {
                message = null;
                return false;
            }

            message = Encoding.UTF8.GetString(bytes, 0, length);
            return true;
        }

        private static string TrimLineBreaks(string message)
        {
            if (message == null)
            {
                return string.Empty;
            }

            var length = message.Length;

            while (length > 0 && (message[length - 1] == '\n' || message[length - 1] == '\r'))
            {
                --length;
            }

            return length == message.Length
                ? message
                : message.Substring(0, length);
        }

        private static void Write(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Critical:
                case LogLevel.Error:
                    Debug.LogError(message);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogLevel.None:
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}
