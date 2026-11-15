using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using NUnit.Framework;
using System.Threading;
using System.Text;
using System.IO;
using ZLogger;
using System;

namespace Nk7.Logger.Tests
{
    [Category("Logger")]
    [Description("Allocation counts on the platform the test runs on: the Editor in Play Mode or a development player on a device")]
    public sealed class LoggerPlayerAllocationTests
    {
        private const string TAG = "[nk7-logger-allocations] ";
        private const int WARM_UP_CALLS = 200;
        private const int BURST_CALLS = 1000;
        private const int PACED_CALLS = 200;
        private const int PACE_MS = 2;

        private static object _keep;
        private static int _sink;

        [Test]
        [Description("Measures the same calls as the Editor tests and reports the numbers; it checks nothing but that allocations are counted at all")]
        public void AllocationsOnThisPlatform()
        {
            var root = Path.Combine(UnityEngine.Application.temporaryCachePath, "nk7-logger-tests", Guid.NewGuid().ToString("N"));
            var report = new StringBuilder();
            var number = 42;
            var word = "text";
            var probe = new ProbeState();
            var plain = new PlainState();

            ILogger packageLogger = null;
            ILoggerFactory zLoggerFactory = null;
            ILoggerFactory discardingFactory = null;

            Line(report, "platform " + UnityEngine.Application.platform + ", backend " + GetBackend() + ", Unity " + UnityEngine.Application.unityVersion +
                ", development build " + UnityEngine.Debug.isDebugBuild + ", device " + UnityEngine.SystemInfo.deviceModel);
            Line(report, "recorder valid " + Recorder.Get("GC.Alloc").isValid);

            var control = CountAllocations(() => _keep = new object(), BURST_CALLS, 0);
            var idle = CountAllocations(() => _sink++, PACED_CALLS, PACE_MS);

            Line(report, "control: " + BURST_CALLS + " calls of new object() counted as " + control + "; " + PACED_CALLS + " empty calls " + PACE_MS + " ms apart counted as " + idle);

            try
            {
                packageLogger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(Path.Combine(root, "package"), LogLevel.Information));

                var logger = packageLogger;
                var service = new LoggerService(logger);

                Line(report, "package logger, file writer of the package");
                Line(report, "  disabled, " + BURST_CALLS + " calls: facade Debug " + CountAllocations(() => service.Debug($"value {number}"), BURST_CALLS, 0) +
                    ", ILogger.LogDebug " + CountAllocations(() => logger.LogDebug($"value {number}"), BURST_CALLS, 0) +
                    ", ZLogDebug " + CountAllocations(() => logger.ZLogDebug($"value {number}"), BURST_CALLS, 0));
                Line(report, "  enabled, " + PACED_CALLS + " calls " + PACE_MS + " ms apart: facade Information " + CountAllocations(() => service.Information($"value {number}"), PACED_CALLS, PACE_MS) +
                    ", ILogger.LogInformation " + CountAllocations(() => logger.LogInformation($"value {number}"), PACED_CALLS, PACE_MS) +
                    ", ZLogInformation " + CountAllocations(() => logger.ZLogInformation($"value {number}"), PACED_CALLS, PACE_MS));
                Line(report, "  enabled, " + BURST_CALLS + " calls in a row: facade Information " + CountAllocations(() => service.Information($"value {number}"), BURST_CALLS, 0) +
                    ", once more " + CountAllocations(() => service.Information($"value {number}"), BURST_CALLS, 0));

                var zLoggerDirectory = Path.Combine(root, "zlogger");
                var zLoggerPath = Path.Combine(zLoggerDirectory, "zlogger.log");

                Directory.CreateDirectory(zLoggerDirectory);

                zLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddZLoggerFile(zLoggerPath, SetPlainText);
                });

                var zLogger = zLoggerFactory.CreateLogger("Application");

                Line(report, "plain ZLogger logger, file writer of ZLogger");
                Line(report, "  disabled, " + BURST_CALLS + " calls: ZLogDebug " + CountAllocations(() => zLogger.ZLogDebug($"value {number}"), BURST_CALLS, 0));
                Line(report, "  enabled, " + PACED_CALLS + " calls " + PACE_MS + " ms apart: ZLogInformation " + CountAllocations(() => zLogger.ZLogInformation($"value {number}"), PACED_CALLS, PACE_MS) +
                    ", ILogger.LogInformation " + CountAllocations(() => zLogger.LogInformation($"value {number}"), PACED_CALLS, PACE_MS));
                Line(report, "  enabled, " + BURST_CALLS + " calls in a row: ZLogInformation " + CountAllocations(() => zLogger.ZLogInformation($"value {number}"), BURST_CALLS, 0) +
                    ", once more " + CountAllocations(() => zLogger.ZLogInformation($"value {number}"), BURST_CALLS, 0));

                discardingFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddZLoggerLogProcessor(new DiscardingProcessor());
                });

                var discarding = discardingFactory.CreateLogger("Application");
                var discardingService = new LoggerService(discarding);

                Line(report, "output that discards the entry, " + BURST_CALLS + " calls in a row");
                Line(report, "  facade Information " + CountAllocations(() => discardingService.Information($"value {number}"), BURST_CALLS, 0) +
                    ", ZLogInformation no arguments " + CountAllocations(() => discarding.ZLogInformation($"value"), BURST_CALLS, 0) +
                    ", one int " + CountAllocations(() => discarding.ZLogInformation($"value {number}"), BURST_CALLS, 0) +
                    ", one string " + CountAllocations(() => discarding.ZLogInformation($"value {word}"), BURST_CALLS, 0) +
                    ", ILogger.LogInformation plain string " + CountAllocations(() => discarding.LogInformation("value"), BURST_CALLS, 0));

                Line(report, "a struct through a generic parameter, " + BURST_CALLS + " calls");
                Line(report, "  'is interface' true " + CountAllocations(() => _sink += IsInterface(probe), BURST_CALLS, 0) +
                    ", 'is interface' false " + CountAllocations(() => _sink += IsInterface(plain), BURST_CALLS, 0) +
                    ", cast to interface " + CountAllocations(() => _sink += CastToInterface(probe), BURST_CALLS, 0) +
                    ", 'is exact struct type' true " + CountAllocations(() => _sink += IsExactType(probe), BURST_CALLS, 0) +
                    ", 'is exact struct type' false " + CountAllocations(() => _sink += IsExactType(plain), BURST_CALLS, 0));
            }
            finally
            {
                LoggerFactory.Close(packageLogger);
                zLoggerFactory?.Dispose();
                discardingFactory?.Dispose();

                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
                catch (Exception)
                {
                }
            }

            var text = report.ToString();

            Assert.GreaterOrEqual(control, BURST_CALLS, "Allocations are not counted on this platform, so the numbers below mean nothing.\n" + text);
            Assert.Pass(text);
        }

        private static void Line(StringBuilder report, string text)
        {
            report.Append(TAG).Append(text).Append('\n');

            UnityEngine.Debug.Log(TAG + text);
        }

        private static string GetBackend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#elif ENABLE_MONO
            return "Mono";
#else
            return "unknown";
#endif
        }

        private static void SetPlainText(ZLoggerOptions options)
        {
            options.UsePlainTextFormatter();
        }

        private static int CountAllocations(Action call, int calls, int paceMs)
        {
            for (var i = 0; i < WARM_UP_CALLS; i++)
            {
                call();
            }

            Thread.Sleep(50);

            var recorder = Recorder.Get("GC.Alloc");

            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;

            try
            {
                for (var i = 0; i < calls; i++)
                {
                    call();

                    if (paceMs > 0)
                    {
                        Thread.Sleep(paceMs);
                    }
                }
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }

            return recorder.sampleBlockCount;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int IsInterface<TState>(TState state)
        {
            return (state is IProbeInfo) ? 1 : 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CastToInterface<TState>(TState state)
        {
            return ((IProbeInfo)state).Get();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int IsExactType<TState>(TState state)
        {
            return (state is ProbeState) ? 1 : 0;
        }

        private interface IProbeInfo
        {
            int Get();
        }

        private readonly struct ProbeState : IProbeInfo
        {
            public int Get()
            {
                return 1;
            }
        }

        private readonly struct PlainState
        {
        }

        private sealed class DiscardingProcessor : IAsyncLogProcessor
        {
            public void Post(IZLoggerEntry log)
            {
                log.Return();
            }

            public ValueTask DisposeAsync()
            {
                return default;
            }
        }
    }
}
