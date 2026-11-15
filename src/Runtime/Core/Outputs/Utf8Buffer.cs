using System.Buffers;
using System;

namespace Nk7.Logger
{
    internal sealed class Utf8Buffer : IBufferWriter<byte>
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

        public void Write(byte[] bytes)
        {
            EnsureCapacity(bytes.Length);
            System.Buffer.BlockCopy(bytes, 0, _array, _count, bytes.Length);

            _count += bytes.Length;
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
