using Microsoft.Extensions.Logging;
using Nk7.Logger;
using Nk7.Logger.Testing;
using UnityEngine.Profiling;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Threading;
using ZLogger;
using System;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("Allocations on the thread that logs: a disabled level costs nothing, an enabled entry to the file costs nothing at a steady rate")]
    public sealed class LoggerAllocationTests
    {
        private const int WARM_UP_CALLS = 200;
        private const int BURST_CALLS = 1000;
        private const int PACED_CALLS = 200;
        private const int PACE_MS = 2;

        private SessionLogDirectory _directory;
        private ILoggerService _service;
        private ILogger _logger;

        [SetUp]
        public void SetUp()
        {
            _directory = new SessionLogDirectory();
            _logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));
            _service = new LoggerService(_logger);
        }

        [TearDown]
        public void TearDown()
        {
            LoggerFactory.Close(_logger);
            _directory.Dispose();
        }

        [Test]
        [Description("A Debug call below the minimum level allocates nothing through the facade, while ILogger.LogDebug still builds the string")]
        public void DisabledLevelAllocatesNothing()
        {
            var service = _service;
            var logger = _logger;
            var number = 42;

            var facade = CountAllocations(() => service.Debug($"value {number}"), BURST_CALLS, 0);
            var extension = CountAllocations(() => logger.LogDebug($"value {number}"), BURST_CALLS, 0);

            Assert.AreEqual(0, facade, "A disabled level allocated through the facade");
            Assert.Greater(extension, 0, "ILogger.LogDebug with an interpolated string allocated nothing: the comparison measures nothing");

            Assert.Pass($"{BURST_CALLS} disabled Debug calls: facade {facade} allocations, ILogger.LogDebug {extension}");
        }

        [Test]
        [Description("An enabled entry written to the file allocates nothing at a steady rate, and less than ILogger.LogInformation with an interpolated string when logged in a row")]
        public void EnabledFileEntryAllocatesNothingAtASteadyRate()
        {
            var service = _service;
            var logger = _logger;
            var number = 42;

            var facadePaced = CountAllocations(() => service.Information($"value {number}"), PACED_CALLS, PACE_MS);
            var extensionPaced = CountAllocations(() => logger.LogInformation($"value {number}"), PACED_CALLS, PACE_MS);
            var facadeBurst = CountAllocations(() => service.Information($"value {number}"), BURST_CALLS, 0);
            var facadeBurstAgain = CountAllocations(() => service.Information($"value {number}"), BURST_CALLS, 0);
            var extensionBurst = CountAllocations(() => logger.LogInformation($"value {number}"), BURST_CALLS, 0);

            var message = $"enabled Information to the file. {PACED_CALLS} calls {PACE_MS} ms apart: facade {facadePaced} allocations, ILogger.LogInformation {extensionPaced}. " +
                $"{BURST_CALLS} calls in a row: facade {facadeBurst}, facade once more {facadeBurstAgain}, ILogger.LogInformation {extensionBurst}";

            Assert.IsTrue(facadePaced == 0, "The facade allocated at a steady rate. " + message);
            Assert.IsTrue(facadeBurstAgain < extensionBurst, "The facade allocated as much as ILogger.LogInformation for calls in a row. " + message);

            Assert.Pass(message);
        }

        [Test]
        [Description("With an output that only takes the entry and gives it back, an enabled entry allocates nothing: the facade and ZLogger are free up to the output")]
        public void EnabledEntryAllocatesNothingBeforeTheOutput()
        {
            using var factory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddZLoggerLogProcessor(new DiscardingProcessor());
            });

            var service = new LoggerService(factory.CreateLogger("Application"));
            var number = 42;

            var paced = CountAllocations(() => service.Information($"value {number}"), PACED_CALLS, PACE_MS);
            var burst = CountAllocations(() => service.Information($"value {number}"), BURST_CALLS, 0);
            var message = $"enabled Information to an output that discards the entry. {PACED_CALLS} calls {PACE_MS} ms apart: {paced} allocations. {BURST_CALLS} calls in a row: {burst}";

            Assert.IsTrue(paced == 0 && burst == 0, message);
            Assert.Pass(message);
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
