using UnityEngine;
using System.Text;
using System.IO;
using System;

namespace Nk7.Logger
{
    internal sealed class UnityDebugStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => default;

        public override long Position { get; set; }

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

        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }

            char prefix = (char)buffer[0];
            int messageOffset = offset + 1;
            int messageLength = count - 1;

            while (messageLength > 0)
            {
                byte last = buffer[messageOffset + messageLength - 1];

                if (last != (byte)'\n' && last != (byte)'\r')
                {
                    break;
                }
                    
                --messageLength;
            }

            if (messageLength <= 0)
            {
                return;
            }

            string message = Encoding.UTF8.GetString(buffer, messageOffset, messageLength);

            switch (prefix)
            {
                case UnityDebugFormatter.INFORMATION_PREFIX:
                case UnityDebugFormatter.DEBUG_PREFIX:
                case UnityDebugFormatter.TRACE_PREFIX:
                    Debug.Log(message);
                    break;
                case UnityDebugFormatter.CRITICAL_PREFIX:
                case UnityDebugFormatter.ERROR_PREFIX:
                    Debug.LogError(message);
                    break;
                case UnityDebugFormatter.WARNING_PREFIX:
                    Debug.LogWarning(message);
                    break;
            }
        }
    }
}
