namespace Kevahu.Microservices.Orchestrator
{
    /// <summary>
    /// Defines configuration options specific to a single gateway that the orchestrator interacts
    /// with. Instances of this class are typically configured via <see cref="Builder.OrchestratorBuilderExtensions.AddGateway"/>.
    /// </summary>
    public class OrchestratorGatewayOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets the number of parallel RPC connections to establish to the gateway.
        /// </summary>
        public byte Connections { get; set; }

        /// <summary>
        /// Gets or sets the unique friendly name identifying the gateway.
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// Gets or sets the file containing the gateway's public key (PKCS#1 format) for secure communication.
        /// </summary>
        public FileInfo PublicKey { get; set; }

        /// <summary>
        /// Gets or sets the URL endpoint on the gateway used for signing in and registering services.
        /// </summary>
        public Uri SignInUrl { get; set; }

        /// <summary>
        /// Gets or sets the authentication token required by the gateway's sign-in endpoint.
        /// </summary>
        public string Token { get; set; }

        #endregion Properties
    }
}