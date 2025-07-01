using System.Threading.Tasks;

using System.IO; // For exceptions like FileNotFoundException, DirectoryNotFoundException
using System.Threading.Tasks;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Defines a contract for services that interact with the file system.
    /// This interface allows for mocking file system operations in unit tests.
    /// Methods generally follow the behavior of their System.IO counterparts.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check. Can be null.</param>
        /// <returns>true if the caller has the required permissions and <paramref name="path"/> contains the name of an existing file; otherwise, false. This method also returns false if <paramref name="path"/> is null, an invalid path, or a zero-length string.</returns>
        bool FileExists(string? path);

        /// <summary>
        /// Asynchronously opens a text file, reads all the text in the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading.</param>
        /// <returns>A task that represents the asynchronous read operation. The value of the TResult parameter contains a string containing all text in the file.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified path is invalid (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.IOException">An I/O error occurred while opening the file.</exception>
        /// <exception cref="System.UnauthorizedAccessException"><paramref name="path"/> specified a file that is read-only, or this operation is not supported on the current platform, or <paramref name="path"/> specified a directory, or the caller does not have the required permission.</exception>
        /// <exception cref="System.IO.FileNotFoundException">The file specified in <paramref name="path"/> was not found.</exception>
        /// <exception cref="System.NotSupportedException"><paramref name="path"/> is in an invalid format.</exception>
        /// <exception cref="System.Security.SecurityException">The caller does not have the required permission.</exception>
        Task<string> ReadAllTextAsync(string path);

        /// <summary>
        /// Asynchronously creates a new file, writes the specified string to the file, and then closes the file.
        /// If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path">The file to write to.</param>
        /// <param name="contents">The string to write to the file. If null, an empty string is written.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified path is invalid (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.IOException">An I/O error occurred while opening the file.</exception>
        /// <exception cref="System.UnauthorizedAccessException">The caller does not have the required permission.</exception>
        /// <exception cref="System.NotSupportedException"><paramref name="path"/> is in an invalid format.</exception>
        /// <exception cref="System.Security.SecurityException">The caller does not have the required permission.</exception>
        Task WriteAllTextAsync(string path, string? contents);

        /// <summary>
        /// Deletes the specified file. An exception is not thrown if the specified file does not exist.
        /// </summary>
        /// <param name="path">The name of the file to be deleted. Wildcard characters are not supported.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified path is invalid (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.IOException">The specified file is in use, or there is an existing file or directory that has the ReadOnly attribute.</exception>
        /// <exception cref="System.UnauthorizedAccessException">The caller does not have the required permission.</exception>
        /// <exception cref="System.NotSupportedException"><paramref name="path"/> is in an invalid format.</exception>
        void DeleteFile(string path);

        /// <summary>
        /// Creates a uniquely named, zero-byte temporary file on disk and returns the full path of that file.
        /// </summary>
        /// <returns>The full path of the temporary file.</returns>
        /// <exception cref="System.IO.IOException">An I/O error occurs, such as no unique temporary file name is available, or the temporary file could not be created.</exception>
        string GetTempFileName();

        /// <summary>
        /// Copies an existing file to a new file. Overwriting a file of the same name is allowed.
        /// </summary>
        /// <param name="sourceFileName">The file to copy.</param>
        /// <param name="destFileName">The name of the destination file. This cannot be a directory.</param>
        /// <param name="overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="sourceFileName"/> or <paramref name="destFileName"/> is null.</exception>
        /// <exception cref="System.IO.FileNotFoundException"><paramref name="sourceFileName"/> was not found.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The path specified in <paramref name="sourceFileName"/> or <paramref name="destFileName"/> is invalid (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.IOException">An I/O error has occurred, or <paramref name="destFileName"/> already exists and <paramref name="overwrite"/> is false.</exception>
        /// <exception cref="System.UnauthorizedAccessException">The caller does not have the required permission.</exception>
        void CopyFile(string sourceFileName, string destFileName, bool overwrite);

        /// <summary>
        /// Ensures that the directory for the specified path exists. If the directory structure does not exist, it is created.
        /// If the path is a file path, ensures its containing directory exists. If the path is a directory path, ensures that directory exists.
        /// </summary>
        /// <param name="path">The file or directory path for which to ensure the directory structure exists.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="System.IO.IOException">The directory specified by <paramref name="path"/> is a file, or the network name is not known.</exception>
        /// <exception cref="System.UnauthorizedAccessException">The caller does not have the required permission.</exception>
        void EnsureDirectoryExists(string path);

        /// <summary>
        /// Returns the directory information for the specified path string.
        /// </summary>
        /// <param name="path">The path of a file or directory. Can be null.</param>
        /// <returns>Directory information for <paramref name="path"/>, or null if <paramref name="path"/> denotes a root directory or is null. Returns <see cref="string.Empty"/> if path does not contain directory information.</returns>
        string? GetDirectoryName(string? path);
    }
}
