using System.IO;
using System.Threading.Tasks;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Standard implementation of IFileSystem using System.IO.File and System.IO.Path.
    /// </summary>
    public class StandardFileSystem : IFileSystem
    {
        public bool FileExists(string? path)
        {
            return File.Exists(path);
        }

        public Task<string> ReadAllTextAsync(string path)
        {
            return File.ReadAllTextAsync(path);
        }

        public Task WriteAllTextAsync(string path, string? contents)
        {
            return File.WriteAllTextAsync(path, contents ?? string.Empty);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public string GetTempFileName()
        {
            return Path.GetTempFileName();
        }

        public void CopyFile(string sourceFileName, string destFileName, bool overwrite)
        {
            File.Copy(sourceFileName, destFileName, overwrite);
        }

        public void EnsureDirectoryExists(string path)
        {
            string? directoryName = Path.GetDirectoryName(path);
            // If path is a directory itself, GetDirectoryName might return its parent.
            // If path is already a root or just a filename, GetDirectoryName might be null/empty.
            // We want to ensure the target directory for a file, or the directory itself if path is a dir.

            // If 'path' is intended to be a directory that should exist:
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
            {
                 Directory.CreateDirectory(path);
            }
            // If 'path' is a file path, ensure its containing directory exists:
            else if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            // If path is a directory that might not have a trailing slash, Path.GetDirectoryName might give parent.
            // A more robust way for "ensure this directory exists"
            if (!Directory.Exists(path) && !File.Exists(path)) // Check if it's not an existing file
            {
                 // Heuristic: if it doesn't have an extension, or ends with a separator, assume it's a directory path.
                 // This is not foolproof. For this library's use, paths will likely be well-defined.
                 if (!Path.HasExtension(path) || path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
                 {
                    Directory.CreateDirectory(path);
                 }
            }
        }

        // Simpler EnsureDirectoryExists for the directory containing a given file path
        public void EnsureContainingDirectoryExists(string filePath)
        {
            string? directoryName = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
        }


        public string? GetDirectoryName(string? path)
        {
            return Path.GetDirectoryName(path);
        }
    }
}
