using WireGuardManager.Utilities;

namespace WireGuardManager
{
    /// <summary>
    /// Provides a central access point for logging within the WireGuardManager library.
    /// Consumers of the library can replace the default <see cref="ConsoleLoggingProvider"/>
    /// with their own implementation of <see cref="IWgLoggingProvider"/> to integrate
    /// with their application's logging infrastructure.
    /// </summary>
    public static class WgLogging
    {
        /// <summary>
        /// Gets or sets the current logging provider.
        /// Defaults to <see cref="ConsoleLoggingProvider"/>.
        /// </summary>
        public static IWgLoggingProvider Logger { get; set; } = new ConsoleLoggingProvider();
    }
}
