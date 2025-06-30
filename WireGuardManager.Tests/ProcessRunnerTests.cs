using NUnit.Framework;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Diagnostics;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class ProcessRunnerTests
    {
        private static string GetTestHelperAppPath()
        {
            // Assumes TestHelperApp.exe (or TestHelperApp on Linux/macOS) is copied to the output directory of the test project.
            // The .csproj for WireGuardManager.Tests should reference TestHelperApp project and set CopyToOutputDirectory.
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string helperAppName = "TestHelperApp";
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                helperAppName += ".exe";
            }
            return Path.Combine(assemblyDir, helperAppName);
        }

        [Test]
        public void RunAsync_CommandNotFound_ThrowsCommandNotFoundException()
        {
            var runner = new ProcessRunner();
            Assert.ThrowsAsync<CommandNotFoundException>(() =>
                runner.RunAsync("non_existent_command_blah_blah", "")
            );
        }

        [Test]
        public async Task RunAsync_SuccessfulCommand_ReturnsSuccess()
        {
            string helperAppPath = GetTestHelperAppPath();
            if (!File.Exists(helperAppPath)) Assert.Inconclusive("TestHelperApp not found at " + helperAppPath);

            var runner = new ProcessRunner();
            var result = await runner.RunAsync(helperAppPath, "echo Hello"); // Uses ProcessExecutionResult
            Assert.That(result.Success, Is.True);
            Assert.That(result.StandardOutput, Does.Contain("TestHelperApp: Echoing arguments.").And.Does.Contain("Hello"));
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunAsync_ProcessTimesOut_ThrowsProcessTimeoutException()
        {
            string helperAppPath = GetTestHelperAppPath();
            if (!File.Exists(helperAppPath)) Assert.Inconclusive("TestHelperApp not found at " + helperAppPath);

            var runner = new ProcessRunner();
            var timeout = TimeSpan.FromMilliseconds(200); // Increased slightly to ensure timeout occurs reliably
            var sleepDuration = 4000;

            var ex = Assert.ThrowsAsync<ProcessTimeoutException>(() =>
                runner.RunAsync(helperAppPath, $"sleep {sleepDuration}", timeout: timeout)
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo(helperAppPath));
            Assert.That(ex.TimeoutDuration, Is.EqualTo(timeout));
            Assert.That(ex.Message, Does.Contain($"timed out after {timeout.TotalSeconds} seconds"));

            // Check if process was actually killed (this is hard to verify reliably across platforms without PIDs)
            // For now, the exception being thrown is the primary check.
            // We can also check console output for the "killed" message if ProcessRunner logs it.
            // This requires capturing console output, which can be done similar to WgManagerConfigTests.
        }

        [Test]
        public async Task RunAsync_ProcessCompletesWithinTimeout_ReturnsSuccess()
        {
            string helperAppPath = GetTestHelperAppPath();
            if (!File.Exists(helperAppPath)) Assert.Inconclusive("TestHelperApp not found at " + helperAppPath);

            var runner = new ProcessRunner();
            var timeout = TimeSpan.FromSeconds(5);
            var sleepDuration = 100;

            var result = await runner.RunAsync(helperAppPath, $"sleep {sleepDuration}", timeout: timeout); // Uses ProcessExecutionResult

            Assert.That(result.Success, Is.True);
            Assert.That(result.StandardOutput, Does.Contain("TestHelperApp: Sleep finished."));
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }
    }
}
