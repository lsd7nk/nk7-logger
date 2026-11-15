using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Buffers;
using System.Text;
using ZLogger;
using System;

namespace Nk7.Logger
{
    internal sealed class TextLogState : IZLoggerFormattable, IReferenceCountable
    {
        private const int MAX_POOLED = 1024;
        private const int MIN_BUFFER_CHARS = 64;
        private const int MAX_KEPT_BUFFER_CHARS = 1024;

        public static readonly Func<TextLogState, Exception, string> Formatter = Format;

        private static readonly Stack<TextLogState> _pool = new();

        private char[] _buffer;
        private int _length;
        private int _references;

        public int ParameterCount => 0;
        public bool IsSupportUtf8ParameterKey => false;

        private TextLogState()
        {
        }

        public static TextLogState Rent(ReadOnlySpan<char> text)
        {
            TextLogState state = null;

            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    state = _pool.Pop();
                }
            }

            state ??= new TextLogState();

            if (state._buffer == null || state._buffer.Length < text.Length)
            {
                state._buffer = new char[GetCapacity(text.Length)];
            }

            state._length = text.Length;
            state._references = 1;

            text.CopyTo(state._buffer);

            return state;
        }

        public void Retain()
        {
            Interlocked.Increment(ref _references);
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) != 0)
            {
                return;
            }

            if (_buffer != null && _buffer.Length > MAX_KEPT_BUFFER_CHARS)
            {
                _buffer = null;
            }

            _length = 0;

            lock (_pool)
            {
                if (_pool.Count < MAX_POOLED)
                {
                    _pool.Push(this);
                }
            }
        }

        public IZLoggerEntry CreateEntry(in LogInfo info)
        {
            return ZLoggerEntry<TextLogState>.Create(info, this);
        }

        public override string ToString()
        {
            return new string(_buffer, 0, _length);
        }

        public void ToString(IBufferWriter<byte> writer)
        {
            var chars = new ReadOnlySpan<char>(_buffer, 0, _length);
            var bytes = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(_length));

            writer.Advance(Encoding.UTF8.GetBytes(chars, bytes));
        }

        public string GetOriginalFormat()
        {
            return ToString();
        }

        public void WriteOriginalFormat(IBufferWriter<byte> writer)
        {
            ToString(writer);
        }

        public void WriteJsonParameterKeyValues(Utf8JsonWriter jsonWriter, JsonSerializerOptions jsonSerializerOptions, IKeyNameMutator keyNameMutator = null)
        {
        }

        public ReadOnlySpan<byte> GetParameterKey(int index)
        {
            throw new IndexOutOfRangeException(nameof(index));
        }

        public string GetParameterKeyAsString(int index)
        {
            throw new IndexOutOfRangeException(nameof(index));
        }

        public object GetParameterValue(int index)
        {
            throw new IndexOutOfRangeException(nameof(index));
        }

        public T GetParameterValue<T>(int index)
        {
            throw new IndexOutOfRangeException(nameof(index));
        }

        public Type GetParameterType(int index)
        {
            throw new IndexOutOfRangeException(nameof(index));
        }

        private static string Format(TextLogState state, Exception exception)
        {
            return state.ToString();
        }

        private static int GetCapacity(int length)
        {
            var capacity = MIN_BUFFER_CHARS;

            while (capacity < length)
            {
                capacity *= 2;
            }

            return capacity;
        }
    }
}
