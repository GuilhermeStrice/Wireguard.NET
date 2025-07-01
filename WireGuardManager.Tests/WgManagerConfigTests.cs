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
            Assert.That(WgLogging.MinimumLogLevel, Is.EqualTo(LogLevel.Info), "Global log level should remain default if config is missing.");
        }

        [TestCase("Debug", LogLevel.Debug)]
        [TestCase("warning", LogLevel.Warning)] // Case-insensitivity test
        [TestCase("ERROR", LogLevel.Error)]
        [TestCase("None", LogLevel.None)]
        [TestCase("Trace", LogLevel.Trace)]
        public async Task LoadAsync_ValidMinimumLogLevelInConfig_SetsGlobalLogLevel(string logLevelString, LogLevel expectedLevel)
        {
            var originalGlobalLogLevel = WgLogging.MinimumLogLevel; // Save to restore
            var mockFileSystem = new Mock<IFileSystem>();
            var tempConfigPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_loglevel_config.json");

            var configContent = $"{{\"minimumLogLevel\": \"{logLevelString}\"}}";
            mockFileSystem.Setup(fs => fs.FileExists(tempConfigPath)).Returns(true);
            mockFileSystem.Setup(fs => fs.ReadAllTextAsync(tempConfigPath)).ReturnsAsync(configContent);
            WgManagerConfig.FileSystemProvider = mockFileSystem.Object;

            await WgManagerConfig.LoadAsync(tempConfigPath);

            Assert.That(WgLogging.MinimumLogLevel, Is.EqualTo(expectedLevel));

            // Cleanup
            WgManagerConfig.FileSystemProvider = new StandardFileSystem(); // Reset provider
            WgLogging.MinimumLogLevel = originalGlobalLogLevel; // Reset global log level
        }

        [Test]
        public async Task LoadAsync_InvalidMinimumLogLevelInConfig_LogsWarningAndRetainsGlobalLogLevel()
        {
            var originalGlobalLogLevel = WgLogging.MinimumLogLevel;
            LogLevel initialLogLevelForTest = LogLevel.Debug; // Set a non-default to ensure it's not changed back to default
            WgLogging.MinimumLogLevel = initialLogLevelForTest;

            var mockFileSystem = new Mock<IFileSystem>();
            var tempConfigPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_invalid_loglevel_config.json");
            var configContent = "{\"minimumLogLevel\": \"InvalidValue\"}";

            mockFileSystem.Setup(fs => fs.FileExists(tempConfigPath)).Returns(true);
            mockFileSystem.Setup(fs => fs.ReadAllTextAsync(tempConfigPath)).ReturnsAsync(configContent);
            WgManagerConfig.FileSystemProvider = mockFileSystem.Object;

            // Capture log output
            var mockLogger = new Mock<IWgLoggingProvider>();
            var originalLogger = WgLogging.Logger;
            WgLogging.Logger = mockLogger.Object;

            await WgManagerConfig.LoadAsync(tempConfigPath);

            Assert.That(WgLogging.MinimumLogLevel, Is.EqualTo(initialLogLevelForTest), "Global log level should not change on invalid config value.");
            mockLogger.Verify(log => log.LogWarning(It.Is<string>(s => s.Contains("Invalid MinimumLogLevel value 'InvalidValue'"))), Times.Once);

            // Cleanup
            WgManagerConfig.FileSystemProvider = new StandardFileSystem();
            WgLogging.MinimumLogLevel = originalGlobalLogLevel;
            WgLogging.Logger = originalLogger;
        }

        [Test]
        public async Task LoadAsync_MissingMinimumLogLevelInConfig_RetainsGlobalLogLevel()
        {
            var originalGlobalLogLevel = WgLogging.MinimumLogLevel;
            LogLevel initialLogLevelForTest = LogLevel.Warning;
            WgLogging.MinimumLogLevel = initialLogLevelForTest;

            var mockFileSystem = new Mock<IFileSystem>();
            var tempConfigPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_missing_loglevel_config.json");
            var configContent = "{\"allowSystemdManagement\": true}"; // No minimumLogLevel field

            mockFileSystem.Setup(fs => fs.FileExists(tempConfigPath)).Returns(true);
            mockFileSystem.Setup(fs => fs.ReadAllTextAsync(tempConfigPath)).ReturnsAsync(configContent);
            WgManagerConfig.FileSystemProvider = mockFileSystem.Object;

            var mockLogger = new Mock<IWgLoggingProvider>();
            var originalLogger = WgLogging.Logger;
            WgLogging.Logger = mockLogger.Object;

            await WgManagerConfig.LoadAsync(tempConfigPath);

            Assert.That(WgLogging.MinimumLogLevel, Is.EqualTo(initialLogLevelForTest), "Global log level should not change if field is missing.");
            mockLogger.Verify(log => log.LogWarning(It.IsAny<string>()), Times.Never); // No warning should be logged for missing field

            // Cleanup
            WgManagerConfig.FileSystemProvider = new StandardFileSystem();
            WgLogging.MinimumLogLevel = originalGlobalLogLevel;
            WgLogging.Logger = originalLogger;
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task LoadAsync_EnableConsoleColorsInConfig_SetsStaticPropertyOnConsoleLoggingProvider(bool enableColors)
        {
            var originalColorSetting = ConsoleLoggingProvider.UseConsoleColors; // Save to restore
            var mockFileSystem = new Mock<IFileSystem>();
            var tempConfigPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_colors_config.json");

            var configContent = $"{{\"enableConsoleColors\": {enableColors.ToString().ToLower()}}}";
            mockFileSystem.Setup(fs => fs.FileExists(tempConfigPath)).Returns(true);
            mockFileSystem.Setup(fs => fs.ReadAllTextAsync(tempConfigPath)).ReturnsAsync(configContent);
            WgManagerConfig.FileSystemProvider = mockFileSystem.Object;

            await WgManagerConfig.LoadAsync(tempConfigPath);

            Assert.That(ConsoleLoggingProvider.UseConsoleColors, Is.EqualTo(enableColors));

            // Cleanup
            WgManagerConfig.FileSystemProvider = new StandardFileSystem();
            ConsoleLoggingProvider.UseConsoleColors = originalColorSetting;
        }

        [Test]
        public async Task LoadAsync_ValidTimeoutSettingsInConfig_LoadsTimeouts()
        {
            var mockFileSystem = new Mock<IFileSystem>();
            var tempConfigPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_timeouts_config.json");
            var configContent = @"{
                ""defaultShortOperationTimeoutSeconds"": 10,
                ""defaultLongOperationTimeoutSeconds"": 50
            }";
            mockFileSystem.Setup(fs => fs.FileExists(tempConfigPath)).Returns(true);
            mockFileSystem.Setup(fs => fs.ReadAllTextAsync(tempConfigPath)).ReturnsAsync(configContent);
            WgManagerConfig.FileSystemProvider = mockFileSystem.Object;

            var config = await WgManagerConfig.LoadAsync(tempConfigPath);

            Assert.That(config.DefaultShortOperationTimeoutSeconds, Is.EqualTo(10));
            Assert.That(config.DefaultLongOperationTimeoutSeconds, Is.EqualTo(50));

            WgManagerConfig.FileSystemProvider = new StandardFileSystem(); // Reset
        }

        [Test]
        public async Task LoadAsync_MissingTimeoutSettingsInConfig_TimeoutsAreNull()
        {
            var mockFileSystem = new Mock<IFileSystem>();
            var tempConfigPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_no_timeouts_config.json");
            var configContent = @"{ ""minimumLogLevel"": ""Debug"" }"; // Config without timeout settings
            mockFileSystem.Setup(fs => fs.FileExists(tempConfigPath)).Returns(true);
            mockFileSystem.Setup(fs => fs.ReadAllTextAsync(tempConfigPath)).ReturnsAsync(configContent);
            WgManagerConfig.FileSystemProvider = mockFileSystem.Object;

            var config = await WgManagerConfig.LoadAsync(tempConfigPath);

            Assert.That(config.DefaultShortOperationTimeoutSeconds, Is.Null);
            Assert.That(config.DefaultLongOperationTimeoutSeconds, Is.Null);

            WgManagerConfig.FileSystemProvider = new StandardFileSystem(); // Reset
        }
    }
}
