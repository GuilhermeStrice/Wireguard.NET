using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;

namespace WireGuardManager
{
    public static class WgKeyManager
    {
        public class KeyPair
        {
            public string PrivateKey { get; }
            public string PublicKey { get; }

            public KeyPair(string privateKey, string publicKey)
            {
                PrivateKey = privateKey;
                PublicKey = publicKey;
            }
        }

        private static string GetWgPath()
        {
            // In Linux, 'wg' should be in the PATH.
            // If it's installed in a non-standard location, users might need to configure this.
            // For simplicity, we assume 'wg' is accessible.
            // Could add configuration options or environment variable checks later.
            return "wg";
        }

        public static async Task<KeyPair> GenerateKeyPairAsync()
        {
            string privateKey = null;
            string publicKey = null;
            string tempPrivateKeyFile = null;

            try
            {
                // 1. Generate Private Key
                var genkeyResult = await ProcessRunner.RunAsync(GetWgPath(), "genkey");
                if (!genkeyResult.Success || string.IsNullOrWhiteSpace(genkeyResult.StandardOutput))
                {
                    throw new Exception($"Failed to generate private key. wg genkey error: {genkeyResult.StandardError}");
                }
                privateKey = genkeyResult.StandardOutput.Trim();

                // 2. Generate Public Key from Private Key
                // wg pubkey expects private key from stdin or a file.
                // Using a temporary file is more robust than piping directly if there are issues with stdin handling in ProcessRunner or wg.

                tempPrivateKeyFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempPrivateKeyFile, privateKey);

                // The command `wg pubkey < privatekeyfile`
                var pubkeyResult = await ProcessRunner.RunAsync(GetWgPath(), $"pubkey < \"{tempPrivateKeyFile}\"");

                // Alternative: try piping if the above has issues with shell redirection.
                // This would require ProcessRunner to support redirecting standard input.
                // For now, we assume the file method is more straightforward with current ProcessRunner.
                // If direct piping is preferred:
                // var pubkeyResult = await ProcessRunner.RunAsync(GetWgPath(), "pubkey", inputForStdin: privateKey);
                // This would require modifying ProcessRunner to accept `inputForStdin` and write to `process.StandardInput`.

                if (!pubkeyResult.Success || string.IsNullOrWhiteSpace(pubkeyResult.StandardOutput))
                {
                    // Let's try to read the public key from the standard output of `wg genkey | wg pubkey` if the previous method fails
                    // This is a common pattern but requires shell interpretation for the pipe.
                    // We can do this by explicitly invoking a shell.
                    var shellResult = await ProcessRunner.RunAsync("/bin/sh", $"-c \"echo '{privateKey.Replace("'", "'\\''")}' | {GetWgPath()} pubkey\"");
                    if (!shellResult.Success || string.IsNullOrWhiteSpace(shellResult.StandardOutput)) {
                         throw new Exception($"Failed to generate public key. wg pubkey error: {pubkeyResult.StandardError} (file method) and {shellResult.StandardError} (pipe method)");
                    }
                    publicKey = shellResult.StandardOutput.Trim();

                } else {
                     publicKey = pubkeyResult.StandardOutput.Trim();
                }


                if (string.IsNullOrWhiteSpace(privateKey) || string.IsNullOrWhiteSpace(publicKey))
                {
                    throw new Exception("Key generation resulted in one or more empty keys.");
                }

                return new KeyPair(privateKey, publicKey);
            }
            catch (Exception ex)
            {
                // Log or handle more gracefully
                throw new Exception("Error during key pair generation: " + ex.Message, ex);
            }
            finally
            {
                if (tempPrivateKeyFile != null && File.Exists(tempPrivateKeyFile))
                {
                    try { File.Delete(tempPrivateKeyFile); } catch { /* best effort */ }
                }
            }
        }

        public static async Task<string> GeneratePresharedKeyAsync()
        {
            var result = await ProcessRunner.RunAsync(GetWgPath(), "genpsk");
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                throw new Exception($"Failed to generate preshared key. wg genpsk error: {result.StandardError}");
            }
            return result.StandardOutput.Trim();
        }
    }
}
