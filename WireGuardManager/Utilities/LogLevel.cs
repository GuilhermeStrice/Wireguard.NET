namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Defines the severity levels for logging.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Detailed information, typically of interest only when diagnosing problems.
        /// </summary>
        Trace = 0,
        /// <summary>
        /// Information that is diagnostically helpful to developers.
        /// </summary>
        Debug = 1,
        /// <summary>
        /// General informational messages.
        /// </summary>
        Info = 2,
        /// <summary>
        /// Indicates a potential problem or an unusual event.
        /// </summary>
        Warning = 3,
        /// <summary>
        /// Indicates a failure or error that requires attention.
        /// </summary>
        Error = 4,
        /// <summary>
        /// Disables logging; no messages will be logged.
        /// </summary>
        None = 5
    }
}
