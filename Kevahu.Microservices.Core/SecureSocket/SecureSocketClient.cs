using Kevahu.Microservices.Core.SecureSocket.Cryptography;
using Kevahu.Microservices.Core.SecureSocket.Extensions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Kevahu.Microservices.Core.SecureSocket
{
    /// <summary>
    /// Implements the client-side logic for establishing a secure socket connection. It handles
    /// connecting to a server, performing the RSA key exchange to establish a shared symmetric key,
    /// and creating a <see cref="SecureSocketConnection"/> for subsequent encrypted communication.
    /// </summary>
    internal class SecureSocketClient : SecureSocket
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureSocketClient"/> class. Validates that
        /// the provided socket is a client socket and that the necessary local and remote keys are configured.
        /// </summary>
        /// <param name="socket">
        /// The underlying client <see cref="System.Net.Sockets.Socket"/> to use for the connection.
        /// </param>
        /// <param name="remoteEndPoint">
        /// The <see cref="EndPoint"/> of the remote server to connect to.
        /// </param>
        /// <param name="recipientName">
        /// The friendly name of the remote server. Must correspond to a key in the trusted keys
        /// collection ( <see cref="SecureSocket._TrustedKeys"/>).
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the provided <paramref name="socket"/> is detected as a listening socket.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the <paramref name="recipientName"/> is not found in the trusted keys collection.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the local public ( <see cref="SecureSocket._MyPublicKey"/>) or private ( <see
        /// cref="SecureSocket._MyPrivateKey"/>) key has not been set via <see cref="SecureSocket.SetMyKeys"/>.
        /// </exception>
        public SecureSocketClient(Socket socket, EndPoint remoteEndPoint, string recipientName) : base(socket)
        {
            if ((bool)socket.GetType().GetField("_isListening", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(socket))
            {
                throw new InvalidOperationException("The socket must be a client.");
            }
            if (!ContainsTrustedKey(recipientName))
            {
                throw new ArgumentException($"'{recipientName}' is not found in the trusted keys.", nameof(recipientName));
            }
            if (_MyPublicKey == null)
            {
                throw new ArgumentNullException("My public key is required.");
            }
            if (_MyPrivateKey == null)
            {
                throw new ArgumentNullException("My private key is required.");
            }
            _recipientName = recipientName;
            _remoteEndPoint = remoteEndPoint;
        }

        #endregion Public Constructors

        #region Fields

        /// <summary>
        /// The friendly name of the remote server this client intends to connect to.
        /// </summary>
        private readonly string _recipientName;

        /// <summary>
        /// The network endpoint of the remote server.
        /// </summary>
        private readonly EndPoint _remoteEndPoint;

        #endregion Fields

        #region Public Methods

        /// <summary>
        /// Synchronously connects to the remote server and performs the secure handshake.
        /// Establishes the connection if not already connected, sends the encrypted token key, and
        /// returns a <see cref="SecureSocketConnection"/> for further communication.
        /// </summary>
        /// <returns>
        /// A <see cref="SecureSocketConnection"/> representing the established secure connection.
        /// </returns>
        /// <exception cref="TimeoutException">
        /// Thrown if the connection or handshake times out (uses default infinite timeout).
        /// </exception>
        /// <exception cref="SocketException">
        /// Thrown if a socket error occurs during connection or sending.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public SecureSocketConnection Connect() => ConnectAsync(-1, CancellationToken.None).Result;

        /// <summary>
        /// Asynchronously connects to the remote server and performs the secure handshake with
        /// timeout and cancellation support. Establishes the connection if not already connected,
        /// sends the encrypted token key, and returns a <see cref="SecureSocketConnection"/> for
        /// further communication.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the connection and handshake operations to
        /// complete. Use -1 for infinite timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the established <see cref="SecureSocketConnection"/>.
        /// </returns>
        /// <exception cref="TimeoutException">
        /// Thrown if the operation times out or is canceled before completion.
        /// </exception>
        /// <exception cref="SocketException">
        /// Thrown if a socket error occurs during connection or sending.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public async Task<SecureSocketConnection> ConnectAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(millisecondsTimeout);
            cancellationToken.Register(cancellationTokenSource.Cancel);

            RsaTokenGenerator rsaToken = new RsaTokenGenerator(_MyPublicKey.ToArray(), _MyPrivateKey.ToArray(), _TrustedKeys[_recipientName].ToArray(), null);
            byte[] token = rsaToken.GenerateEncryptedToken();
            if (!_socket.Connected)
            {
                await _socket.ConnectAsync(_remoteEndPoint, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            _socket.SendWithProtocol(token);
            return new SecureSocketConnection(_socket, _recipientName, rsaToken.TokenKey, true);
        }

        #endregion Public Methods
    }
}