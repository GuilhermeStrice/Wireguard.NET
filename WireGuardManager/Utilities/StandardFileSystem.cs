using System.IO;
using System.Threading.Tasks;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Implements the <see cref="IFileSystem"/> interface using standard <see cref="System.IO"/> operations.
    /// This class provides the default, real file system interactions for the library.
    /// </summary>
    public class StandardFileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public bool FileExists(string? path)
        {
            return File.Exists(path);
        }

        /// <inheritdoc/>
        public Task<string> ReadAllTextAsync(string path)
        {
            return File.ReadAllTextAsync(path);
        }

        /// <inheritdoc/>
        public Task WriteAllTextAsync(string path, string? contents)
        {
            return File.WriteAllTextAsync(path, contents ?? string.Empty);
        }

        /// <inheritdoc/>
        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        /// <inheritdoc/>
        public string GetTempFileName()
        {
            return Path.GetTempFileName();
        }

        /// <inheritdoc/>
        public void CopyFile(string sourceFileName, string destFileName, bool overwrite)
        {
            File.Copy(sourceFileName, destFileName, overwrite);
        }

        /// <inheritdoc/>
        public void EnsureDirectoryExists(string path)
        {
            // This implementation aims to create the directory if 'path' itself is meant to be a directory,
            // or the containing directory if 'path' is a file path.
            if (string.IsNullOrWhiteSpace(path)) return;

            string? directoryToEnsure = null;

            // Check if the path might be a directory path itself
            // Heuristic: no extension or ends with a separator. This isn't foolproof.
            // Or, if it already exists as a directory.
            if (Directory.Exists(path)) return; // Already exists as a directory

            if (!Path.HasExtension(path) ||
                path.EndsWith(Path.DirectorySeparatorChar.ToString()) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                directoryToEnsure = path;
            }
            else
            {
                // Assume it's a file path, get its directory
                directoryToEnsure = Path.GetDirectoryName(path);
            }

            if (!string.IsNullOrWhiteSpace(directoryToEnsure) && !Directory.Exists(directoryToEnsure))
            {
                Directory.CreateDirectory(directoryToEnsure);
            }
        }

        /// <inheritdoc/>
        public string? GetDirectoryName(string? path)
        {
            return Path.GetDirectoryName(path);
        }
    }
}
