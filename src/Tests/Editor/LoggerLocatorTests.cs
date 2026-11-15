using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Nk7.Logger;
using Nk7.Logger.Testing;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("LoggerLocator: packages write to the Unity console until the app plugs in its logger, and to the app logger after that")]
    public sealed class LoggerLocatorTests
    {
        private SessionLogDirectory _directory;
        private ILoggerService _appLogger;

        [SetUp]
        public void SetUp()
        {
            _appLogger = LoggerLocator.Target;
            _directory = new SessionLogDirectory();
            LoggerLocator.Initialize(null);
        }

        [TearDown]
        public void TearDown()
        {
            LoggerLocator.Initialize(_appLogger);
            _directory.Dispose();
        }

        [Test]
        [Description("Without an app logger package entries go to the Unity console: Trace as Log, Warning as Warning, Error as Error")]
        public void WithoutAnAppLoggerEntriesGoToTheUnityConsole()
        {
            LogAssert.Expect(LogType.Log, new Regex("console trace"));
            LogAssert.Expect(LogType.Warning, new Regex("console warning"));
            LogAssert.Expect(LogType.Error, new Regex("console error"));

            LoggerLocator.Instance.Trace("console trace");
            LoggerLocator.Instance.Warning("console warning");
            LoggerLocator.Instance.Error("console error");
        }

        [Test]
        [Description("After Initialize package entries go to the app logger, into the current session file")]
        public void AppLoggerReceivesEntriesOnceInitialized()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            LoggerLocator.Initialize(new LoggerService(logger));
            LoggerLocator.Instance.Information("app entry");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();

            StringAssert.Contains("app entry", SessionLogDirectory.WaitFor(file, "app entry"), "The package entry did not reach the app session file");
        }

        [Test]
        [Description("A package that kept Instance in a field before Initialize writes to the app logger after it")]
        public void InstanceKeptBeforeInitializeFollowsTheAppLogger()
        {
            var kept = LoggerLocator.Instance;
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            LoggerLocator.Initialize(new LoggerService(logger));
            kept.Information("kept entry");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();

            StringAssert.Contains("kept entry", SessionLogDirectory.WaitFor(file, "kept entry"),
                "The logger kept before Initialize still writes only to the console");
        }

        [Test]
        [Description("Reset, which runs on every play mode start, drops the app logger of the previous run: entries go to the Unity console again")]
        public void ResetReturnsEntriesToTheUnityConsole()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            LoggerLocator.Initialize(new LoggerService(logger));
            LoggerLocator.Reset();

            Assert.IsNull(LoggerLocator.Target, "Reset kept the app logger of the previous run");

            LogAssert.Expect(LogType.Warning, new Regex("after reset"));

            LoggerLocator.Instance.Warning("after reset");
        }
    }
}
