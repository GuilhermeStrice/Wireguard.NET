using NUnit.Framework;
using WireGuardManager;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgKeyManagerTests
    {
        // These tests require 'wg' command to be available in PATH
        // And that the environment allows process execution.
        // They are more integration-style tests for this component.

        [Test]
        public async Task GenerateKeyPairAsync_ReturnsValidKeyPair()
        {
            WgKeyManager.KeyPair keyPair = null;
            try
            {
                keyPair = await WgKeyManager.GenerateKeyPairAsync();
            }
            catch (System.Exception ex)
            {
                Assert.Inconclusive($"'wg' command might not be available or executable: {ex.Message}");
            }

            Assert.That(keyPair, Is.Not.Null, "KeyPair should not be null.");
            Assert.That(keyPair.PrivateKey, Is.Not.Null.Or.Empty, "PrivateKey should not be null or empty.");
            Assert.That(keyPair.PublicKey, Is.Not.Null.Or.Empty, "PublicKey should not be null or empty.");

            // WireGuard keys are 44 characters long, Base64 encoded.
            // Example: abcdefghijklmnopqrstuvwxyzABCDEFGHIJKL123456=
            string base64Pattern = @"^[A-Za-z0-9+/]{43}=$";
            Assert.That(Regex.IsMatch(keyPair.PrivateKey, base64Pattern), Is.True, $"PrivateKey format is invalid: {keyPair.PrivateKey}");
            Assert.That(Regex.IsMatch(keyPair.PublicKey, base64Pattern), Is.True, $"PublicKey format is invalid: {keyPair.PublicKey}");
        }

        [Test]
        public async Task GeneratePresharedKeyAsync_ReturnsValidKey()
        {
            string psk = null;
            try
            {
                psk = await WgKeyManager.GeneratePresharedKeyAsync();
            }
            catch (System.Exception ex)
            {
                 Assert.Inconclusive($"'wg' command might not be available or executable: {ex.Message}");
            }

            Assert.That(psk, Is.Not.Null.Or.Empty, "PresharedKey should not be null or empty.");

            string base64Pattern = @"^[A-Za-z0-9+/]{43}=$";
            Assert.That(Regex.IsMatch(psk, base64Pattern), Is.True, $"PresharedKey format is invalid: {psk}");
        }
    }
}
