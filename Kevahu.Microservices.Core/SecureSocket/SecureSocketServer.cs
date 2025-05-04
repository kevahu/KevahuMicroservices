using Kevahu.Microservices.Core.SecureSocket.Cryptography;
using Kevahu.Microservices.Core.SecureSocket.Extensions;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Reflection;

namespace Kevahu.Microservices.Core.SecureSocket
{
    /// <summary>
    /// Represents a secure socket server that listens for incoming client connections and handles
    /// secure communication using RSA and AES encryption. This class extends the SecureSocket base
    /// class and provides server-side functionality.
    /// </summary>
    internal class SecureSocketServer : SecureSocket
    {
        #region Classes

        /// <summary>
        /// Provides data for the <see cref="SecureSocketServer.OnClientConnected"/> event. Contains
        /// the successfully established <see cref="SecureSocketConnection"/>.
        /// </summary>
        /// <param name="client">
        /// The established <see cref="SecureSocketConnection"/> representing the connected client.
        /// </param>
        internal class ClientConnectedEventArgs(SecureSocketConnection client) : EventArgs
        {
            #region Properties

            /// <summary>
            /// Gets the <see cref="SecureSocketConnection"/> representing the successfully
            /// connected and authenticated client.
            /// </summary>
            public SecureSocketConnection Client { get; } = client;

            #endregion Properties
        }

        /// <summary>
        /// Provides data for the <see cref="SecureSocketServer.OnClientUnauthorised"/> event.
        /// Contains the client <see cref="Socket"/> that failed authentication and details about
        /// the failure.
        /// </summary>
        /// <param name="client">
        /// The client <see cref="Socket"/> that failed the authentication or handshake process.
        /// </param>
        /// <param name="message">A message describing the reason for the authorization failure.</param>
        /// <param name="exception">
        /// The exception that occurred during the handshake or authentication, if any.
        /// </param>
        internal class ClientUnauthorisedEventArgs(Socket client, string message, Exception? exception = null) : EventArgs
        {
            #region Properties

            /// <summary>
            /// Gets the client <see cref="Socket"/> that failed the authentication or handshake process.
            /// </summary>
            public Socket Client { get; } = client;

            /// <summary>
            /// Gets the exception that occurred during the handshake or authentication, if any,
            /// leading to the failure.
            /// </summary>
            public Exception? Exception { get; } = exception;

            /// <summary>
            /// Gets a message describing the reason for the authorization failure.
            /// </summary>
            public string Message { get; } = message;

            #endregion Properties

            #region Public Methods

            /// <summary>
            /// Returns a string representation of the unauthorized event, including the client
            /// endpoint, message, and exception details if available.
            /// </summary>
            /// <returns>A string summarizing the event.</returns>
            public override string ToString() => $"{Client.RemoteEndPoint}: {Message}" + (Exception != null ? $"\n{Exception}" : "");

            #endregion Public Methods
        }

        #endregion Classes

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the SecureSocketServer class with the specified socket.
        /// </summary>
        /// <param name="socket">The underlying socket connection for the server.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the socket is not configured as a server.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the public or private key is not set.
        /// </exception>
        public SecureSocketServer(Socket socket) : base(socket)
        {
            if (!(bool)socket.GetType().GetField("_isListening", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(socket))
            {
                throw new InvalidOperationException("The socket must be a server.");
            }

            if (_MyPublicKey == null)
            {
                throw new ArgumentNullException("My public key is required.");
            }
            if (_MyPrivateKey == null)
            {
                throw new ArgumentNullException("My private key is required.");
            }
        }

        #endregion Public Constructors

        #region Events

        /// <summary>
        /// Occurs when a client successfully connects to the server.
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> OnClientConnected;

        /// <summary>
        /// Occurs when a client is unauthorized to connect to the server.
        /// </summary>
        public event EventHandler<ClientUnauthorisedEventArgs> OnClientUnauthorised;

        #endregion Events

        #region Public Methods

        /// <summary>
        /// Starts the server and begins listening for incoming client connections.
        /// </summary>
        /// <param name="block">Indicates whether the method should block until the server is stopped.</param>
        /// <param name="cancellationToken">
        /// A cancellation token to observe while waiting for the task to complete.
        /// </param>
        public void Start(bool block, CancellationToken cancellationToken)
        {
            Task server = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Socket? client = await _socket.AcceptAsync(cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (client?.Connected ?? false)
                        {
                            client.DisconnectAsync(false).ConfigureAwait(false);
                        }

                        break;
                    }
                    Task.Run(() => InitiateClientAsync(client, cancellationToken));
                }
            }, cancellationToken);
            if (block)
            {
                server.Wait();
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Initializes and authenticates a client connection asynchronously.
        /// </summary>
        /// <param name="client">The client socket to initiate.</param>
        /// <param name="cancellationToken">
        /// A cancellation token to observe while waiting for the task to complete.
        /// </param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task InitiateClientAsync(Socket client, CancellationToken cancellationToken)
        {
            byte[]? response = await client.ReceiveWithProtocolAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                if (client?.Connected ?? false)
                {
                    client.DisconnectAsync(false).ConfigureAwait(false);
                }

                return;
            }
            try
            {
                byte[] clientPublicKey = RsaTokenGenerator.GetPublicKey(response);
                KeyValuePair<string, ReadOnlyCollection<byte>> clientKey;
                try
                {
                    clientKey = _TrustedKeys.SingleOrDefault(kv => kv.Value.SequenceEqual(clientPublicKey));
                }
                catch
                {
                    Task.Run(() => OnClientUnauthorised?.Invoke(this, new ClientUnauthorisedEventArgs(client, "The public key was registered more then once."))).ConfigureAwait(false);
                    await client.DisconnectAsync(false).ConfigureAwait(false);
                    return;
                }
                if (clientKey.Key == null)
                {
                    Task.Run(() => OnClientUnauthorised?.Invoke(this, new ClientUnauthorisedEventArgs(client, "The public key of the client was not found in the list of trusted keys."))).ConfigureAwait(false);
                    await client.DisconnectAsync(false).ConfigureAwait(false);
                    return;
                }
                RsaTokenGenerator rsaToken = new RsaTokenGenerator(clientPublicKey, null, _MyPublicKey.ToArray(), _MyPrivateKey.ToArray());
                byte[] tokenKey = null;
                try
                {
                    tokenKey = rsaToken.DecryptToken(response);
                }
                catch (Exception ex)
                {
                    Task.Run(() => OnClientUnauthorised?.Invoke(this, new ClientUnauthorisedEventArgs(client, "The client's handshake was incorrect.", ex))).ConfigureAwait(false);
                    await client.DisconnectAsync(false).ConfigureAwait(false);
                    return;
                }
                Task.Run(() => OnClientConnected?.Invoke(this, new ClientConnectedEventArgs(new SecureSocketConnection(client, clientKey.Key, tokenKey)))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Task.Run(() => OnClientUnauthorised?.Invoke(this, new ClientUnauthorisedEventArgs(client, "An error occurred while initiating client communication.", ex))).ConfigureAwait(false);
                await client.DisconnectAsync(false).ConfigureAwait(false);
            }
        }

        #endregion Private Methods
    }
}