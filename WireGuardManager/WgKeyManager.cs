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

        private static string GetWgPath(WgManagerConfig? config = null)
        {
            // Load config if not provided to check for custom path
            config ??= WgManagerConfig.Load();
            return string.IsNullOrWhiteSpace(config.WgPath) ? "wg" : config.WgPath;
        }

        public static async Task<KeyPair> GenerateKeyPairAsync(WgManagerConfig? config = null)
        {
            string wgPath = GetWgPath(config);
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

        public static async Task<string> GeneratePresharedKeyAsync(WgManagerConfig? config = null)
        {
            string wgPath = GetWgPath(config);
            var result = await ProcessRunner.RunAsync(wgPath, "genpsk");
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                throw new ExternalToolException(wgPath, "Failed to generate preshared key using 'wg genpsk'.",
                    result.ExitCode, result.StandardOutput, result.StandardError);
            }
            return result.StandardOutput.Trim();
        }

        // --- Pure C# Key Generation using NSec.Cryptography ---

        /// <summary>
        /// Generates a new WireGuard key pair (public and private) using NSec.Cryptography.
        /// This method does not rely on the 'wg' command-line tool.
        /// Requires the libsodium native library to be available at runtime for NSec.
        /// </summary>
        /// <returns>A KeyPair object containing the Base64 encoded private and public keys.</returns>
        public static KeyPair GenerateKeyPairPureCSharp()
        {
            try
            {
                // X25519 is used by WireGuard. NSec's KeyAgreementAlgorithm.X25519 is appropriate.
                // Creating a new key with NSec generates a random private key.
                using var key = NSec.Cryptography.Key.Create(NSec.Cryptography.KeyAgreementAlgorithm.X25519,
                                                             new NSec.Cryptography.KeyCreationParameters{ ExportPolicy = NSec.Cryptography.KeyExportPolicies.AllowPlaintextExport });

                byte[] privateKeyBytes = key.Export(NSec.Cryptography.KeyBlobFormat.RawPrivateKey); // libsodium's raw format for X25519 private key is 32 bytes
                byte[] publicKeyBytes = key.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey); // libsodium's raw format for X25519 public key is 32 bytes

                return new KeyPair(
                    Convert.ToBase64String(privateKeyBytes),
                    Convert.ToBase64String(publicKeyBytes)
                );
            }
            catch (Exception ex) // Catch potential exceptions from NSec, e.g., DllNotFoundException if libsodium is missing
            {
                throw new WireGuardManagerException("Failed to generate key pair using Pure C# method (NSec.Cryptography). Ensure libsodium native library is available.", ex);
            }
        }

        /// <summary>
        /// Generates a new WireGuard preshared key using NSec.Cryptography.
        /// This method does not rely on the 'wg' command-line tool.
        /// Requires the libsodium native library to be available at runtime for NSec.
        /// </summary>
        /// <returns>A Base64 encoded preshared key (32 random bytes).</returns>
        public static string GeneratePresharedKeyPureCSharp()
        {
            try
            {
                byte[] pskBytes = new byte[32];
                // Fill with cryptographically secure random bytes
                NSec.Cryptography.RandomGenerator.Default.GenerateBytes(pskBytes);
                return Convert.ToBase64String(pskBytes);
            }
            catch (Exception ex) // Catch potential exceptions from NSec
            {
                throw new WireGuardManagerException("Failed to generate preshared key using Pure C# method (NSec.Cryptography). Ensure libsodium native library is available.", ex);
            }
        }
    }
}
