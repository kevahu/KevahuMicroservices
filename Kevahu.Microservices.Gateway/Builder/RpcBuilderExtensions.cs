using Kevahu.Microservices.Core.RemoteProcedureCall;
using Kevahu.Microservices.Core.SecureSocket;
using Kevahu.Microservices.Core.SecureSocket.Cryptography;
using Kevahu.Microservices.Gateway.Middleware.RemoteProcedureCall;
using Kevahu.Microservices.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Net;
using Yarp.ReverseProxy.LoadBalancing;

namespace Kevahu.Microservices.Gateway.Builder
{
    /// <summary>
    /// Provides extension methods for configuring RPC (Remote Procedure Call) functionality for the
    /// gateway using <see cref="RpcBuilder"/>, <see cref="IServiceCollection"/>, and <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class RpcBuilderExtensions
    {
        #region Public Methods

        /// <summary>
        /// Adds necessary RPC services to the dependency injection container and returns a new <see
        /// cref="RpcBuilder"/> instance for configuration.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>A new <see cref="RpcBuilder"/> instance for further configuration.</returns>
        public static RpcBuilder AddRemoteProcedureCall(this IServiceCollection services)
        {
            RpcBuilder builder = new RpcBuilder();
            return services.AddRemoteProcedureCallCore(builder);
        }

        /// <summary>
        /// Adds necessary RPC services to the dependency injection container, configures <see
        /// cref="RpcOptions"/> using the provided action, and returns the <see cref="RpcBuilder"/> instance.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configure">An action to configure the <see cref="RpcOptions"/>.</param>
        /// <returns>The <see cref="RpcBuilder"/> instance used for configuration.</returns>
        public static RpcBuilder AddRemoteProcedureCall(this IServiceCollection services, Action<RpcOptions> configure)
        {
            RpcBuilder builder = new RpcBuilder();
            configure(builder.Value);
            return services.AddRemoteProcedureCallCore(builder);
        }

        /// <summary>
        /// Adds a trusted public key (PKCS#1 format) from a file to the <see
        /// cref="SecureSocket"/>'s trusted key store. This key is used to verify the identity of
        /// remote RPC servers connecting to or being connected from the gateway.
        /// </summary>
        /// <param name="builder">The <see cref="RpcBuilder"/> instance.</param>
        /// <param name="publicKey">The file containing the public key (PKCS#1 format).</param>
        /// <param name="friendlyName">
        /// A unique friendly name to associate with the public key, typically identifying the
        /// remote server.
        /// </param>
        /// <returns>The <see cref="RpcBuilder"/> instance for chaining.</returns>
        public static RpcBuilder AddTrustedKey(this RpcBuilder builder, FileInfo publicKey, string friendlyName)
        {
            byte[] publicKeyBytes = new byte[publicKey.Length];
            using (FileStream publicKeyStream = publicKey.OpenRead())
            {
                publicKeyStream.Read(publicKeyBytes);
            }
            SecureSocket.AddTrustedKey(publicKeyBytes.AsReadOnly(), friendlyName);
            return builder;
        }

        /// <summary>
        /// Enables RPC mesh networking for the gateway via <see cref="RpcFramework.AllowMesh"/>. If
        /// enabled, the gateway will attempt to forward incoming RPC requests to other connected
        /// RPC nodes if the target service is not hosted locally.
        /// </summary>
        /// <param name="builder">The <see cref="RpcBuilder"/> instance.</param>
        /// <returns>The <see cref="RpcBuilder"/> instance for chaining.</returns>
        public static RpcBuilder AllowMesh(this RpcBuilder builder)
        {
            RpcFramework.AllowMesh = true;
            return builder;
        }

        /// <summary>
        /// Sets the host address and port on which the gateway's own RPC server will listen for
        /// incoming connections.
        /// </summary>
        /// <param name="builder">The <see cref="RpcBuilder"/> instance.</param>
        /// <param name="host">The host name or IP address to bind the RPC server to.</param>
        /// <param name="port">The port number for the RPC server to listen on.</param>
        /// <returns>The <see cref="RpcBuilder"/> instance for chaining.</returns>
        public static RpcBuilder SetServer(this RpcBuilder builder, string host, ushort port)
        {
            builder.Value.ServerEndpoint = new IPEndPoint(IPAddress.Parse(host), port);
            return builder;
        }

        /// <summary>
        /// Sets a token string in the <see cref="RpcOptions"/>, likely used for
        /// authentication/authorization purposes within custom middleware (e.g., <see cref="RpcSignInMiddleware"/>).
        /// </summary>
        /// <param name="builder">The <see cref="RpcBuilder"/> instance.</param>
        /// <param name="token">The token string.</param>
        /// <returns>The <see cref="RpcBuilder"/> instance for chaining.</returns>
        public static RpcBuilder SetToken(this RpcBuilder builder, string token)
        {
            builder.Value.Token = token;
            return builder;
        }

        /// <summary>
        /// Configures the application pipeline to use RPC middleware, starts the gateway's RPC
        /// server, and registers logging handlers for RPC framework events (connect, disconnect,
        /// incoming requests).
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> instance for chaining.</returns>
        public static IApplicationBuilder UseRemoteProcedureCall(this IApplicationBuilder app)
        {
            RpcOptions options = app.ApplicationServices.GetRequiredService<RpcOptions>();
            RpcFramework.StartServer(options.ServerEndpoint.Address.ToString(), (ushort)options.ServerEndpoint.Port, false);
            app.UseMiddleware<RpcSignInMiddleware>();
            app.UseMiddleware<RpcRetryMiddleware>();
            RpcFramework.OnConnected += (sender, e) =>
            {
                ILogger logger = app.ApplicationServices.GetRequiredService<ILogger<RpcFramework.ConnectedEventArgs>>();
                logger.LogInformation($"RPC {e.FriendlyName} connected");
            };
            RpcFramework.OnIncoming += (sender, e) =>
            {
                ILogger logger = app.ApplicationServices.GetRequiredService<ILogger<RpcFramework.IncomingEventArgs>>();
                string message = $"RPC {e.FriendlyName}: {e.Procedure}";
                if (e.Forwarded)
                {
                    message += " [Forwarded]";
                }
                message += $" ({e.Elapsed.TotalMilliseconds}ms)";
                if (e.IsError)
                {
                    logger.LogError(e.Exception, message);
                }
                else
                {
                    logger.LogInformation(message);
                }
            };
            RpcFramework.OnUnauthorized += (sender, e) =>
            {
                ILogger logger = app.ApplicationServices.GetRequiredService<ILogger<RpcFramework.UnauthorizedEventArgs>>();
                logger.LogWarning(e.Exception, $"RPC {e.RemoteEndpoint} unauthorized: {e.Message}");
                YarpConfigService configService = app.ApplicationServices.GetRequiredService<YarpConfigService>();
                configService.RemoveConfig(null);
            };
            RpcFramework.OnDisconnected += (sender, e) =>
            {
                ILogger logger = app.ApplicationServices.GetRequiredService<ILogger<RpcFramework.DisconnectedEventArgs>>();
                logger.LogWarning($"RPC {e.FriendlyName} disconnected");
                YarpConfigService configService = app.ApplicationServices.GetRequiredService<YarpConfigService>();
                configService.RemoveConfig(e.FriendlyName);
                SecureSocket.RemoveTrustedKey(e.FriendlyName);
            };
            return app;
        }

        /// <summary>
        /// Sets the gateway's own RSA public (PKCS#1) and private (PKCS#8) keys used for secure
        /// communication handshake. Reads keys from the specified files. If the files do not exist,
        /// generates a new key pair (8192-bit) and saves them to the specified file paths.
        /// </summary>
        /// <param name="builder">The <see cref="RpcBuilder"/> instance.</param>
        /// <param name="publicKey">The file path for the public key (PKCS#1 format).</param>
        /// <param name="privateKey">The file path for the private key (PKCS#8 format).</param>
        /// <returns>The <see cref="RpcBuilder"/> instance for chaining.</returns>
        public static RpcBuilder WithMyKeys(this RpcBuilder builder, FileInfo publicKey, FileInfo privateKey)
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
            return builder;
        }

        /// <summary>
        /// Sets the default timeout for outgoing RPC requests initiated by the gateway framework (
        /// <see cref="RpcFramework.TimeoutMilliseconds"/>).
        /// </summary>
        /// <param name="builder">The <see cref="RpcBuilder"/> instance.</param>
        /// <param name="timeout">
        /// The timeout duration in milliseconds. Use -1 for infinite timeout (default).
        /// </param>
        /// <returns>The <see cref="RpcBuilder"/> instance for chaining.</returns>
        public static RpcBuilder WithTimeout(this RpcBuilder builder, int timeout)
        {
            RpcFramework.TimeoutMilliseconds = timeout;
            return builder;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Helper method to register core RPC services as singletons in the dependency injection container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="builder">The <see cref="RpcBuilder"/> containing the configured options.</param>
        /// <returns>The provided <see cref="RpcBuilder"/> instance.</returns>
        private static RpcBuilder AddRemoteProcedureCallCore(this IServiceCollection services, RpcBuilder builder)
        {
            services.TryAddSingleton(builder.Value);
            services.TryAddSingleton<RpcSignInMiddleware>();
            services.TryAddSingleton<RpcRetryMiddleware>();
            services.AddSingleton<YarpConfigService>();
            services.AddSingleton<ILoadBalancingPolicy, LeastBusyLoadBalancingPolicy>();
            return builder;
        }

        #endregion Private Methods
    }
}