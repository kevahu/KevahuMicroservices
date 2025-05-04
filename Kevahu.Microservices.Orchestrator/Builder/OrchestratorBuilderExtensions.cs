using Kevahu.Microservices.Core.RemoteProcedureCall;
using Kevahu.Microservices.Core.SecureSocket;
using Kevahu.Microservices.Core.SecureSocket.Cryptography;
using Kevahu.Microservices.Orchestrator.Loading;

namespace Kevahu.Microservices.Orchestrator.Builder
{
    /// <summary>
    /// Provides extension methods for configuring the <see cref="OrchestratorBuilder"/>.
    /// </summary>
    public static class OrchestratorBuilderExtensions
    {
        #region Public Methods

        /// <summary>
        /// Adds configuration for a gateway that the orchestrator should connect to and register with.
        /// </summary>
        /// <param name="builder">The <see cref="OrchestratorBuilder"/> instance.</param>
        /// <param name="friendlyName">The unique friendly name identifying the gateway.</param>
        /// <param name="connections">
        /// The number of parallel RPC connections to establish to the gateway.
        /// </param>
        /// <param name="publicKey">
        /// The file containing the gateway's public key (PKCS#1 format) for secure communication.
        /// </param>
        /// <param name="signInUrl">
        /// The URL endpoint on the gateway used for signing in and registering services.
        /// </param>
        /// <param name="token">The authentication token required by the gateway's sign-in endpoint.</param>
        /// <returns>The <see cref="OrchestratorBuilder"/> instance for chaining.</returns>
        public static OrchestratorBuilder AddGateway(this OrchestratorBuilder builder,
            string friendlyName,
            byte connections,
            FileInfo publicKey,
            Uri signInUrl,
            string token)
        {
            builder.Options.GatewayOptions = builder.Options.GatewayOptions.Append(new OrchestratorGatewayOptions()
            {
                FriendlyName = friendlyName,
                Connections = connections,
                PublicKey = publicKey,
                SignInUrl = signInUrl,
                Token = token
            }).ToArray();
            return builder;
        }

        /// <summary>
        /// Sets the friendly name for this orchestrator instance. This name is used when
        /// registering with gateways.
        /// </summary>
        /// <param name="builder">The <see cref="OrchestratorBuilder"/> instance.</param>
        /// <param name="friendlyName">The unique friendly name for this orchestrator.</param>
        /// <returns>The <see cref="OrchestratorBuilder"/> instance for chaining.</returns>
        public static OrchestratorBuilder WithFriendlyName(this OrchestratorBuilder builder, string friendlyName)
        {
            builder.Options.FriendlyName = friendlyName;
            return builder;
        }

        /// <summary>
        /// Sets the orchestrator's own RSA public (PKCS#1) and private (PKCS#8) keys used for
        /// secure communication handshakes. Reads keys from the specified files. If the files do
        /// not exist, generates a new key pair (8192-bit) and saves them to the specified file
        /// paths. Also updates the global <see cref="SecureSocket"/> keys.
        /// </summary>
        /// <param name="builder">The <see cref="OrchestratorBuilder"/> instance.</param>
        /// <param name="publicKey">The file path for the public key (PKCS#1 format).</param>
        /// <param name="privateKey">The file path for the private key (PKCS#8 format).</param>
        /// <returns>The <see cref="OrchestratorBuilder"/> instance for chaining.</returns>
        public static OrchestratorBuilder WithMyKeys(this OrchestratorBuilder builder, FileInfo publicKey, FileInfo privateKey)
        {
            byte[] publicKeyBytes;
            byte[] privateKeyBytes;
            if (!publicKey.Exists || !privateKey.Exists)
            {
                (byte[] PrivateKey, byte[] PublicKey) keys = RsaTokenGenerator.GenerateKeys();
                publicKeyBytes = keys.PublicKey;
                using (FileStream publicKeyStream = publicKey.Create())
                {
                    publicKeyStream.Write(publicKeyBytes);
                }
                privateKeyBytes = keys.PrivateKey;
                using (FileStream privateKeyStream = privateKey.Create())
                {
                    privateKeyStream.Write(privateKeyBytes);
                }
            }
            else
            {
                publicKeyBytes = new byte[publicKey.Length];
                using (FileStream publicKeyStream = publicKey.OpenRead())
                {
                    publicKeyStream.Read(publicKeyBytes);
                }
                privateKeyBytes = new byte[privateKey.Length];
                using (FileStream privateKeyStream = privateKey.OpenRead())
                {
                    privateKeyStream.Read(privateKeyBytes);
                }
            }
            SecureSocket.SetMyKeys(publicKeyBytes.AsReadOnly(), privateKeyBytes.AsReadOnly());
            builder.Options.PublicKey = publicKey;
            builder.Options.PrivateKey = privateKey;
            return builder;
        }

        /// <summary>
        /// Specifies the directory containing service assemblies to be loaded by the orchestrator.
        /// Creates a <see cref="ServicesLoadContext"/>, finds and registers RPC services within the
        /// loaded assemblies using <see
        /// cref="RpcFramework.FindAndAddServices(IEnumerable{System.Reflection.Assembly})"/>, and
        /// invokes the <see cref="OrchestratorBuilder.OnServicesLoaded"/> event.
        /// </summary>
        /// <param name="builder">The <see cref="OrchestratorBuilder"/> instance.</param>
        /// <param name="servicesPath">The directory containing the service assemblies.</param>
        /// <returns>The <see cref="OrchestratorBuilder"/> instance for chaining.</returns>
        public static OrchestratorBuilder WithServices(this OrchestratorBuilder builder, DirectoryInfo servicesPath)
        {
            builder.Options.ServicesLoadContext = new ServicesLoadContext(servicesPath);
            RpcFramework.FindAndAddServices(builder.Options.ServicesLoadContext.FindServices());
            builder.InvokeServicesLoaded(builder.Options.ServicesLoadContext);
            return builder;
        }

        #endregion Public Methods
    }
}