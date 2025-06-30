using System;
using System.Threading.Tasks;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Defines an interface for running external processes.
    /// </summary>
    public interface IProcessRunner
    {
        /// <summary>
        /// Runs an external process asynchronously.
        /// </summary>
        /// <param name="fileName">The name of the executable file to run.</param>
        /// <param name="arguments">The command-line arguments to pass to the executable.</param>
        /// <param name="workingDirectory">Optional working directory for the process.</param>
        /// <param name="timeout">Optional timeout for the process execution.</param>
        /// <returns>A task representing the asynchronous operation, with a result of <see cref="ProcessExecutionResult"/>.</returns>
        Task<ProcessExecutionResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, TimeSpan? timeout = null);
    }
}
