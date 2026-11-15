using System.Threading;
using System.Text;
using System.IO;
using ZLogger;
using System;

namespace Nk7.Logger
{
    internal sealed class BackgroundLogWriter : IDisposable
    {
        private const int INITIAL_QUEUE_CAPACITY = 256;
        private const string THREAD_NAME = "Nk7.Logger file writer";

        private static readonly byte[] _newLine = Encoding.UTF8.GetBytes(Environment.NewLine);

        private readonly object _gate = new();
        private readonly Utf8Buffer _buffer = new();
        private readonly IZLoggerFormatter _formatter;
        private readonly Stream _stream;
        private readonly Thread _thread;

        private IZLoggerEntry[] _pending = new IZLoggerEntry[INITIAL_QUEUE_CAPACITY];
        private IZLoggerEntry[] _writing = new IZLoggerEntry[INITIAL_QUEUE_CAPACITY];
        private int _pendingCount;
        private bool _closing;
        private bool _failed;

        public BackgroundLogWriter(Stream stream, IZLoggerFormatter formatter)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = THREAD_NAME
            };

            _thread.Start();
        }

        public bool TryPost(IZLoggerEntry entry)
        {
            lock (_gate)
            {
                if (_closing)
                {
                    return false;
                }

                if (_pendingCount == _pending.Length)
                {
                    Array.Resize(ref _pending, _pendingCount * 2);
                }

                _pending[_pendingCount++] = entry;

                if (_pendingCount == 1)
                {
                    Monitor.Pulse(_gate);
                }
            }

            return true;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _closing = true;
                Monitor.PulseAll(_gate);
            }

            if (Thread.CurrentThread != _thread)
            {
                _thread.Join();
            }
        }

        private void Run()
        {
            while (true)
            {
                IZLoggerEntry[] batch;
                int count;
                bool closing;

                lock (_gate)
                {
                    while (_pendingCount == 0 && !_closing)
                    {
                        Monitor.Wait(_gate);
                    }

                    batch = _pending;
                    count = _pendingCount;
                    closing = _closing;

                    _pending = _writing;
                    _pendingCount = 0;
                    _writing = batch;
                }

                if (count > 0)
                {
                    Write(batch, count);
                }
                else if (closing)
                {
                    return;
                }
            }
        }

        private void Write(IZLoggerEntry[] batch, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var entry = batch[i];

                batch[i] = null;

                try
                {
                    _buffer.Clear();
                    _formatter.FormatLogEntry(_buffer, entry);
                    _buffer.Write(_newLine);
                    _stream.Write(_buffer.Array, 0, _buffer.Count);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
                finally
                {
                    entry.Return();
                }
            }

            try
            {
                _stream.Flush();
            }
            catch (Exception exception)
            {
                Report(exception);
            }
        }

        private void Report(Exception exception)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;

            UnityEngine.Debug.LogException(exception);
        }
    }
}
