using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System;

namespace Nk7.Logger
{
    internal sealed class CappedFileStream : Stream
    {
        private readonly FileStream _file;
        private readonly long _capacity;
        private long _written;
        private bool _isFull;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;

        public override long Position
        {
            get => _written;
            set => throw new NotSupportedException();
        }

        public CappedFileStream(string path, long capacity)
        {
            _file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _capacity = capacity;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!Accepts(buffer.Length))
            {
                return;
            }

            _file.Write(buffer);
            _written += buffer.Length;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(new ReadOnlySpan<byte>(buffer, offset, count));
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return default;
        }

        public override void Flush()
        {
            _file.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            _file.Flush();
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _file.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool Accepts(int count)
        {
            if (_isFull)
            {
                return false;
            }

            _isFull = _written + count > _capacity;

            return !_isFull;
        }
    }
}
