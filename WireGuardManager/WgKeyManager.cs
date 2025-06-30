using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;

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
                // TODO: Add validation here in Step 2.1
                PrivateKey = privateKey;
                PublicKey = publicKey;
            }
        }

        private static string GetWgPath() // TODO: Make configurable in Step 2.2 (Phase 2)
        {
            return "wg";
        }

        public static async Task<KeyPair> GenerateKeyPairAsync()
        {
            string wgPath = GetWgPath();
            string? privateKey = null;
            string? publicKey = null;
            string? tempPrivateKeyFile = null;

            try
            {
                // 1. Generate Private Key
                var genkeyResult = await ProcessRunner.RunAsync(wgPath, "genkey", timeout: ProcessRunner.DefaultShortOperationTimeout);
                if (!genkeyResult.Success || string.IsNullOrWhiteSpace(genkeyResult.StandardOutput))
                {
                    throw new ExternalToolException(wgPath, "Failed to generate private key using 'wg genkey'.",
                        genkeyResult.ExitCode, genkeyResult.StandardOutput, genkeyResult.StandardError);
                }
                privateKey = genkeyResult.StandardOutput.Trim();

                // 2. Generate Public Key from Private Key
                // Using a temporary file is more robust for ProcessRunner without direct stdin piping.
                tempPrivateKeyFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempPrivateKeyFile, privateKey);

                // The command `wg pubkey < privatekeyfile` requires shell redirection.
                // We'll invoke it via shell to ensure redirection works.
                string pubkeyCommand = $"/bin/sh -c \"{wgPath} pubkey < '{tempPrivateKeyFile.Replace("'", "'\\''")}'\"";
                var pubkeyResult = await ProcessRunner.RunAsync("/bin/sh", $"-c \"'{wgPath}' pubkey < '{tempPrivateKeyFile.Replace("'", "'\\''")}'\"", timeout: ProcessRunner.DefaultShortOperationTimeout);


                if (!pubkeyResult.Success || string.IsNullOrWhiteSpace(pubkeyResult.StandardOutput))
                {
                     // Fallback: try direct echo to wg pubkey via shell (if the temp file method had issues, though less likely)
                    Console.WriteLine($"Warning: 'wg pubkey < tempfile' failed (Exit: {pubkeyResult.ExitCode}, Err: {pubkeyResult.StandardError}). Attempting echo to pubkey pipe.");
                    var shellEchoResult = await ProcessRunner.RunAsync("/bin/sh", $"-c \"echo '{privateKey.Replace("'", "'\\''")}' | '{wgPath}' pubkey\"", timeout: ProcessRunner.DefaultShortOperationTimeout);
                    if (!shellEchoResult.Success || string.IsNullOrWhiteSpace(shellEchoResult.StandardOutput)) {
                         throw new ExternalToolException(wgPath,
                            $"Failed to generate public key using '{wgPath} pubkey'. Both temp file and echo pipe methods failed.",
                            shellEchoResult.ExitCode, shellEchoResult.StandardOutput, shellEchoResult.StandardError);
                    }
                    publicKey = shellEchoResult.StandardOutput.Trim();
                }
                else
                {
                     publicKey = pubkeyResult.StandardOutput.Trim();
                }

                if (string.IsNullOrWhiteSpace(privateKey) || string.IsNullOrWhiteSpace(publicKey))
                {
                    // This case should ideally be caught by the checks above throwing ExternalToolException
                    throw new WireGuardManagerException("Key generation resulted in one or more empty keys, despite tool success reports.");
                }

                return new KeyPair(privateKey, publicKey);
            }
            catch (WireGuardManagerException) { throw; } // Re-throw our specific exceptions
            catch (Exception ex) // Catch other unexpected errors (e.g., file system errors for temp file)
            {
                throw new WireGuardManagerException("An unexpected error occurred during key pair generation.", ex);
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
            string wgPath = GetWgPath();
            var result = await ProcessRunner.RunAsync(wgPath, "genpsk");
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                throw new ExternalToolException(wgPath, "Failed to generate preshared key using 'wg genpsk'.",
                    result.ExitCode, result.StandardOutput, result.StandardError);
            }
            return result.StandardOutput.Trim();
        }
    }
}
