using Microsoft.Extensions.Logging;
using Nk7.Logger;
using Nk7.Logger.Testing;
using NUnit.Framework;
using System.Linq;
using System.IO;
using System;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("Session files: each launch has its own file, newest first, only the last ones are kept, foreign files are left alone")]
    public sealed class SessionLogFilesTests
    {
        private SessionLogDirectory _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = new SessionLogDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            _directory.Dispose();
        }

        [Test]
        [Description("Without a logs folder the session list is empty and there is no error")]
        public void MissingDirectoryHasNoSessions()
        {
            CollectionAssert.IsEmpty(SessionLogFiles.GetSessionFiles(_directory.Path), "Sessions found without a logs folder");
        }

        [Test]
        [Description("Each logger starts its own session in a separate file, the newest one comes first")]
        public void EachLoggerStartsItsOwnSessionAndTheNewestComesFirst()
        {
            var first = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));
            var second = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            first.LogInformation("from first");
            second.LogInformation("from second");

            var files = SessionLogFiles.GetSessionFiles(_directory.Path);

            Assert.That(files.Count, Is.EqualTo(2), "Two loggers must give two session files");
            StringAssert.Contains("from second", SessionLogDirectory.WaitFor(files[0], "from second"), "The first in the list is not the newest session");
            StringAssert.Contains("from first", SessionLogDirectory.WaitFor(files[1], "from first"), "The older session is not second");
        }

        [Test]
        [Description("Only the last sessions within the limit are kept, the oldest are removed")]
        public void OnlyTheLastSessionsAreKept()
        {
            const int maxSessions = 3;

            for (var i = 0; i < maxSessions + 2; i++)
            {
                LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information, maxSessions))
                    .LogInformation($"session {i}");
            }

            var files = SessionLogFiles.GetSessionFiles(_directory.Path);

            Assert.That(files.Count, Is.EqualTo(maxSessions), "More or fewer sessions than the limit");
            StringAssert.Contains("session 4", SessionLogDirectory.WaitFor(files[0], "session 4"), "The newest session is not first");
            StringAssert.Contains("session 2", SessionLogDirectory.WaitFor(files[2], "session 2"), "Not the oldest sessions were removed");
        }

        [Test]
        [Description("Foreign files in the logs folder are neither listed as sessions nor removed by the cleanup")]
        public void ForeignFilesAreNeitherListedNorRemoved()
        {
            Directory.CreateDirectory(_directory.Path);
            File.WriteAllText(Path.Combine(_directory.Path, "notes.txt"), "keep me");

            for (var i = 0; i < 3; i++)
            {
                LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information, 1));
            }

            Assert.That(SessionLogFiles.GetSessionFiles(_directory.Path).Count, Is.EqualTo(1), "A foreign file counted as a session or the limit did not apply");
            CollectionAssert.Contains(_directory.FileNames(), "notes.txt", "The cleanup removed a foreign file");
        }

        [Test]
        [Description("With the clock turned back the new session still comes first and the old ones are dropped first")]
        public void ClockTurnedBackStillPutsTheNewSessionFirst()
        {
            const string future = "session-20990101-000000-0000000.log";

            Directory.CreateDirectory(_directory.Path);
            File.WriteAllText(Path.Combine(_directory.Path, future), "from the future");

            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information, 2));

            logger.LogInformation("current session");

            var files = SessionLogFiles.GetSessionFiles(_directory.Path);

            Assert.That(files.Count, Is.EqualTo(2), "The session count does not match the limit");
            StringAssert.Contains("current session", SessionLogDirectory.WaitFor(files[0], "current session"),
                "The current session is not first after the clock went back");
            Assert.That(Path.GetFileName(files[1]), Is.EqualTo(future), "The older session is not second");
        }

        [Test]
        [Description("Options without a folder, with zero sessions or zero size are rejected right away")]
        public void OptionsRejectValuesThatCannotHoldASession()
        {
            Assert.Throws<ArgumentException>(() => new SessionLogOptions(" ", LogLevel.Information), "An empty logs folder was accepted");
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionLogOptions(_directory.Path, LogLevel.Information, 0), "Zero sessions were accepted");
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionLogOptions(_directory.Path, LogLevel.Information, 5, 0), "A zero session size was accepted");
        }
    }
}
