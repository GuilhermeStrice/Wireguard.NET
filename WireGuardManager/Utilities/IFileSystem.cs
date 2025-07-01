using System.Threading.Tasks;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Defines an interface for common file system operations, allowing for testability.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check.</param>
        /// <returns>true if the caller has the required permissions and path contains the name of an existing file; otherwise, false.</returns>
        bool FileExists(string? path);

        /// <summary>
        /// Asynchronously opens a text file, reads all the text in the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading.</param>
        /// <returns>A task that represents the asynchronous read operation, which wraps the string containing all text in the file.</returns>
        Task<string> ReadAllTextAsync(string path);

        /// <summary>
        /// Asynchronously creates a new file, writes the specified string to the file, and then closes the file.
        /// If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path">The file to write to.</param>
        /// <param name="contents">The string to write to the file. If null, an empty file will be created/overwritten.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        Task WriteAllTextAsync(string path, string? contents);

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The name of the file to be deleted. Wildcard characters are not supported.</param>
        void DeleteFile(string path);

        /// <summary>
        /// Creates a uniquely named, zero-byte temporary file on disk and returns the full path of that file.
        /// </summary>
        /// <returns>The full path of the temporary file.</returns>
        string GetTempFileName();

        /// <summary>
        /// Copies an existing file to a new file. Overwriting a file of the same name is allowed.
        /// </summary>
        /// <param name="sourceFileName">The file to copy.</param>
        /// <param name="destFileName">The name of the destination file. This cannot be a directory.</param>
        /// <param name="overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        void CopyFile(string sourceFileName, string destFileName, bool overwrite);

        /// <summary>
        /// Creates all directories and subdirectories in the specified path unless they already exist.
        /// </summary>
        /// <param name="path">The directory path to create.</param>
        /// <returns>An object that represents the directory at the specified path. This object is returned regardless of whether a directory at the specified path already exists.</returns>
        // Potentially: DirectoryInfo CreateDirectory(string path);
        // For simplicity if only used for ensuring path for WgConfigFileManager
        void EnsureDirectoryExists(string path);

        /// <summary>
        /// Returns the directory information for the specified path string.
        /// </summary>
        /// <param name="path">The path of a file or directory.</param>
        /// <returns>Directory information for path, or null if path denotes a root directory or is null. Returns String.Empty if path does not contain directory information.</returns>
        string? GetDirectoryName(string? path);
    }
}
