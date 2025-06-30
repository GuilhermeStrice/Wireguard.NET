using NUnit.Framework;
using WireGuardManager;
using System.IO;
using System.Text.Json;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgManagerConfigTests
    {
        private string _testConfigDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestConfigs");
        private string _validConfigPath = null!;
        private string _malformedConfigPath = null!;
        private string _emptyConfigPath = null!;
        private string _nonExistentConfigPath = null!;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(_testConfigDir);

            _validConfigPath = Path.Combine(_testConfigDir, "valid_config.json");
            File.WriteAllText(_validConfigPath, JsonSerializer.Serialize(new { AllowSystemdManagement = true, SystemdServicePath = "/test/path" }));

            _malformedConfigPath = Path.Combine(_testConfigDir, "malformed_config.json");
            File.WriteAllText(_malformedConfigPath, "{ \"AllowSystemdManagement\": true, \"SystemdServicePath\": \"/test/path\""); // Missing closing brace

            _emptyConfigPath = Path.Combine(_testConfigDir, "empty_config.json");
            File.WriteAllText(_emptyConfigPath, "{}");

            _nonExistentConfigPath = Path.Combine(_testConfigDir, "non_existent_config.json");
            if(File.Exists(_nonExistentConfigPath)) File.Delete(_nonExistentConfigPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testConfigDir))
            {
                Directory.Delete(_testConfigDir, true);
            }
        }

        [Test]
        public void Load_ValidConfigFile_LoadsCorrectly()
        {
            var config = WgManagerConfig.Load(_validConfigPath);
            Assert.That(config.AllowSystemdManagement, Is.True);
            Assert.That(config.SystemdServicePath, Is.EqualTo("/test/path"));
        }

        [Test]
        public void Load_NonExistentConfigFile_ReturnsDefault()
        {
            var config = WgManagerConfig.Load(_nonExistentConfigPath);
            // Default values are false and /etc/systemd/system
            Assert.That(config.AllowSystemdManagement, Is.False);
            Assert.That(config.SystemdServicePath, Is.EqualTo("/etc/systemd/system"));
        }

        [Test]
        public void Load_MalformedConfigFile_ReturnsDefaultAndLogsWarning()
        {
            // Capture console output to check for warning (basic check)
            using var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            var config = WgManagerConfig.Load(_malformedConfigPath);

            Console.SetOut(originalOut); // Restore console output
            string output = sw.ToString();

            Assert.That(config.AllowSystemdManagement, Is.False);
            Assert.That(config.SystemdServicePath, Is.EqualTo("/etc/systemd/system"));
            Assert.That(output, Does.Contain("Error parsing configuration file").And.Does.Contain("Using default settings."));
        }

        [Test]
        public void Load_EmptyConfigFile_ReturnsDefaultValuesForMissingProperties()
        {
            var config = WgManagerConfig.Load(_emptyConfigPath);
            Assert.That(config.AllowSystemdManagement, Is.False); // Default from class definition
            Assert.That(config.SystemdServicePath, Is.EqualTo("/etc/systemd/system")); // Default from class definition
        }

        [Test]
        public void Load_NullPath_TriesToLoadFromAssemblyLocationAndReturnsDefaultIfMissing()
        {
            // This test assumes config.json is NOT next to the test DLL or is invalid there.
            // The default config.json copied to output for the main library might interfere
            // if tests are run from a location that sees it.
            // For a truly isolated test, one might need to run this in an environment
            // where the default config.json is guaranteed not to be present or is known.

            // To make it more robust, let's temporarily ensure the default path doesn't have a valid file
            // This is a bit hacky for a unit test.
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;
            var defaultConfigInTestAssemblyDir = Path.Combine(assemblyDirectory, "config.json");
            string? backupContent = null;
            bool backedUp = false;

            if (File.Exists(defaultConfigInTestAssemblyDir))
            {
                backedUp = true;
                backupContent = File.ReadAllText(defaultConfigInTestAssemblyDir);
                File.Delete(defaultConfigInTestAssemblyDir);
            }

            var config = WgManagerConfig.Load(null); // Pass null to trigger default path logic

            if (backedUp)
            {
                File.WriteAllText(defaultConfigInTestAssemblyDir, backupContent ?? "");
            }

            Assert.That(config.AllowSystemdManagement, Is.False);
            Assert.That(config.SystemdServicePath, Is.EqualTo("/etc/systemd/system"));
        }
    }
}
