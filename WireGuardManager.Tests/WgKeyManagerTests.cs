using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Exceptions;
using WireGuardManager.Utilities;
using Moq;
using System.Threading.Tasks;
using System; // For DllNotFoundException if checking specific inner exceptions

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgKeyManagerTests
    {
        private Mock<IProcessRunner> _mockProcessRunner = null!;
        private Mock<IFileSystem> _mockFileSystem = null!;
        private WgManagerConfig _testConfig = null!; // For passing to methods if needed for path config

        // Example valid keys for mocking process output
        private const string MockPrivateKey = "PVTKEYPVTKEYPVTKEYPVTKEYPVTKEYPVTKEYPVTKEYAAA=";
        private const string MockPublicKey = "PUBKEYPUBKEYPUBKEYPUBKEYPUBKEYPUBKEYPUBKEYAAA=";
        private const string MockPsk = "PSKPSKPSKPSKPSKPSKPSKPSKPSKPSKPSKPSKPSKPSKAAA=";

        [SetUp]
        public void SetUp()
        {
            _mockProcessRunner = new Mock<IProcessRunner>();
            _mockFileSystem = new Mock<IFileSystem>();

            WgKeyManager.ProcessRunnerInstance = _mockProcessRunner.Object;
            WgKeyManager.FileSystemProvider = _mockFileSystem.Object;

            // _testConfig will be created per test as needed for timeout settings
        }

        [TearDown]
        public void TearDown()
        {
            // Reset static providers to default instances if necessary, or ensure each test sets them up.
            // For safety, reset them so other test fixtures aren't affected if they also use WgKeyManager.
            WgKeyManager.ProcessRunnerInstance = new ProcessRunner();
            WgKeyManager.FileSystemProvider = new StandardFileSystem();
        }

        [Test]
        public async Task GenerateKeyPairAsync_SuccessfulExecution_ReturnsValidKeyPair()
        {
            _mockProcessRunner.Setup(p => p.RunAsync("wg", "genkey", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPrivateKey + "\n", ""));

            _mockFileSystem.Setup(fs => fs.GetTempFileName()).Returns("temp_private_key_file");
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync("temp_private_key_file", MockPrivateKey)).Returns(Task.CompletedTask);

            // Mocking the shell command for `wg pubkey < tempfile`
            _mockProcessRunner.Setup(p => p.RunAsync("/bin/sh", It.Is<string>(s => s.Contains("wg pubkey < 'temp_private_key_file'")), null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPublicKey + "\n", ""));

            _mockFileSystem.Setup(fs => fs.FileExists("temp_private_key_file")).Returns(true); // For finally block
            _mockFileSystem.Setup(fs => fs.DeleteFile("temp_private_key_file"));


            var keyPair = await WgKeyManager.GenerateKeyPairAsync(_testConfig);

            Assert.That(keyPair, Is.Not.Null);
            Assert.That(keyPair.PrivateKey, Is.EqualTo(MockPrivateKey));
            Assert.That(keyPair.PublicKey, Is.EqualTo(MockPublicKey));
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PrivateKey));
            Assert.That(ValidationUtils.IsValidWireGuardKey(keyPair.PublicKey));

            _mockProcessRunner.Verify(p => p.RunAsync("wg", "genkey", null, It.IsAny<TimeSpan?>()), Times.Once);
            _mockFileSystem.Verify(fs => fs.GetTempFileName(), Times.Once);
            _mockFileSystem.Verify(fs => fs.WriteAllTextAsync("temp_private_key_file", MockPrivateKey), Times.Once);
            _mockProcessRunner.Verify(p => p.RunAsync("/bin/sh", It.Is<string>(s => s.Contains("wg pubkey < 'temp_private_key_file'")), null, It.IsAny<TimeSpan?>()), Times.Once);
            _mockFileSystem.Verify(fs => fs.DeleteFile("temp_private_key_file"), Times.Once);
        }

        [Test]
        public void GenerateKeyPairAsync_GenKeyFails_ThrowsExternalToolException()
        {
            _mockProcessRunner.Setup(p => p.RunAsync("wg", "genkey", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "", "genkey error"));

            var ex = Assert.ThrowsAsync<ExternalToolException>(() => WgKeyManager.GenerateKeyPairAsync(_testConfig));
            Assert.That(ex.ToolName, Is.EqualTo("wg"));
            Assert.That(ex.Message, Does.Contain("Failed to generate private key"));
            Assert.That(ex.StandardError, Is.EqualTo("genkey error"));
        }

        [Test]
        public void GenerateKeyPairAsync_PubKeyFails_ThrowsExternalToolException()
        {
            _mockProcessRunner.Setup(p => p.RunAsync("wg", "genkey", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPrivateKey + "\n", ""));
            _mockFileSystem.Setup(fs => fs.GetTempFileName()).Returns("temp_pk_file");
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync("temp_pk_file", MockPrivateKey)).Returns(Task.CompletedTask);
            _mockProcessRunner.Setup(p => p.RunAsync("/bin/sh", It.Is<string>(s => s.Contains("wg pubkey < 'temp_pk_file'")), null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "", "pubkey error from tempfile")); // First pubkey attempt fails

            // Mock the fallback echo pipe attempt to also fail
            _mockProcessRunner.Setup(p => p.RunAsync("/bin/sh", It.Is<string>(s => s.Contains($"echo '{MockPrivateKey}' | 'wg' pubkey")), null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "", "pubkey error from echo"));


            var ex = Assert.ThrowsAsync<ExternalToolException>(() => WgKeyManager.GenerateKeyPairAsync(_testConfig));
            Assert.That(ex.ToolName, Is.EqualTo("wg"));
            Assert.That(ex.Message, Does.Contain("Failed to generate public key"));
            Assert.That(ex.Message, Does.Contain("Both temp file and echo pipe methods failed"));
            Assert.That(ex.StandardError, Is.EqualTo("pubkey error from echo"));
        }


        [Test]
        public async Task GeneratePresharedKeyAsync_SuccessfulExecution_ReturnsValidKey()
        {
            _mockProcessRunner.Setup(p => p.RunAsync("wg", "genpsk", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPsk + "\n", ""));

            string psk = await WgKeyManager.GeneratePresharedKeyAsync(_testConfig);

            Assert.That(psk, Is.EqualTo(MockPsk));
            Assert.That(ValidationUtils.IsValidWireGuardKey(psk));
            _mockProcessRunner.Verify(p => p.RunAsync("wg", "genpsk", null, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public void GeneratePresharedKeyAsync_GenPskFails_ThrowsExternalToolException()
        {
            _testConfig = new WgManagerConfig(); // Default config (no custom timeouts)
            _mockProcessRunner.Setup(p => p.RunAsync("wg", "genpsk", null, Utilities.ProcessRunner.DefaultShortOperationTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(1, "", "genpsk error"));

            var ex = Assert.ThrowsAsync<ExternalToolException>(() => WgKeyManager.GeneratePresharedKeyAsync(_testConfig));
            Assert.That(ex.ToolName, Is.EqualTo("wg"));
            Assert.That(ex.Message, Does.Contain("Failed to generate preshared key"));
            Assert.That(ex.StandardError, Is.EqualTo("genpsk error"));
        }

        [Test]
        public async Task GenerateKeyPairAsync_UsesConfiguredTimeout()
        {
            _testConfig = new WgManagerConfig { DefaultShortOperationTimeoutSeconds = 5 };
            TimeSpan expectedTimeout = TimeSpan.FromSeconds(5);

            _mockProcessRunner.Setup(p => p.RunAsync("wg", "genkey", null, expectedTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPrivateKey + "\n", ""));
            _mockFileSystem.Setup(fs => fs.GetTempFileName()).Returns("temp_file_timeout");
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync("temp_file_timeout", MockPrivateKey)).Returns(Task.CompletedTask);
            _mockProcessRunner.Setup(p => p.RunAsync("/bin/sh", It.Is<string>(s => s.Contains("wg pubkey < 'temp_file_timeout'")), null, expectedTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPublicKey + "\n", ""));
            _mockFileSystem.Setup(fs => fs.FileExists("temp_file_timeout")).Returns(true);
            _mockFileSystem.Setup(fs => fs.DeleteFile("temp_file_timeout"));

            await WgKeyManager.GenerateKeyPairAsync(_testConfig);

            _mockProcessRunner.Verify(p => p.RunAsync("wg", "genkey", null, expectedTimeout), Times.Once);
            _mockProcessRunner.Verify(p => p.RunAsync("/bin/sh", It.Is<string>(s => s.Contains("wg pubkey < 'temp_file_timeout'")), null, expectedTimeout), Times.Once);
        }

        [Test]
        public async Task GenerateKeyPairAsync_UsesStaticDefaultTimeout_WhenConfigNotSet()
        {
             _testConfig = new WgManagerConfig { DefaultShortOperationTimeoutSeconds = null }; // Explicitly null
            TimeSpan expectedTimeout = Utilities.ProcessRunner.DefaultShortOperationTimeout;

            _mockProcessRunner.Setup(p => p.RunAsync("wg", "genkey", null, expectedTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPrivateKey + "\n", ""));
            _mockFileSystem.Setup(fs => fs.GetTempFileName()).Returns("temp_file_static_timeout");
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync("temp_file_static_timeout", MockPrivateKey)).Returns(Task.CompletedTask);
            _mockProcessRunner.Setup(p => p.RunAsync("/bin/sh", It.Is<string>(s => s.Contains("wg pubkey < 'temp_file_static_timeout'")), null, expectedTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, MockPublicKey + "\n", ""));
             _mockFileSystem.Setup(fs => fs.FileExists("temp_file_static_timeout")).Returns(true);
            _mockFileSystem.Setup(fs => fs.DeleteFile("temp_file_static_timeout"));

            await WgKeyManager.GenerateKeyPairAsync(_testConfig); // Pass config with null timeout

            _mockProcessRunner.Verify(p => p.RunAsync("wg", "genkey", null, expectedTimeout), Times.Once);
        }


        // Tests for Pure C# methods (these do not use IProcessRunner or IFileSystem directly from WgKeyManager)
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
