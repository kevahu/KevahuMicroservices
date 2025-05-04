using Kevahu.Microservices.Core.RemoteProcedureCall;
using Kevahu.Microservices.Core.SecureSocket;
using Kevahu.Microservices.Orchestrator.Builder;
using System.Net;

namespace Kevahu.Microservices.Orchestrator
{
    /// <summary>
    /// Responsible for initiating and managing connections from the orchestrator to configured
    /// gateways. Handles the initial sign-in process and automatic reconnection attempts upon disconnection.
    /// </summary>
    public class OrchestratorInitiator
    {
        #region Classes

        /// <summary>
        /// Provides data for the <see cref="OrchestratorInitiator.OnReconnectFailed"/> event.
        /// </summary>
        public class OrchestratorReconnectFailedEventArgs : EventArgs
        {
            #region Properties

            /// <summary>
            /// Gets the exception that occurred during the reconnection attempt.
            /// </summary>
            public Exception Exception { get; init; }

            /// <summary>
            /// Gets the friendly name of the gateway that failed to reconnect.
            /// </summary>
            public string FriendlyName { get; init; }

            #endregion Properties
        }

        #endregion Classes

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestratorInitiator"/> class using
        /// configuration from the provided builder. Subscribes to the <see
        /// cref="RpcFramework.OnDisconnected"/> event to handle reconnections.
        /// </summary>
        /// <param name="builder">The <see cref="OrchestratorBuilder"/> containing the configuration.</param>
        public OrchestratorInitiator(OrchestratorBuilder builder)
        {
            Options = builder.Options;
            RpcFramework.OnDisconnected += ReconnectHandler;
        }

        #endregion Public Constructors

        #region Properties

        /// <summary>
        /// Gets the configuration options for the orchestrator initiator.
        /// </summary>
        public OrchestratorOptions Options { get; internal set; }

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs when an attempt to automatically reconnect to a disconnected gateway fails.
        /// </summary>
        public event EventHandler<OrchestratorReconnectFailedEventArgs> OnReconnectFailed;

        #endregion Events

        #region Public Methods

        /// <summary>
        /// Asynchronously attempts to connect to all configured gateways that are not currently connected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ConnectAllAsync()
        {
            foreach (OrchestratorGatewayOptions gatewayOptions in Options.GatewayOptions)
            {
                if (!RpcFramework.IsConnected(gatewayOptions.FriendlyName))
                {
                    await ConnectAsync(gatewayOptions);
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Asynchronously connects to a specific gateway defined by the provided options. Performs
        /// the sign-in HTTP request to the gateway's sign-in URL, sending the orchestrator's public
        /// key and potentially route/base information. If successful, establishes the RPC
        /// backchannel connection.
        /// </summary>
        /// <param name="gatewayOptions">The configuration options for the gateway to connect to.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous connection operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if already connected to the specified gateway.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the HTTP sign-in request to the gateway fails.
        /// </exception>
        /// <exception cref="System.Net.Http.HttpRequestException">
        /// Thrown if there is an issue sending the HTTP request.
        /// </exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown if adding the gateway's trusted key fails (though unlikely with current implementation).
        /// </exception>
        /// // Assuming AddTrustedKey could potentially throw
        private async Task ConnectAsync(OrchestratorGatewayOptions gatewayOptions)
        {
            if (RpcFramework.IsConnected(gatewayOptions.FriendlyName))
            {
                throw new InvalidOperationException($"Already connected to {gatewayOptions.FriendlyName}");
            }

            byte[] gatewayPublicKey;
            using (FileStream fileStream = gatewayOptions.PublicKey.OpenRead())
            {
                gatewayPublicKey = new byte[fileStream.Length];
                await fileStream.ReadAsync(gatewayPublicKey, 0, (int)fileStream.Length);
            }

            SecureSocket.AddTrustedKey(gatewayPublicKey.AsReadOnly(), gatewayOptions.FriendlyName);

            byte[] myPublicKey;
            using (FileStream fileStream = Options.PublicKey.OpenRead())
            {
                myPublicKey = new byte[fileStream.Length];
                await fileStream.ReadAsync(myPublicKey, 0, (int)fileStream.Length);
            }

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Patch, gatewayOptions.SignInUrl);
            request.Headers.Add("Token", gatewayOptions.Token);
            request.Headers.Add("Friendly-Name", Options.FriendlyName);
            if (Options.Routes?.Count > 0)
            {
                if (Options.BaseHost != null)
                {
                    request.Headers.Add("BaseHost", Options.BaseHost);
                }
                if (Options.BasePort != null)
                {
                    request.Headers.Add("BasePort", Options.BasePort.ToString());
                }

                request.Headers.Add("Routes", string.Join(' ', Options.Routes));
            }

            request.Content = new ByteArrayContent(myPublicKey);

            HttpResponseMessage response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    IPEndPoint backchannelEndpoint = IPEndPoint.Parse(await response.Content.ReadAsStringAsync());
                    if (backchannelEndpoint.Address.Equals(IPAddress.Any) || backchannelEndpoint.Address.Equals(IPAddress.IPv6Any))
                    {
                        if (gatewayOptions.SignInUrl.HostNameType == UriHostNameType.Dns)
                        {
                            var dnsEntry = await Dns.GetHostEntryAsync(gatewayOptions.SignInUrl.DnsSafeHost);
                            backchannelEndpoint = new IPEndPoint(dnsEntry.AddressList.First(a => a.AddressFamily == backchannelEndpoint.AddressFamily), backchannelEndpoint.Port);
                        }
                        else
                        {
                            backchannelEndpoint = new IPEndPoint(IPAddress.Parse(gatewayOptions.SignInUrl.Host), backchannelEndpoint.Port);
                        }
                    }
                    RpcFramework.AddServer(backchannelEndpoint.Address.ToString(), (ushort)backchannelEndpoint.Port, gatewayOptions.FriendlyName, gatewayOptions.Connections, true, true);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Failed to parse the backchannel endpoint from the Gateway (code {response.StatusCode}).", ex);
                }
            }
            else
            {
                throw new IOException($"Failed to connect to the Gateway (code {response.StatusCode}).");
            }
        }

        /// <summary>
        /// Event handler for the <see cref="RpcFramework.OnDisconnected"/> event. Attempts to
        /// automatically reconnect to a configured gateway after a delay if it disconnects. Raises
        /// the <see cref="OnReconnectFailed"/> event if reconnection attempts fail.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the friendly name of the disconnected gateway.</param>
        private async void ReconnectHandler(object? sender, RpcFramework.DisconnectedEventArgs e)
        {
            if (!Options.GatewayOptions.Any(g => g.FriendlyName == e.FriendlyName))
            {
                return;
            }
            while (!RpcFramework.IsConnected(e.FriendlyName))
            {
                await Task.Delay(Options.ReconnectDelay);
                try
                {
                    await ConnectAsync(Options.GatewayOptions.First(g => g.FriendlyName == e.FriendlyName));
                }
                catch (Exception ex)
                {
                    OnReconnectFailed?.Invoke(this, new OrchestratorReconnectFailedEventArgs()
                    {
                        Exception = ex,
                        FriendlyName = e.FriendlyName
                    });
                }
            }
        }

        #endregion Private Methods
    }
}