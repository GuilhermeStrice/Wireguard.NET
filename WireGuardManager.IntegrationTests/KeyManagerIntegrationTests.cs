using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities; // For ValidationUtils
using System.Threading.Tasks;

namespace WireGuardManager.IntegrationTests
{
    [TestFixture]
    [Category("Integration")]
    public class KeyManagerIntegrationTests
    {
        [OneTimeSetUp]
        public void CheckPrerequisites()
        {
            // Reset static providers to ensure we are using the real implementations
            // This is important if unit tests using mocks ran in the same test execution process earlier.
            WgKeyManager.ProcessRunnerInstance = new ProcessRunner();
            WgKeyManager.FileSystemProvider = new StandardFileSystem();
            // WgManagerConfig.FileSystemProvider = new StandardFileSystem(); // WgKeyManager loads its own config for paths

            // Optional: Check if 'wg' command is truly available for wg-tool based tests.
            // If not, these specific tests could be marked inconclusive.
            // Pure C# tests should still run if libsodium is present.
            try
            {
                var result = WgKeyManager.ProcessRunnerInstance.RunAsync("wg", "--version", timeout: TimeSpan.FromSeconds(5)).Result;
                if (!result.Success && result.ExitCode != 0) // Some wg --version might exit non-zero
                {
                     var checkNoArg = WgKeyManager.ProcessRunnerInstance.RunAsync("wg","").Result;
                     if(!checkNoArg.Success && checkNoArg.ExitCode != 0) // wg by itself should print help and exit 0
                        TestContext.Progress.WriteLine("Warning: 'wg' command may not be fully functional. Tool-based key gen tests might fail.");
                }
            }
            catch (Exception ex)
            {
                TestContext.Progress.WriteLine($"Warning: Could not verify 'wg' command presence for tool-based tests. Error: {ex.Message}");
            }
        }

        [Test]
        public async Task GenerateKeyPairAsync_ToolBased_ReturnsValidKeyPair()
        {
            // This test relies on the 'wg' command being available in the environment (e.g., Docker container)
            var keyPair = await WgKeyManager.GenerateKeyPairAsync(); // Uses wg tool

            Assert.That(keyPair, Is.Not.Null, "KeyPair (Tool) should not be null.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PrivateKey), Is.True, $"Generated PrivateKey (Tool) format is invalid: {keyPair.PrivateKey}");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PublicKey), Is.True, $"Generated PublicKey (Tool) format is invalid: {keyPair.PublicKey}");
            Assert.That(keyPair.PrivateKey, Is.Not.EqualTo(keyPair.PublicKey));
        }

        [Test]
        public async Task GeneratePresharedKeyAsync_ToolBased_ReturnsValidKey()
        {
            // This test relies on the 'wg' command
            var psk = await WgKeyManager.GeneratePresharedKeyAsync(); // Uses wg tool

            Assert.That(ValidationUtils.IsValidWireGuardKey(psk), Is.True, $"Generated PresharedKey (Tool) format is invalid: {psk}");
        }

        [Test]
        public void GenerateKeyPairPureCSharp_ReturnsValidKeyPair()
        {
            // This test relies on libsodium being available for NSec
            var keyPair = WgKeyManager.GenerateKeyPairPureCSharp();

            Assert.That(keyPair, Is.Not.Null, "KeyPair (Pure C#) should not be null.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PrivateKey), Is.True, $"Generated PrivateKey (Pure C#) format is invalid: {keyPair.PrivateKey}");
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PublicKey), Is.True, $"Generated PublicKey (Pure C#) format is invalid: {keyPair.PublicKey}");
            Assert.That(keyPair.PrivateKey, Is.Not.EqualTo(keyPair.PublicKey));
        }

        [Test]
        public void GeneratePresharedKeyPureCSharp_ReturnsValidKey()
        {
            // This test relies on libsodium being available for NSec
            var psk = WgKeyManager.GeneratePresharedKeyPureCSharp();

            Assert.That(ValidationUtils.IsValidWireGuardKey(psk), Is.True, $"Generated PresharedKey (Pure C#) format is invalid: {psk}");
        }
    }
}
