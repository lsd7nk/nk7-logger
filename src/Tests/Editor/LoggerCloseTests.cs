using Microsoft.Extensions.Logging;
using Nk7.Logger;
using Nk7.Logger.Testing;
using NUnit.Framework;
using System.Linq;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("Closing a logger: the last entries reach the file, the file is released, entries logged afterwards are dropped quietly")]
    public sealed class LoggerCloseTests
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
        [Description("Close writes everything logged so far to the session file before it returns, with no waiting for the background thread")]
        public void CloseWritesTheLastEntriesToTheFile()
        {
            const int count = 500;

            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            for (var i = 0; i < count; i++)
            {
                logger.LogInformation($"entry {i:D3}");
            }

            logger.LogInformation("last entry");

            LoggerFactory.Close(logger);

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var content = SessionLogDirectory.Read(file);

            StringAssert.Contains("entry 000", content, "The first entry is missing from the closed session file");
            StringAssert.Contains("last entry", content, "The last entry before Close did not reach the session file");
        }

        [Test]
        [Description("Close releases the session file: it is held open while the logger works and can be opened exclusively afterwards")]
        public void CloseReleasesTheSessionFile()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            logger.LogInformation("entry");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();

            SessionLogDirectory.WaitFor(file, "entry");

            Assert.Throws<System.IO.IOException>(() => OpenExclusively(file), "The session file is not held open while the logger works");

            LoggerFactory.Close(logger);

            Assert.DoesNotThrow(() => OpenExclusively(file), "The session file is still held open after Close");
        }

        [Test]
        [Description("An entry logged after Close does not throw and does not reach the file")]
        public void ClosedLoggerDropsLateEntriesQuietly()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            logger.LogInformation("early entry");

            LoggerFactory.Close(logger);

            Assert.DoesNotThrow(() => logger.LogInformation("late entry"), "Logging after Close threw");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var content = SessionLogDirectory.Read(file);

            StringAssert.Contains("early entry", content, "The entry before Close is missing");
            StringAssert.DoesNotContain("late entry", content, "An entry after Close reached the file");
        }

        [Test]
        [Description("Closing the same logger twice, a null logger or a logger the factory did not create does nothing")]
        public void ClosingTwiceOrAnUnknownLoggerDoesNothing()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));
            var foreign = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            LoggerFactory.Close(logger);

            Assert.DoesNotThrow(() => LoggerFactory.Close(logger), "Closing a logger twice threw");
            Assert.DoesNotThrow(() => LoggerFactory.Close(null), "Closing a null logger threw");
            Assert.DoesNotThrow(() => LoggerFactory.Close(foreign), "Closing a foreign logger threw");
        }

        private static void OpenExclusively(string file)
        {
            using var stream = new System.IO.FileStream(file, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
        }
    }
}
