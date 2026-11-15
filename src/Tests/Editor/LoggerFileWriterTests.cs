using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Nk7.Logger;
using Nk7.Logger.Testing;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using System.Linq;
using System.IO;
using System;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("File output: the package's own background writer, the single log file, and the ZLogger writer kept for WebGL")]
    public sealed class LoggerFileWriterTests
    {
        private SessionLogDirectory _directory;
        private bool _ownFileWriter;

        [SetUp]
        public void SetUp()
        {
            _directory = new SessionLogDirectory();
            _ownFileWriter = LoggerFactory.OwnFileWriter;
        }

        [TearDown]
        public void TearDown()
        {
            LoggerFactory.OwnFileWriter = _ownFileWriter;
            _directory.Dispose();
        }

        [Test]
        [Description("Entries logged from several threads at once all reach the file, each once and with its own text")]
        public void EntriesFromSeveralThreadsAllReachTheFile()
        {
            const int threadCount = 4;
            const int perThread = 250;

            var logger = LoggerFactory.GetSessionLogger(LogLevel.None, new SessionLogOptions(_directory.Path, LogLevel.Information));
            var service = new LoggerService(logger);
            var threads = new Thread[threadCount];

            for (var t = 0; t < threadCount; t++)
            {
                var thread = t;

                threads[t] = new Thread(() =>
                {
                    for (var i = 0; i < perThread; i++)
                    {
                        service.Information($"thread {thread} entry {i:D3} done");
                    }
                });
            }

            for (var t = 0; t < threadCount; t++)
            {
                threads[t].Start();
            }

            for (var t = 0; t < threadCount; t++)
            {
                threads[t].Join();
            }

            LoggerFactory.Close(logger);

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var lines = SessionLogDirectory.Read(file).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var texts = new HashSet<string>();

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                texts.Add(line.Substring(line.IndexOf("] ", StringComparison.Ordinal) + 2));
            }

            Assert.AreEqual(threadCount * perThread, lines.Length, "The number of lines in the file differs from the number of entries");
            Assert.AreEqual(threadCount * perThread, texts.Count, "Some entries are repeated or damaged");

            for (var t = 0; t < threadCount; t++)
            {
                for (var i = 0; i < perThread; i++)
                {
                    var expected = "thread " + t + " entry " + i.ToString("D3") + " done";

                    Assert.IsTrue(texts.Contains(expected), "The entry is missing: " + expected);
                }
            }
        }

        [Test]
        [Description("GetLogger with a file path writes entries to that file, creates the missing folder and clears a file left from before")]
        public void SingleFileLoggerWritesEntriesToItsFile()
        {
            var inMissingFolder = Path.Combine(_directory.Path, "single", "app.log");
            var leftFromBefore = Path.Combine(_directory.Path, "app.log");

            AssertSingleFile(inMissingFolder);

            File.WriteAllText(leftFromBefore, "stale content");

            AssertSingleFile(leftFromBefore);
        }

        [Test]
        [Description("With the ZLogger writer, the one kept for WebGL, the session logger still filters the console and the file by their own levels and writes interpolated entries")]
        public void ZLoggerWriterKeepsTheSessionLoggerWorking()
        {
            LoggerFactory.OwnFileWriter = false;

            var logger = LoggerFactory.GetSessionLogger(LogLevel.Warning, new SessionLogOptions(_directory.Path, LogLevel.Error));
            var service = new LoggerService(logger);
            var number = 3;

            LogAssert.Expect(LogType.Warning, new Regex("fallback console only 3"));
            LogAssert.Expect(LogType.Error, new Regex("fallback both sinks 3"));

            try
            {
                service.Information($"fallback nowhere {number}");
                service.Warning($"fallback console only {number}");
                service.Error($"fallback both sinks {number}");
            }
            finally
            {
                LoggerFactory.Close(logger);
            }

            var file = SessionLogFiles.GetSessionFiles(_directory.Path).Single();
            var content = SessionLogDirectory.Read(file);

            StringAssert.Contains("fallback both sinks 3", content, "An Error entry missed the Error file");
            StringAssert.DoesNotContain("fallback console only 3", content, "A Warning entry reached the Error file");
            StringAssert.DoesNotContain("fallback nowhere 3", content, "An Information entry reached the Error file");
        }

        [Test]
        [Description("With the ZLogger writer, the one kept for WebGL, GetLogger with a file path still writes entries to that file")]
        public void ZLoggerWriterKeepsTheSingleFileLoggerWorking()
        {
            LoggerFactory.OwnFileWriter = false;

            AssertSingleFile(Path.Combine(_directory.Path, "fallback", "app.log"));
        }

        private static void AssertSingleFile(string path)
        {
            var logger = LoggerFactory.GetLogger(LogLevel.Information, path);
            var service = new LoggerService(logger);
            var number = 9;

            LogAssert.Expect(LogType.Log, new Regex("single file value 9"));
            LogAssert.Expect(LogType.Warning, new Regex("single file plain"));

            try
            {
                service.Information($"single file value {number}");
                service.Warning("single file plain");
                service.Debug($"single file hidden {number}");
            }
            finally
            {
                LoggerFactory.Close(logger);
            }

            var content = SessionLogDirectory.Read(path);

            StringAssert.Contains("single file value 9", content, "The interpolated entry is missing from " + path);
            StringAssert.Contains("single file plain", content, "The plain entry is missing from " + path);
            StringAssert.DoesNotContain("single file hidden 9", content, "An entry below the minimum level reached " + path);
            StringAssert.DoesNotContain("stale content", content, "The file left from before was not cleared: " + path);
        }
    }
}
