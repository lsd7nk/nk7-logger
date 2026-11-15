using Microsoft.Extensions.Logging;
using Nk7.Logger;
using Nk7.Logger.Testing;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using System.Linq;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("Session logger: what reaches the file and the console, the line format and the file size limit")]
    public sealed class SessionLoggerTests
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
        [Description("The file keeps entries of its level and above even with the console off")]
        public void FileKeepsEntriesFromItsLevelWhileConsoleIsOff()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            logger.LogDebug("debug entry");
            logger.LogInformation("information entry");
            logger.LogWarning("warning entry");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var content = SessionLogDirectory.WaitFor(file, "warning entry");

            StringAssert.Contains("information entry", content, "An Information entry missed the Information file");
            StringAssert.Contains("warning entry", content, "A Warning entry missed the Information file");
            StringAssert.DoesNotContain("debug entry", content, "A Debug entry reached the Information file");
        }

        [Test]
        [Description("Console and file filter by their own levels: Warning only in the console, Error in both, Information nowhere")]
        public void ConsoleAndFileFilterByTheirOwnLevels()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.Warning, new SessionLogOptions(_directory.Path, LogLevel.Error));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("console only"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("both sinks"));

            logger.LogInformation("nowhere");
            logger.LogWarning("console only");
            logger.LogError("both sinks");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var content = SessionLogDirectory.WaitFor(file, "both sinks");

            StringAssert.Contains("both sinks", content, "An Error entry missed the Error file");
            StringAssert.DoesNotContain("console only", content, "A Warning entry reached the Error file");
            StringAssert.DoesNotContain("nowhere", content, "An Information entry reached the Error file");
        }

        [Test]
        [Description("A file line starts with the UTC time and the short level: '2026-10-30 09:27:12.586 [WRN] …'")]
        public void FileEntryStartsWithUtcTimeAndLevel()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));

            logger.LogWarning("leveled entry");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var content = SessionLogDirectory.WaitFor(file, "leveled entry");

            StringAssert.IsMatch(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[WRN\] leveled entry", content, "The line does not start with the time and the level");
        }

        [Test]
        [Description("Once the size limit is reached the session file stops growing: the first entries stay, the last are dropped")]
        public void SessionStopsWritingOnceItReachesTheSizeLimit()
        {
            const long limit = 300;

            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information, 5, limit));

            logger.LogInformation("first entry");

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();

            SessionLogDirectory.WaitFor(file, "first entry");

            for (var i = 0; i < 50; i++)
            {
                logger.LogInformation($"filler entry {i:D2} {new string('x', 40)}");
            }

            logger.LogInformation("last entry");

            SessionLogDirectory.WaitForStableLength(file);

            var content = SessionLogDirectory.Read(file);

            StringAssert.Contains("first entry", content, "The first session entry was lost");
            StringAssert.DoesNotContain("last entry", content, "An entry after the limit reached the file");
            Assert.That(new System.IO.FileInfo(file).Length, Is.LessThanOrEqualTo(limit), "The session file grew past the limit");
        }
    }
}
