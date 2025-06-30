using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace WireGuardManager.Utilities
{
    public static class ProcessRunner
    {
        public class ProcessResult
        {
            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
            public bool Success => ExitCode == 0;

            public ProcessResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }
        }

        public static async Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory = null)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    process.StartInfo.WorkingDirectory = workingDirectory;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputBuilder.AppendLine(args.Data); };
                process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorBuilder.AppendLine(args.Data); };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Consider adding a timeout
                    await process.WaitForExitAsync(); // .NET 5+
                                                      // For older .NET, you might need a more complex way to wait or use Task.Run for process.WaitForExit()
                }
                catch (Exception ex)
                {
                    // Handle exceptions during process start, e.g., file not found
                    return new ProcessResult(-1, string.Empty, $"Failed to start process {fileName}: {ex.Message}");
                }


                return new ProcessResult(process.ExitCode, outputBuilder.ToString().TrimEnd('\r', '\n'), errorBuilder.ToString().TrimEnd('\r', '\n'));
            }
        }
    }
}
