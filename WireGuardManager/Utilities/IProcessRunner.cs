using System;
using System.Threading.Tasks;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Defines a contract for services that can execute external command-line processes.
    /// This interface allows for mocking process execution in unit tests.
    /// </summary>
    public interface IProcessRunner
    {
        /// <summary>
        /// Runs an external process asynchronously and returns its execution result.
        /// </summary>
        /// <param name="fileName">The name or path of the executable file to run.</param>
        /// <param name="arguments">The command-line arguments to pass to the executable.</param>
        /// <param name="workingDirectory">The optional working directory for the process. If null, the current directory is used.</param>
        /// <param name="timeout">An optional <see cref="TimeSpan"/> to wait for the process to exit. If null, waits indefinitely.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ProcessExecutionResult"/>
        /// with the exit code, standard output, and standard error of the executed process.</returns>
        /// <exception cref="WireGuardManager.Exceptions.CommandNotFoundException">Thrown if the specified <paramref name="fileName"/> is not found.</exception>
        /// <exception cref="WireGuardManager.Exceptions.ProcessTimeoutException">Thrown if the process execution exceeds the specified <paramref name="timeout"/>.</exception>
        /// <exception cref="WireGuardManager.Exceptions.ExternalToolException">Thrown for other errors during process startup or execution.</exception>
        Task<ProcessExecutionResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, TimeSpan? timeout = null);
    }
}
