using Nk7.Logger.Testing;
using Nk7.Logger;
using NUnit.Framework;
using System.IO;

namespace Nk7.Logger.Tests
{
    [Category(LoggerTestCategories.LOGGER)]
    [Description("Session file size cap: what is written up to the limit and what is dropped after it")]
    public sealed class CappedFileStreamTests
    {
        private SessionLogDirectory _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = new SessionLogDirectory();
            Directory.CreateDirectory(_directory.Path);
        }

        [TearDown]
        public void TearDown()
        {
            _directory.Dispose();
        }

        [Test]
        [Description("Writes up to the limit are kept; the first write that does not fit closes the file for the rest of the session")]
        public void FirstOverflowClosesTheFile()
        {
            var path = Path.Combine(_directory.Path, "capped.log");

            using (var stream = new CappedFileStream(path, 100))
            {
                stream.Write(new byte[60], 0, 60);
                stream.Write(new byte[60], 0, 60);
                stream.Write(new byte[10], 0, 10);
                stream.Flush();
            }

            Assert.That(new FileInfo(path).Length, Is.EqualTo(60),
                "The file holds a write past the limit or lost the write that fit");
        }
    }
}
