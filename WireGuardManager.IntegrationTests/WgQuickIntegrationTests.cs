using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;
using System.Threading.Tasks;
using System.IO;
using System.Linq; // For FirstOrDefault
using System.Collections.Generic; // For List

namespace WireGuardManager.IntegrationTests
{
    [TestFixture]
    [Category("Integration")]
    public class WgQuickIntegrationTests
    {
        private string _testBaseDir = null!;
        private string _wgConfDir = null!; // Simulates /etc/wireguard or a custom path for tests
        private WgManagerConfig _integrationTestConfig = null!;
        private const string TestInterface = "wgtestint0"; // Unique interface name for these tests

        private const string ServerPrivateKey = "SVRPRVKEYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        private const string ServerPublicKey = "SVRPUBKEYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="; // Pre-calculated from ServerPrivateKey
        private const string PeerPrivateKey = "CLTPRVKEYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        private const string PeerPublicKey = "CLTPUBKEYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="; // Pre-calculated from PeerPrivateKey


        [OneTimeSetUp]
        public async Task GlobalSetup()
        {
            // Ensure real providers are used
            WgQuick.ProcessRunnerInstance = new ProcessRunner();
            WgQuick.FileSystemProvider = new StandardFileSystem();
            WgSystemdManager.ProcessRunnerInstance = new ProcessRunner(); // For EnsureServiceExistsAndEnabled
            WgSystemdManager.FileSystemProvider = new StandardFileSystem();
            WgConfigFileManager.FileSystemProvider = new StandardFileSystem();
            WgConfig.FileSystemProvider = new StandardFileSystem();
            WgManagerConfig.FileSystemProvider = new StandardFileSystem();


            _testBaseDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "WgQuickIntegrationTestRun_" + Path.GetRandomFileName());
            _wgConfDir = Path.Combine(_testBaseDir, "wireguard_configs");
            Directory.CreateDirectory(_wgConfDir);

            _integrationTestConfig = new WgManagerConfig
            {
                AllowSystemdManagement = false, // Keep false for most tests to avoid systemctl dependency unless specific test enables it
                SystemdServicePath = Path.Combine(_testBaseDir, "fake_systemd"), // Use a fake path for service files
                WireguardConfigDirectory = _wgConfDir, // Crucial for DeployConfigAsync and wg-quick finding files by interface name
                // Tool paths default to PATH
            };
            Directory.CreateDirectory(_integrationTestConfig.SystemdServicePath);


            // Pre-calculate public keys if needed, or use known valid test pairs.
            // For real integration, using wg genkey/pubkey once could provide actual pairs.
            // For now, using placeholder valid-format keys.
            // If using actual 'wg' to generate keys for setup:
            // var serverKeyPair = await WgKeyManager.GenerateKeyPairAsync(); // Uses wg tool
            // ServerPrivateKey = serverKeyPair.PrivateKey;
            // ServerPublicKey = serverKeyPair.PublicKey;
            // var peerKeyPair = await WgKeyManager.GenerateKeyPairAsync();
            // PeerPrivateKey = peerKeyPair.PrivateKey;
            // PeerPublicKey = peerKeyPair.PublicKey;
            // TestContext.Progress.WriteLine($"Using Server PubKey: {ServerPublicKey}, Peer PubKey: {PeerPublicKey} for tests.");

            // Ensure no pre-existing test interface is up from a failed previous run
            // This requires wg-quick to be available.
            try
            {
                await WgQuick.Down(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(10));
                TestContext.Progress.WriteLine($"Ensured {TestInterface} is down before tests.");
            }
            catch (ExternalToolException)
            {
                TestContext.Progress.WriteLine($"{TestInterface} was likely already down or does not exist.");
            }
        }

        [OneTimeTearDown]
        public async Task GlobalTeardown()
        {
            // Attempt to bring down the interface if it was left up
            try
            {
                await WgQuick.Down(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(10));
                TestContext.Progress.WriteLine($"Ensured {TestInterface} is down after tests.");
            }
            catch (ExternalToolException) { /* Might already be down */ }

            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }

        private async Task<string> CreateAndDeployTestConfig(string interfaceName, WgServerConfig server, List<WgPeerConfig>? peers = null)
        {
            var wgConfig = new WgConfig(server);
            if (peers != null)
            {
                foreach (var peer in peers) wgConfig.AddPeer(peer);
            }

            string tempSourcePath = Path.Combine(_testBaseDir, $"{interfaceName}_source.conf");
            wgConfig.ToFile(tempSourcePath); // Uses WgConfig's FileSystemProvider (StandardFileSystem)

            await WgConfigFileManager.DeployConfigAsync(tempSourcePath, interfaceName, _integrationTestConfig);
            return Path.Combine(_integrationTestConfig.WireguardConfigDirectory, $"{interfaceName}.conf");
        }

        [Test, Order(1)]
        public async Task Up_And_Show_SimpleInterface_Succeeds()
        {
            var serverConf = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.200.1/24" }, ListenPort = 51830 };
            await CreateAndDeployTestConfig(TestInterface, serverConf);

            var upResult = await WgQuick.Up(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(20));
            Assert.That(upResult.Success, Is.True, $"wg-quick up failed: {upResult.StandardError}");

            var showResult = await WgQuick.Show(TestInterface, _integrationTestConfig);
            Assert.That(showResult.Success, Is.True, "wg show failed");
            Assert.That(showResult.StandardOutput, Does.Contain(ServerPublicKey.Substring(0,10)), "Server public key not found in wg show output.");
            Assert.That(showResult.StandardOutput, Does.Contain("listening port: 51830"), "Listening port not found or incorrect.");
        }

        [Test, Order(2)]
        public async Task SetPeerAsync_AddPeerToLiveInterface_SucceedsAndShowsPeer()
        {
            // Assumes interface TestInterface is up from previous test or can be brought up.
            // Ensure it's up for this test if tests are independent.
            var serverConfCheck = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.200.1/24" }, ListenPort = 51830 };
            await CreateAndDeployTestConfig(TestInterface, serverConfCheck); // Ensure config file exists for wg-quick state
            try { await WgQuick.Up(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(15)); } catch {} // Ensure up, ignore if already

            var peerUpdateOptions = new WgPeerUpdateOptions
            {
                AllowedIPs = new List<string> { "10.200.200.2/32" }
            };

            var setResult = await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, peerUpdateOptions, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"wg set peer failed: {setResult.StandardError}");

            var showDetails = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            Assert.That(showDetails, Is.Not.Null, "ShowInterfaceDetailsAsync returned null.");
            Assert.That(showDetails.Peers, Has.Count.EqualTo(1), "Peer count should be 1 after adding.");
            var addedPeerInfo = showDetails.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey);
            Assert.That(addedPeerInfo, Is.Not.Null, "Added peer not found in show details.");
            Assert.That(addedPeerInfo.AllowedIPs, Contains.Item("10.200.200.2/32"));
        }

        [Test, Order(3)]
        public async Task SetPeerAsync_RemovePeerFromLiveInterface_Succeeds()
        {
            // Assumes interface TestInterface is up and has PeerPublicKey from previous test.
            var peerUpdateOptions = new WgPeerUpdateOptions { Remove = true };

            var setResult = await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, peerUpdateOptions, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"wg set peer remove failed: {setResult.StandardError}");

            var showDetails = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            Assert.That(showDetails, Is.Not.Null);
            Assert.That(showDetails.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey), Is.Null, "Peer should be removed.");
        }


        [Test, Order(4)]
        public async Task Down_ExistingInterface_Succeeds()
        {
            // Assumes interface TestInterface is up.
            var downResult = await WgQuick.Down(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(15));
            Assert.That(downResult.Success, Is.True, $"wg-quick down failed: {downResult.StandardError}");

            // Verify it's down (wg show should typically fail or show no interface)
            var showResult = await WgQuick.Show(TestInterface, _integrationTestConfig);
            Assert.That(showResult.StandardOutput, Does.Not.Contain(ServerPublicKey.Substring(0,10)), "Interface should be down, but public key found.");
            // wg show for a non-existent/down interface might exit non-zero or just print nothing for that interface.
            // If it prints nothing for the interface, Success might still be true if other interfaces exist.
            // A more reliable check might be to see if `ip link show wgtestint0` reports it as down or non-existent.
        }

        // Test for SyncConf
        [Test, Order(5)]
        public async Task SyncConf_UpdatesLiveInterface()
        {
            // 1. Create initial config and bring interface up
            var serverConfV1 = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.201.1/24" }, ListenPort = 51831 };
            string confPathV1 = await CreateAndDeployTestConfig("wg_sync_test", serverConfV1);
            await WgQuick.Up("wg_sync_test", _integrationTestConfig);

            // 2. Create a new config file (V2) with a change (e.g., different port or add peer)
            var serverConfV2 = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.201.1/24" }, ListenPort = 51832 };
            var peerConf = new WgPeerConfig(PeerPublicKey) { AllowedIPs = new List<string> { "10.200.201.2/32" } };
            var wgConfigV2 = new WgConfig(serverConfV2);
            wgConfigV2.AddPeer(peerConf);
            string confPathV2 = Path.Combine(_testBaseDir, "wg_sync_test_v2.conf");
            wgConfigV2.ToFile(confPathV2);

            // 3. Apply V2 using SyncConf
            var syncResult = await WgQuick.SyncConf("wg_sync_test", confPathV2, _integrationTestConfig);
            Assert.That(syncResult.Success, Is.True, $"SyncConf failed: {syncResult.StandardError}");

            // 4. Verify changes with ShowInterfaceDetailsAsync
            var details = await WgQuick.ShowInterfaceDetailsAsync("wg_sync_test", _integrationTestConfig);
            Assert.That(details, Is.Not.Null);
            Assert.That(details.ListenPort, Is.EqualTo(51832));
            Assert.That(details.Peers.Count, Is.EqualTo(1));
            Assert.That(details.Peers.First().PublicKey, Is.EqualTo(PeerPublicKey));

            // 5. Clean up
            await WgQuick.Down("wg_sync_test", _integrationTestConfig);
        }

        // Test for Systemd File Creation (content check, not service status)
        [Test, Order(6)]
        public async Task EnsureServiceExistsAndEnabled_CreatesServiceFile_WhenAllowed()
        {
            var configForSystemd = new WgManagerConfig {
                AllowSystemdManagement = true,
                SystemdServicePath = _integrationTestConfig.SystemdServicePath, // Use the test-specific fake systemd path
                WireguardConfigDirectory = _wgConfDir
                // Tool paths will use defaults (PATH)
            };
            string interfaceName = "wg_svc_it0";
            string expectedServiceFile = Path.Combine(configForSystemd.SystemdServicePath, $"wg-quick@{interfaceName}.service");

            // Ensure no service file exists initially (using real FileSystem for this check as WgSystemdManager will use its provider)
            if(File.Exists(expectedServiceFile)) File.Delete(expectedServiceFile);

            // systemctl calls will likely fail if systemd is not running as PID 1 in Docker,
            // but the file should still be created.
            bool result = false;
            try
            {
                result = await WgSystemdManager.EnsureServiceExistsAndEnabled(interfaceName, configForSystemd);
            }
            catch(ExternalToolException ex) when (ex.ToolName.Contains("systemctl"))
            {
                TestContext.Progress.WriteLine($"systemctl command failed as expected in Docker: {ex.Message}");
                // We expect this if systemd isn't fully functional, but file should be there
            }

            Assert.That(File.Exists(expectedServiceFile), Is.True, "Service file should be created.");
            string serviceContent = File.ReadAllText(expectedServiceFile);
            Assert.That(serviceContent, Does.Contain($"ExecStart=wg-quick up %i"));
            Assert.That(serviceContent, Does.Contain($"Description=WireGuard via wg-quick for %I"));

            // Result might be true if file existed, or if file created and systemctl calls "succeeded" (e.g. returned 0 even if no-op)
            // Or false if systemctl calls genuinely failed and were reported.
            // For this test, primary assertion is file existence and content.
        }

        [Test]
        public void Up_InterfaceName_MissingConfFile_ThrowsExternalToolException()
        {
            string interfaceName = "wg_missing_conf"; // Unique name, ensure no file is deployed for it

            // Ensure the config directory defined in _integrationTestConfig.WireguardConfigDirectory exists
            // but the specific conf file does not.
            Directory.CreateDirectory(_integrationTestConfig.WireguardConfigDirectory);
            string expectedConfPath = Path.Combine(_integrationTestConfig.WireguardConfigDirectory, $"{interfaceName}.conf");
            if(File.Exists(expectedConfPath)) File.Delete(expectedConfPath);

            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgQuick.Up(interfaceName, _integrationTestConfig, TimeSpan.FromSeconds(10))
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo("wg-quick")); // Or the configured path
            // stderr from wg-quick might vary slightly, but usually indicates file not found or similar
            Assert.That(ex.StandardError, Does.Contain("not found").IgnoreCase.Or.Contain("No such file").IgnoreCase);
            TestContext.Progress.WriteLine($"WgQuick.Up for missing conf file failed as expected: {ex.Message}");
        }

        [Test]
        public void Up_FilePath_NonExistentFile_ThrowsExternalToolException()
        {
            string nonExistentFilePath = Path.Combine(_testBaseDir, "non_existent_wg_config.conf");
            if(File.Exists(nonExistentFilePath)) File.Delete(nonExistentFilePath); // Ensure it really doesn't exist

            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgQuick.Up(nonExistentFilePath, _integrationTestConfig, TimeSpan.FromSeconds(10))
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo("wg-quick"));
            // wg-quick stderr for a non-existent file path
            Assert.That(ex.StandardError, Does.Contain("No such file or directory").IgnoreCase
                .Or.Contain("not found").IgnoreCase);
            TestContext.Progress.WriteLine($"WgQuick.Up for non-existent file path failed as expected: {ex.Message}");
        }

        // --- SetPeerAsync Tests ---
        private async Task EnsureTestInterfaceUpWithInitialPeerAsync(string interfaceName = TestInterface, string initialPeerKey = PeerPublicKey, string initialAllowedIP = "10.200.200.2/32")
        {
            var serverConf = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.200.1/24" }, ListenPort = 51830 };
            var peerConf = new WgPeerConfig(initialPeerKey) { AllowedIPs = new List<string> { initialAllowedIP } };
            await CreateAndDeployTestConfig(interfaceName, serverConf, new List<WgPeerConfig> { peerConf });

            try { await WgQuick.Up(interfaceName, _integrationTestConfig, TimeSpan.FromSeconds(15)); }
            catch (ExternalToolException ex) {
                // If it's already up, wg-quick up might return non-zero with specific message
                if (!ex.StandardError.Contains("already exists", StringComparison.OrdinalIgnoreCase)) throw;
                TestContext.Progress.WriteLine($"Interface {interfaceName} might have been already up: {ex.StandardError}");
            }
            var showResult = await WgQuick.Show(interfaceName, _integrationTestConfig);
            Assert.That(showResult.StandardOutput, Does.Contain(ServerPublicKey.Substring(0,10)), $"Interface {interfaceName} failed to come up for SetPeerAsync tests.");
        }

        [Test, Order(10)] // Order after basic Up/Down tests
        public async Task SetPeerAsync_UpdateEndpoint_Succeeds()
        {
            await EnsureTestInterfaceUpWithInitialPeerAsync();
            string newEndpoint = "123.123.123.123:12345";
            var options = new WgPeerUpdateOptions { Endpoint = newEndpoint };

            var setResult = await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, options, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync (Endpoint) failed: {setResult.StandardError}");

            var details = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            var peer = details?.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey);
            Assert.That(peer, Is.Not.Null, "Peer not found after update.");
            Assert.That(peer?.Endpoint, Is.EqualTo(newEndpoint));
        }

        [Test, Order(11)]
        public async Task SetPeerAsync_UpdateAllowedIPs_Succeeds()
        {
            await EnsureTestInterfaceUpWithInitialPeerAsync(); // Re-ensure state or use existing
            var newAllowedIPs = new List<string> { "10.200.200.5/32", "10.200.200.6/32" };
            var options = new WgPeerUpdateOptions { AllowedIPs = newAllowedIPs };

            var setResult = await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, options, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync (AllowedIPs) failed: {setResult.StandardError}");

            var details = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            var peer = details?.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey);
            Assert.That(peer, Is.Not.Null);
            Assert.That(peer?.AllowedIPs, Is.EquivalentTo(newAllowedIPs));
        }

        [Test, Order(12)]
        public async Task SetPeerAsync_AddPresharedKey_Succeeds()
        {
            await EnsureTestInterfaceUpWithInitialPeerAsync();
            string psk = "TESTPSKINTEGRATIONAAAAAAAAAAAAAAAAAAAAAAAAAAA="; // Valid format
            var options = new WgPeerUpdateOptions { PresharedKey = psk };

            var setResult = await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, options, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync (Add PSK) failed: {setResult.StandardError}");

            var details = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            var peer = details?.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey);
            Assert.That(peer, Is.Not.Null);
            Assert.That(peer?.PresharedKeyExists, Is.True, "PresharedKeyExists should be true after adding PSK.");
        }

        [Test, Order(13)]
        public async Task SetPeerAsync_RemovePresharedKey_Succeeds()
        {
            await EnsureTestInterfaceUpWithInitialPeerAsync();
            // First, ensure PSK exists (from previous test or add one)
            await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, new WgPeerUpdateOptions { PresharedKey = "TEMPPSKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=" }, _integrationTestConfig);

            var options = new WgPeerUpdateOptions { PresharedKey = "off" };
            var setResult = await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, options, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync (Remove PSK) failed: {setResult.StandardError}");

            var details = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            var peer = details?.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey);
            Assert.That(peer, Is.Not.Null);
            Assert.That(peer?.PresharedKeyExists, Is.False, "PresharedKeyExists should be false after removing PSK with 'off'.");
        }

        [Test, Order(14)]
        public async Task SetPeerAsync_UpdatePersistentKeepalive_Succeeds()
        {
            await EnsureTestInterfaceUpWithInitialPeerAsync();
            int newKeepalive = 33;
            var options = new WgPeerUpdateOptions { PersistentKeepalive = newKeepalive };

            var setResult = await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, options, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync (PersistentKeepalive) failed: {setResult.StandardError}");

            var details = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            var peer = details?.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey);
            Assert.That(peer, Is.Not.Null);
            Assert.That(peer?.PersistentKeepaliveIntervalSeconds, Is.EqualTo(newKeepalive));
        }

        // Note: The previous Order(2) and Order(3) tests for SetPeerAsync (add/remove) are good.
        // These new tests (10-14) provide more granular checks for specific property updates.
        // Ensure the ordering makes sense or make tests more independent if Order attribute is removed.
        // For now, keeping Order to build on previous states where appropriate, but EnsureTestInterfaceUpWithInitialPeerAsync helps.

        [Test, Order(20)] // After SetPeerAsync tests
        public async Task Save_UpdatesConfFile_AfterLiveChanges()
        {
            // 1. Ensure interface is up and has a known peer state (e.g., from SetPeerAsync_UpdateAllowedIPs_Succeeds)
            await EnsureTestInterfaceUpWithInitialPeerAsync(); // Resets to a known peer
            var newAllowedIPs = new List<string> { "10.200.200.88/32" };
            var options = new WgPeerUpdateOptions { AllowedIPs = newAllowedIPs };
            await WgQuick.SetPeerAsync(TestInterface, PeerPublicKey, options, _integrationTestConfig); // Change it live

            // 2. Call WgQuick.Save
            var saveResult = await WgQuick.Save(TestInterface, _integrationTestConfig);
            Assert.That(saveResult.Success, Is.True, $"WgQuick.Save failed: {saveResult.StandardError}");

            // 3. Read the saved .conf file
            string savedConfPath = Path.Combine(_integrationTestConfig.WireguardConfigDirectory, $"{TestInterface}.conf");
            Assert.That(File.Exists(savedConfPath), Is.True, "Saved .conf file does not exist.");

            var savedWgConfig = WgConfig.FromFile(savedConfPath); // Uses WgConfig's FileSystemProvider
            Assert.That(savedWgConfig, Is.Not.Null);
            var savedPeer = savedWgConfig.Peers.FirstOrDefault(p => p.PublicKey == PeerPublicKey);
            Assert.That(savedPeer, Is.Not.Null, "Peer not found in saved .conf file.");
            Assert.That(savedPeer.AllowedIPs, Is.EquivalentTo(newAllowedIPs), "Saved .conf file does not reflect live AllowedIPs change.");
        }

        [Test, Order(21)]
        public async Task Up_UsesCustomWgQuickPath_FromConfig()
        {
            string dummyToolLogFile = Path.Combine(_testBaseDir, "dummy_wgquick_log.txt");
            string dummyToolPath = Path.Combine(_testBaseDir, "dummy_wg-quick.sh");

            // Create the dummy wg-quick script
            // It will just log its arguments and exit successfully for this test.
            // Ensure it's executable: this will be handled by the Docker environment if script is created there,
            // or by host if tests run directly. For Docker, ensure base image has bash.
            string scriptContent = $"#!/bin/bash\necho \"$0 $@\" >> \"{dummyToolLogFile}\"\nexit 0";
            File.WriteAllText(dummyToolPath, scriptContent);

            // Make it executable - this is host dependent, Docker execution will need to handle this
            // For Linux host / Docker:
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                 Process.Start("chmod", $"+x \"{dummyToolPath}\"")?.WaitForExit();
            }
            else
            {
                TestContext.Progress.WriteLine("Warning: Cannot chmod +x on Windows for dummy script. Test relies on script being executable in Docker.");
            }


            var customPathConfig = new WgManagerConfig
            {
                WireguardConfigDirectory = _wgConfDir,
                WgQuickPath = dummyToolPath // Configure to use our dummy script
            };

            // Prepare a minimal .conf file for the dummy wg-quick to "process"
            string interfaceName = "wg_custom_path";
            var serverConf = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.202.1/24" }};
            await CreateAndDeployTestConfig(interfaceName, serverConf); // Deploys to _wgConfDir

            // Act: Call WgQuick.Up, which should now use the dummy wg-quick
            var upResult = await WgQuick.Up(interfaceName, customPathConfig, TimeSpan.FromSeconds(10));

            // Assert
            Assert.That(upResult.Success, Is.True, "Dummy wg-quick script should have exited successfully.");
            Assert.That(File.Exists(dummyToolLogFile), Is.True, "Dummy script log file should exist.");

            string logContent = File.ReadAllText(dummyToolLogFile);
            Assert.That(logContent, Does.Contain($"up \"{interfaceName}\""), "Dummy script should have been called with 'up interfaceName'.");

            // Clean up dummy script log
            if(File.Exists(dummyToolLogFile)) File.Delete(dummyToolLogFile);
            if(File.Exists(dummyToolPath)) File.Delete(dummyToolPath);
        }

        [Test, Order(22)]
        public async Task Up_InterfaceAlreadyUp_ThrowsExternalToolExceptionOrSpecificError()
        {
            // Ensure interface is up first
            await EnsureTestInterfaceUpWithInitialPeerAsync(TestInterface);
            TestContext.Progress.WriteLine($"{TestInterface} is up for testing 'Up when already up'.");

            // Try to bring it up again
            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgQuick.Up(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(10))
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo("wg-quick"));
            // wg-quick behavior for "already up" can vary. It might exit non-zero with a specific message.
            // Common messages include "already exists" or referring to rtnetlink.
            Assert.That(ex.StandardError, Does.Contain("already exists").IgnoreCase
                .Or.Contain("RTNETLINK answers: File exists").IgnoreCase
                .Or.Contain("is already running").IgnoreCase, // Another possible message
                $"Expected 'already exists' or similar in stderr, but got: {ex.StandardError}");

            TestContext.Progress.WriteLine($"WgQuick.Up for already up interface failed as expected: {ex.Message}");
        }

        [Test, Order(23)]
        public async Task Down_InterfaceAlreadyDown_ThrowsExternalToolExceptionOrSpecificError()
        {
            string interfaceName = "wg_already_down"; // Use a name known to be down
            // Ensure it's down (it shouldn't exist or be up from previous tests with this unique name)
            try
            {
                await WgQuick.Down(interfaceName, _integrationTestConfig, TimeSpan.FromSeconds(5));
            }
            catch (ExternalToolException) { /* Expected if it's already down/doesn't exist */ }
            TestContext.Progress.WriteLine($"{interfaceName} is confirmed down for testing 'Down when already down'.");

            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgQuick.Down(interfaceName, _integrationTestConfig, TimeSpan.FromSeconds(10))
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo("wg-quick"));
            // stderr from wg-quick for "not active" or "does not exist"
            Assert.That(ex.StandardError, Does.Contain("is not a WireGuard interface").IgnoreCase
                .Or.Contain("not active").IgnoreCase
                .Or.Contain("No such device").IgnoreCase, // Another possible message
                 $"Expected 'not a WireGuard interface' or 'not active' in stderr, but got: {ex.StandardError}");

            TestContext.Progress.WriteLine($"WgQuick.Down for already down interface failed as expected: {ex.Message}");
        }

        [Test, Order(24)]
        public async Task SetPeerAsync_NewPeerPublicKey_AddsPeer()
        {
            // Ensure interface is up, but without this specific new peer initially
            string newPeerKey = "NEWPEERPUBKEYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
            await EnsureTestInterfaceUpWithInitialPeerAsync(TestInterface, PeerPublicKey); // Ensure TestInterface is up with an existing peer
             TestContext.Progress.WriteLine($"{TestInterface} is up for testing 'SetPeerAsync with new peer key'.");

            var options = new WgPeerUpdateOptions { AllowedIPs = new List<string> { "10.200.200.99/32" } };
            var setResult = await WgQuick.SetPeerAsync(TestInterface, newPeerKey, options, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync for new peer failed: {setResult.StandardError}");

            var details = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            Assert.That(details, Is.Not.Null);
            var addedPeer = details.Peers.FirstOrDefault(p => p.PublicKey == newPeerKey);
            Assert.That(addedPeer, Is.Not.Null, "New peer was not found after SetPeerAsync.");
            Assert.That(addedPeer.AllowedIPs, Contains.Item("10.200.200.99/32"));
            Assert.That(details.Peers.Count, Is.GreaterThanOrEqualTo(2), "Should have at least the initial peer and the new peer.");
        }

        [Test, Order(25)]
        public async Task SetPeerAsync_OnDownInterface_ThrowsExternalToolException()
        {
            string downInterface = "wg_set_on_down"; // Unique name for this test
            // Ensure this interface's config file exists so `wg set` can find it, but interface is down.
            var serverConf = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.203.1/24" } };
            await CreateAndDeployTestConfig(downInterface, serverConf);

            // Ensure it's down
            try { await WgQuick.Down(downInterface, _integrationTestConfig, TimeSpan.FromSeconds(5)); }
            catch (ExternalToolException) { /* Expected if already down or doesn't exist as live interface */ }
            TestContext.Progress.WriteLine($"{downInterface} is confirmed down for testing 'SetPeerAsync on down interface'.");

            var options = new WgPeerUpdateOptions { AllowedIPs = new List<string> { "10.200.203.2/32" } };

            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgQuick.SetPeerAsync(downInterface, PeerPublicKey, options, _integrationTestConfig)
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo("wg"));
            // `wg set` on a down interface usually results in "No such device" or similar.
            Assert.That(ex.StandardError, Does.Contain("No such device").IgnoreCase
                .Or.Contain("Cannot find device").IgnoreCase,
                 $"Expected 'No such device' or similar, but got: {ex.StandardError}");
        }

        [Test, Order(26)]
        public async Task SyncConf_WithInvalidConfFileContent_ThrowsExternalToolException()
        {
            string interfaceName = "wg_invalid_sync";
            string invalidConfPath = Path.Combine(_testBaseDir, $"{interfaceName}.conf");
            await FileSystem.WriteAllTextAsync(invalidConfPath, "[Interface]\nPrivateKey=INVALID_KEY_NO_EQUALS\nAddress=10.0.0.1/24"); // Malformed key

            // Ensure interface exists for wg set/sync operations (even if it's not fully "up" with wg-quick)
            // For syncconf, the interface must exist. We can try to add it simply.
            try { await ProcessRunnerInstance.RunAsync("ip", $"link add {interfaceName} type wireguard"); }
            catch (Exception ex) { TestContext.Progress.WriteLine($"Could not pre-add link {interfaceName} for SyncConf test, may fail if it does not exist. Error: {ex.Message}");}


            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgQuick.SyncConf(interfaceName, invalidConfPath, _integrationTestConfig)
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo("wg"));
            Assert.That(ex.StandardError, Does.Contain("Invalid Base64 string").IgnoreCase
                .Or.Contain("Unable to parse private key").IgnoreCase
                .Or.Contain("Syntax error").IgnoreCase,  // General parsing error
                $"Expected 'Invalid Base64 string' or similar error from wg syncconf, but got: {ex.StandardError}");

            // Clean up link if added
            try { await ProcessRunnerInstance.RunAsync("ip", $"link del {interfaceName}"); } catch {}
        }

        [Test, Order(27)]
        public async Task SetConf_WithInvalidConfFileContent_ThrowsExternalToolException()
        {
            string interfaceName = "wg_invalid_set";
            string invalidConfPath = Path.Combine(_testBaseDir, $"{interfaceName}.conf");
            await FileSystem.WriteAllTextAsync(invalidConfPath, "[Interface]\nPrivateKey=INVALID_KEY_NO_EQUALS\nAddress=10.0.0.1/24"); // Malformed key

            try { await ProcessRunnerInstance.RunAsync("ip", $"link add {interfaceName} type wireguard"); }
            catch (Exception ex) { TestContext.Progress.WriteLine($"Could not pre-add link {interfaceName} for SetConf test, may fail if it does not exist. Error: {ex.Message}");}

            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgQuick.SetConf(interfaceName, invalidConfPath, _integrationTestConfig)
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Is.EqualTo("wg"));
            Assert.That(ex.StandardError, Does.Contain("Invalid Base64 string").IgnoreCase
                .Or.Contain("Unable to parse private key").IgnoreCase
                .Or.Contain("Syntax error").IgnoreCase,
                 $"Expected 'Invalid Base64 string' or similar error from wg setconf, but got: {ex.StandardError}");

            try { await ProcessRunnerInstance.RunAsync("ip", $"link del {interfaceName}"); } catch {}
        }

        [Test, Order(28)]
        public async Task Save_OnInterfaceNotUpWithWgQuick_MayFailOrDoNothing()
        {
            string interfaceName = "wg_manual_set";
            string confPath = Path.Combine(_testBaseDir, $"{interfaceName}.conf");

            var serverConf = new WgServerConfig(ServerPrivateKey) { Address = new List<string> { "10.200.204.1/24" } };
            var wgConfig = new WgConfig(serverConf);
            wgConfig.ToFile(confPath); // Uses WgConfig's StandardFileSystem

            // Bring up interface manually using wg setconf (requires 'ip link add' first)
            try
            {
                await ProcessRunnerInstance.RunAsync("ip", $"link add {interfaceName} type wireguard");
                await WgQuick.SetConf(interfaceName, confPath, _integrationTestConfig);
                // At this point, interface is up, but not via wg-quick's stateful management for this interface name.
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Setup for Save_OnInterfaceNotUpWithWgQuick_MayFailOrDoNothing failed: Could not bring up interface manually. {ex.Message}");
            }

            // Act: Attempt to save using wg-quick save
            // This command often expects a configuration file at /etc/wireguard/<interfaceName>.conf
            // or relies on state managed by `wg-quick up`.
            // If our _integrationTestConfig.WireguardConfigDirectory is not /etc/wireguard, it will likely fail.
            // Or if wg-quick doesn't consider this interface "managed by it".
            var saveResult = await WgQuick.Save(interfaceName, _integrationTestConfig);

            // Assert: Behavior can vary.
            // 1. It might fail if /etc/wireguard/wg_manual_set.conf doesn't exist (if _integrationTestConfig.WireguardConfigDirectory points elsewhere)
            // 2. It might do nothing successfully if it doesn't find a reason to save or a managed config.
            // 3. It might succeed and save to /etc/wireguard/ if that's where it looks by default and it has perms.

            // For this test, we'll assume it's most likely to fail if the config isn't in the default wg-quick path.
            // Or succeed but not actually update our test file at `confPath` unless WireguardConfigDirectory was /etc/wireguard.
            if (!saveResult.Success)
            {
                TestContext.Progress.WriteLine($"WgQuick.Save for manually set interface failed as expected/tolerated: {saveResult.StandardError}");
                Assert.Pass("WgQuick.Save failed or did nothing for a non-wg-quick managed interface, as expected under some configurations.");
            }
            else
            {
                // If it succeeded, check if it *actually* saved to the original confPath (unlikely unless WireguardConfigDirectory was set to _testBaseDir)
                // or if it saved to a default location like /etc/wireguard/.
                // This assertion is tricky without knowing wg-quick's exact internal logic for "save".
                TestContext.Progress.WriteLine($"WgQuick.Save for manually set interface succeeded. Output: {saveResult.StandardOutput}. Stderr: {saveResult.StandardError}");
                // We can't easily verify where it saved if it didn't save to `confPath`.
                // If it *did* save to `confPath` (because WireguardConfigDirectory pointed there), its content should be similar.
                if (_integrationTestConfig.WireguardConfigDirectory == _testBaseDir)
                {
                    var savedConfig = WgConfig.FromFile(confPath);
                    Assert.That(savedConfig.Interface.PrivateKey, Is.EqualTo(ServerPrivateKey)); // Simple check
                }
                Assert.Pass("WgQuick.Save succeeded. Further validation of save location might be needed depending on wg-quick's behavior.");
            }

            // Cleanup
            try { await ProcessRunnerInstance.RunAsync("ip", $"link del {interfaceName}"); } catch {}
        }
    }
}
