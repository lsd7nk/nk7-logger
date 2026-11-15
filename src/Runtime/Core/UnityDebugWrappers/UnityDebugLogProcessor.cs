using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Buffers;
using System.Text;
using UnityEngine;
using ZLogger;
using System;

namespace Nk7.Logger
{
    internal sealed class UnityDebugLogProcessor : IAsyncLogProcessor
    {
        private readonly Func<IZLoggerFormatter> _formatterFactory;

        [ThreadStatic] private static Utf8Buffer _buffer;
        [ThreadStatic] private static IZLoggerFormatter _formatter;

        public UnityDebugLogProcessor(Func<IZLoggerFormatter> formatterFactory)
        {
            _formatterFactory = formatterFactory ?? throw new ArgumentNullException(nameof(formatterFactory));
        }

        public void Post(IZLoggerEntry log)
        {
            try
            {
                var level = log.LogInfo.LogLevel;

                if (TryFormat(log, out var message))
                {
                    Write(level, message);
                }
            }
            finally
            {
                log.Return();
            }
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }

        private bool TryFormat(IZLoggerEntry log, out string message)
        {
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

       
        private sealed class Utf8Buffer : IBufferWriter<byte>
        {
            private const int INITIAL_CAPACITY = 1024;

            private byte[] _array = new byte[INITIAL_CAPACITY];
            private int _count;

            public byte[] Array => _array;
            public int Count => _count;

            public void Clear()
            {
                _count = 0;
            }

            public void Advance(int count)
            {
                _count += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return new Memory<byte>(_array, _count, _array.Length - _count);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return new Span<byte>(_array, _count, _array.Length - _count);
            }

            private void EnsureCapacity(int sizeHint)
            {
                if (sizeHint < 1)
                {
                    sizeHint = 1;
                }

                if (_array.Length - _count >= sizeHint)
                {
                    return;
                }

                var required = _count + sizeHint;
                var capacity = _array.Length;

                while (capacity < required)
                {
                    capacity *= 2;
                }

                System.Array.Resize(ref _array, capacity);
            }
        }
    }
}
