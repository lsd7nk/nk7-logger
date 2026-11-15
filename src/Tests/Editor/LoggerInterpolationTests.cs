using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Nk7.Logger;
using Nk7.Logger.Testing;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using System.Linq;
using System;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("Interpolated entries through the facade: the text reaches the file and the console intact, a disabled level writes nothing")]
    public sealed class LoggerInterpolationTests
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
        [Description("An interpolated entry reaches the session file with its values, format specifiers, non-ASCII text and the exception")]
        public void InterpolatedEntriesReachTheFile()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));
            var service = new LoggerService(logger);
            var number = 7;
            var name = "alpha";
            var filler = new string('x', 5000);

            service.Information($"value {number} of {name}");
            service.Warning($"привет {number:D3}");
            service.Information($"long {filler} end");
            service.Error(new InvalidOperationException("boom"), $"failed {number}");
            service.Debug($"hidden {number}");

            LoggerFactory.Close(logger);

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var content = SessionLogDirectory.Read(file);

            StringAssert.Contains("value 7 of alpha", content, "The interpolated entry is missing or damaged");
            StringAssert.Contains("привет 007", content, "Non-ASCII text or the format specifier is damaged");
            StringAssert.Contains("long " + filler + " end", content, "A long entry is missing or cut");
            StringAssert.Contains("failed 7", content, "The entry logged with an exception is missing");
            StringAssert.Contains("boom", content, "The exception did not reach the file");
            StringAssert.DoesNotContain("hidden 7", content, "An entry below the minimum level reached the file");
        }

        [Test]
        [Description("Entries logged in a row keep their own text: every entry is in the file once, in the order it was logged")]
        public void EntriesInARowKeepTheirOwnText()
        {
            const int count = 500;

            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));
            var service = new LoggerService(logger);

            for (var i = 0; i < count; i++)
            {
                service.Information($"entry {i:D3} done");
            }

            LoggerFactory.Close(logger);

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var lines = SessionLogDirectory.Read(file).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(count, lines.Length, "The number of lines in the file differs from the number of entries");

            for (var i = 0; i < count; i++)
            {
                StringAssert.EndsWith("entry " + i.ToString("D3") + " done", lines[i].TrimEnd('\r'), "Line " + i + " holds another entry's text");
            }
        }

        [Test]
        [Description("An interpolated entry reaches the Unity console: Information as Log, Error with an exception as Error with the exception text")]
        public void InterpolatedEntriesReachTheConsole()
        {
            var logger = LoggerFactory.GetSessionLogger(LogLevel.Information, new SessionLogOptions(_directory.Path, LogLevel.Information));
            var service = new LoggerService(logger);
            var number = 5;

            LogAssert.Expect(LogType.Log, new Regex("^console value 5$"));
            LogAssert.Expect(LogType.Error, new Regex("console failure 5[\\s\\S]*boom"));

            try
            {
                service.Information($"console value {number}");
                service.Error(new InvalidOperationException("boom"), $"console failure {number}");
            }
            finally
            {
                LoggerFactory.Close(logger);
            }
        }
    }
}
