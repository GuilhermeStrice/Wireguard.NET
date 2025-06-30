using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WireGuardManager.Exceptions;

namespace WireGuardManager.Utilities
{
    public class ProcessRunner : IProcessRunner // Made non-static, implements IProcessRunner
    {
        // Default timeouts for external processes
        public static readonly TimeSpan DefaultShortOperationTimeout = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan DefaultLongOperationTimeout = TimeSpan.FromSeconds(60);

        // ProcessResult class was moved to ProcessExecutionResult.cs as a top-level class

        public async Task<ProcessExecutionResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, TimeSpan? timeout = null) // Instance method, returns ProcessExecutionResult
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
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
                {
                    throw new CommandNotFoundException(fileName, ex);
                }
                catch (Exception ex)
                {
                    throw new ExternalToolException(fileName, $"Failed to start process '{fileName} {arguments}'.", ex);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exitedGracefully = true;
                CancellationTokenSource? cts = null;
                if (timeout.HasValue)
                {
                    cts = new CancellationTokenSource(timeout.Value);
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (TaskCanceledException) // Catches OperationCanceledException as well
                    {
                        exitedGracefully = false;
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill(true); // Kill entire process tree if possible
                                Console.WriteLine($"Warning: Process '{fileName} {arguments}' timed out after {timeout.Value.TotalSeconds}s and was killed.");
                            }
                        }
                        catch (Exception killEx)
                        {
                            // Log or handle failure to kill, but the timeout exception is primary
                            Console.WriteLine($"Warning: Failed to kill timed-out process '{fileName} {arguments}'. {killEx.Message}");
                        }
                        throw new ProcessTimeoutException(fileName, timeout.Value, $"{fileName} {arguments}");
                    }
                    finally
                    {
                        cts?.Dispose();
                    }
                }
                else
                {
                    await process.WaitForExitAsync();
                }

                // Ensure all output is processed after exit, especially if timeout occurred close to exit
                // but WaitForExitAsync might need a bit more time for async pipes to flush.
                // A small delay or a more robust pipe reading mechanism might be needed for edge cases.
                // For now, relying on BeginOutputReadLine/BeginErrorReadLine to complete.
                // If process was killed due to timeout, ExitCode might be unreliable or reflect the kill signal.
                // However, the ProcessTimeoutException is the primary indicator of failure in that case.

                return new ProcessExecutionResult(process.ExitCode, outputBuilder.ToString().TrimEnd('\r', '\n'), errorBuilder.ToString().TrimEnd('\r', '\n'));
            }
        }
    }
}
