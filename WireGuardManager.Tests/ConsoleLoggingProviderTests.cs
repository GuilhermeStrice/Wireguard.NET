using NUnit.Framework;
using WireGuardManager.Utilities;
using System;
using System.IO;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class ConsoleLoggingProviderTests
    {
        private StringWriter _stringWriter = null!;
        private TextWriter _originalOut;
        private TextWriter _originalError;
        private ConsoleLoggingProvider _logger = null!;

        [SetUp]
        public void SetUp()
        {
            _stringWriter = new StringWriter();
            _originalOut = Console.Out;
            _originalError = Console.Error;
            Console.SetOut(_stringWriter); // Capture Console.Out
            Console.SetError(_stringWriter); // Capture Console.Error to the same writer for simplicity in test
            _logger = new ConsoleLoggingProvider();
        }

        [TearDown]
        public void TearDown()
        {
            Console.SetOut(_originalOut); // Restore original Console.Out
            Console.SetError(_originalError); // Restore original Console.Error
            _stringWriter.Dispose();
            WgLogging.MinimumLogLevel = LogLevel.Info; // Reset global minimum log level
        }

        [TestCase(LogLevel.Trace, "Trace Message", true)]
        [TestCase(LogLevel.Debug, "Debug Message", true)]
        [TestCase(LogLevel.Info, "Info Message", true)]
        [TestCase(LogLevel.Warning, "Warning Message", true)]
        [TestCase(LogLevel.Error, "Error Message", true)]
        public void Log_WhenMinLevelIsTrace_AllLevelsAreLogged(LogLevel messageLevel, string message, bool shouldLog)
        {
            WgLogging.MinimumLogLevel = LogLevel.Trace;
            LogMessageAtLevel(messageLevel, message);
            AssertLogging(message, shouldLog);
        }

        [TestCase(LogLevel.Trace, "Trace Message", false)]
        [TestCase(LogLevel.Debug, "Debug Message", false)]
        [TestCase(LogLevel.Info, "Info Message", true)]
        [TestCase(LogLevel.Warning, "Warning Message", true)]
        [TestCase(LogLevel.Error, "Error Message", true)]
        public void Log_WhenMinLevelIsInfo_FiltersLowerLevels(LogLevel messageLevel, string message, bool shouldLog)
        {
            WgLogging.MinimumLogLevel = LogLevel.Info; // Default, but explicit for test clarity
            LogMessageAtLevel(messageLevel, message);
            AssertLogging(message, shouldLog);
        }

        [TestCase(LogLevel.Trace, "Trace Message", false)]
        [TestCase(LogLevel.Debug, "Debug Message", false)]
        [TestCase(LogLevel.Info, "Info Message", false)]
        [TestCase(LogLevel.Warning, "Warning Message", false)]
        [TestCase(LogLevel.Error, "Error Message", false)]
        [TestCase(LogLevel.None, "None Message", false)] // LogLevel.None should suppress its own level too if not explicitly handled
        public void Log_WhenMinLevelIsNone_NoMessagesAreLogged(LogLevel messageLevel, string message, bool shouldLog)
        {
            WgLogging.MinimumLogLevel = LogLevel.None;
            LogMessageAtLevel(messageLevel, message); // Try to log "None Message" at LogLevel.None

            // Special case for LogLevel.None: if messageLevel is None, it won't have a LogX method.
            // The test is more about ensuring other levels are suppressed.
            // If we were to test logging a "None" level message, it wouldn't make sense as there's no LogNone method.
            // The purpose of LogLevel.None as a minimum is to suppress all.
            if(messageLevel == LogLevel.None && shouldLog)
            {
                // This case is tricky because LogLevel.None isn't a loggable level via LogTrace, LogDebug etc.
                // It's a filter setting. So, a message of "None" level cannot be logged.
                // The test case (LogLevel.None, "None Message", false) is valid as is.
            }

            AssertLogging(message, shouldLog);
        }

        [Test]
        public void LogError_IncludesExceptionDetails()
        {
            WgLogging.MinimumLogLevel = LogLevel.Error;
            string errorMessage = "An error occurred";
            var exception = new InvalidOperationException("Test exception details.");

            _logger.LogError(errorMessage, exception);

            string output = _stringWriter.ToString();
            Assert.That(output, Does.Contain(errorMessage));
            Assert.That(output, Does.Contain("ERROR"));
            Assert.That(output, Does.Contain("Exception: System.InvalidOperationException: Test exception details."));
            // Stack trace will also be there if exception was thrown
        }

        private void LogMessageAtLevel(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Trace: _logger.LogTrace(message); break;
                case LogLevel.Debug: _logger.LogDebug(message); break;
                case LogLevel.Info: _logger.LogInfo(message); break;
                case LogLevel.Warning: _logger.LogWarning(message); break;
                case LogLevel.Error: _logger.LogError(message); break;
                // LogLevel.None is not a level to log at, but a filter setting.
            }
        }

        private void AssertLogging(string message, bool shouldBePresent)
        {
            string output = _stringWriter.ToString();
            if (shouldBePresent)
            {
                Assert.That(output, Does.Contain(message));
            }
            else
            {
                Assert.That(output, Does.Not.Contain(message));
            }
        }
    }
}
