using Kevahu.Microservices.Orchestrator.Loading;

namespace Kevahu.Microservices.Orchestrator
{
    /// <summary>
    /// Defines configuration options for the orchestrator application. These options control
    /// aspects like identity, gateway connections, key management, service loading, and
    /// reconnection behavior.
    /// </summary>
    public class OrchestratorOptions
    {
        #region Properties

        /// <summary>
        /// Gets the base URI for the services hosted by this orchestrator instance. This is sent to
        /// gateways during sign-in if provided.
        /// </summary>
        public string? BaseHost { get; set; }

        /// <summary>
        /// Gets the base port for the services hosted by this orchestrator instance. This is sent
        /// to gateways during sign-in if provided.
        /// </summary>
        public ushort? BasePort { get; set; }

        /// <summary>
        /// Gets the base scheme (e.g., "http", "https") for the services hosted by this
        /// orchestrator instance. This is sent to gateways during sign-in if provided.
        /// </summary>
        public string? BaseScheme { get; set; }

        /// <summary>
        /// Gets the unique friendly name for this orchestrator instance. Defaults to a combination
        /// of "Orchestrator", machine name, and process ID. Set via <see cref="Builder.OrchestratorBuilderExtensions.WithFriendlyName"/>.
        /// </summary>
        public string FriendlyName { get; set; } = $"Orchestrator-{Environment.MachineName}-{Environment.ProcessId}";

        /// <summary>
        /// Gets the collection of configuration options for each gateway the orchestrator should
        /// connect to. Populated via <see cref="Builder.OrchestratorBuilderExtensions.AddGateway"/>.
        /// </summary>
        public IReadOnlyCollection<OrchestratorGatewayOptions> GatewayOptions { get; set; } = [];

        /// <summary>
        /// Gets the file information for the orchestrator's private RSA key (PKCS#8 format). Set
        /// via <see cref="Builder.OrchestratorBuilderExtensions.WithMyKeys"/>.
        /// </summary>
        public FileInfo PrivateKey { get; set; }

        /// <summary>
        /// Gets the file information for the orchestrator's public RSA key (PKCS#1 format). Set via
        /// <see cref="Builder.OrchestratorBuilderExtensions.WithMyKeys"/>.
        /// </summary>
        public FileInfo PublicKey { get; set; }

        /// <summary>
        /// Gets the delay in milliseconds before attempting to reconnect to a disconnected gateway.
        /// Defaults to 5000ms.
        /// </summary>
        public int ReconnectDelay { get; set; } = 5000;

        /// <summary>
        /// Gets the collection of route paths served by this orchestrator instance. This is sent to
        /// gateways during sign-in if provided.
        /// </summary>
        public IReadOnlyCollection<string>? Routes { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Loading.ServicesLoadContext"/> used for loading service
        /// assemblies. Set via <see cref="Builder.OrchestratorBuilderExtensions.WithServices"/>.
        /// </summary>
        internal ServicesLoadContext ServicesLoadContext { get; set; }

        #endregion Properties
    }
}