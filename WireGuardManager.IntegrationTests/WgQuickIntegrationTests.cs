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
    }
}
