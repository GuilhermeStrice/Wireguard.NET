using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Exceptions; // Added
using System.Threading.Tasks;
using WireGuardManager.Utilities; // For ValidationUtils

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgKeyManagerTests
    {
        // These tests require 'wg' command to be available in PATH.
        // They act as integration tests for this component.

        [OneTimeSetUp]
        public void CheckWgCommand()
        {
            try
            {
                // A quick check to see if 'wg' is likely available.
                // ProcessRunner.RunAsync itself will throw CommandNotFoundException if 'wg' isn't found.
                var result = ProcessRunner.RunAsync("wg", "--version").Result; // Simple, fast wg command
                if (!result.Success && result.ExitCode !=0) // wg --version might return non-zero if no args given after it by some versions
                {
                    // wg with no args typically exits 0 and prints help.
                    // if `wg --version` fails spectacularly, then `wg` is probably not right.
                     var checkNoArg = ProcessRunner.RunAsync("wg","").Result;
                     if(!checkNoArg.Success)
                        Assert.Inconclusive("'wg' command does not seem to be installed or working correctly. Skipping WgKeyManagerTests.");
                }
            }
            catch (CommandNotFoundException)
            {
                Assert.Inconclusive("'wg' command not found. Skipping WgKeyManagerTests.");
            }
            catch (System.Exception ex) // Catch other startup issues
            {
                 Assert.Inconclusive($"Could not verify 'wg' command presence. Skipping WgKeyManagerTests. Error: {ex.Message}");
            }
        }


        [Test]
        public async Task GenerateKeyPairAsync_ReturnsValidKeyPair()
        {
            WgKeyManager.KeyPair keyPair = await WgKeyManager.GenerateKeyPairAsync();

            Assert.That(keyPair, Is.Not.Null, "KeyPair should not be null.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PrivateKey), Is.True, $"Generated PrivateKey format is invalid: {keyPair.PrivateKey}");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PublicKey), Is.True, $"Generated PublicKey format is invalid: {keyPair.PublicKey}");
        }

        [Test]
        public async Task GeneratePresharedKeyAsync_ReturnsValidKey()
        {
            string psk = await WgKeyManager.GeneratePresharedKeyAsync();

            Assert.That(ValidationUtils.IsValidWireGuardKey(psk), Is.True, $"Generated PresharedKey (async) format is invalid: {psk}");
        }

        // Tests for Pure C# methods
        [Test]
        public void GenerateKeyPairPureCSharp_ReturnsValidKeyPair()
        {
            WgKeyManager.KeyPair keyPair;
            try
            {
                keyPair = WgKeyManager.GenerateKeyPairPureCSharp();
            }
            catch (WireGuardManagerException ex) when (ex.InnerException is DllNotFoundException || ex.Message.Contains("libsodium"))
            {
                Assert.Inconclusive($"NSec.Cryptography (libsodium) native dependency not found. Skipping Pure C# test. Error: {ex.Message}");
                return;
            }

            Assert.That(keyPair, Is.Not.Null, "KeyPair (Pure C#) should not be null.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PrivateKey), Is.True, $"Generated PrivateKey (Pure C#) format is invalid: {keyPair.PrivateKey}");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PublicKey), Is.True, $"Generated PublicKey (Pure C#) format is invalid: {keyPair.PublicKey}");
            Assert.That(keyPair.PrivateKey, Is.Not.EqualTo(keyPair.PublicKey));
        }

        [Test]
        public void GeneratePresharedKeyPureCSharp_ReturnsValidKey()
        {
            string psk;
            try
            {
                psk = WgKeyManager.GeneratePresharedKeyPureCSharp();
            }
            catch (WireGuardManagerException ex) when (ex.InnerException is DllNotFoundException || ex.Message.Contains("libsodium"))
            {
                Assert.Inconclusive($"NSec.Cryptography (libsodium) native dependency not found. Skipping Pure C# test. Error: {ex.Message}");
                return;
            }

            Assert.That(ValidationUtils.IsValidWireGuardKey(psk), Is.True, $"Generated PresharedKey (Pure C#) format is invalid: {psk}");
        }
    }
}
