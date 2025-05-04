using Kevahu.Microservices.Core.SecureSocket.Extensions;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Kevahu.Microservices.Core.SecureSocket
{
    /// <summary>
    /// Represents an established secure connection over a socket, handling AES
    /// encryption/decryption for data transfer. This class manages the cryptographic state derived
    /// from the initial handshake token and provides methods for sending, receiving, and requesting
    /// data securely. It also handles connection state and potential role reversal.
    /// </summary>
    internal class SecureSocketConnection
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureSocketConnection"/> class using an
        /// established socket and a shared secret token. Derives AES key and IV from the token
        /// using HKDF, creates encryptor/decryptor instances, and sets initial state.
        /// </summary>
        /// <param name="socket">The underlying connected <see cref="System.Net.Sockets.Socket"/>.</param>
        /// <param name="friendlyName">
        /// The friendly name associated with the remote endpoint of this connection.
        /// </param>
        /// <param name="token">
        /// The shared secret byte array (token key) established during the handshake, used to
        /// derive AES keys.
        /// </param>
        /// <param name="canRequest">
        /// Initial state indicating if this side of the connection is allowed to send requests
        /// (typically true for clients, false for servers initially).
        /// </param>
        public SecureSocketConnection(Socket socket, string friendlyName, byte[] token, bool canRequest = false)
        {
            _receiveSignal = new AutoResetEvent(true);
            _sendSignal = new AutoResetEvent(true);
            _random = new Random(BitConverter.ToInt32(HKDF.DeriveKey(HashAlgorithmName.SHA512, token, 4), 0));
            byte[] key = new byte[32];
            _random.NextBytes(key);
            byte[] iv = new byte[16];
            _random.NextBytes(iv);
            FriendlyName = friendlyName;
            _aes = Aes.Create();
            _encryptor = _aes.CreateEncryptor(key, iv);
            _decryptor = _aes.CreateDecryptor(key, iv);
            _socket = socket;
            CanRequest = canRequest;
            isServer = !canRequest;
        }

        #endregion Public Constructors

        #region Properties

        /// <summary>
        /// Gets a value indicating whether this connection endpoint is currently allowed to send
        /// requests using the <see cref="RequestAsync"/> method. This state can change if the
        /// connection is reverted using <see cref="RevertAsync"/>.
        /// </summary>
        public bool CanRequest { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the underlying socket is currently connected.
        /// </summary>
        public bool Connected => _socket.Connected && !_disconnected;

        /// <summary>
        /// Gets the friendly name associated with the remote endpoint of this connection.
        /// </summary>
        public string FriendlyName { get; }

        #endregion Properties

        #region Fields

        /// <summary>
        /// The AES cryptographic service provider instance.
        /// </summary>
        private readonly Aes _aes;

        /// <summary>
        /// Random number generator seeded from the token, used for deriving subsequent keys/IVs.
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// Semaphore to ensure thread-safe receive operations.
        /// </summary>
        private readonly AutoResetEvent _receiveSignal;

        /// <summary>
        /// Semaphore to ensure thread-safe send operations.
        /// </summary>
        private readonly AutoResetEvent _sendSignal;

        /// <summary>
        /// Flag indicating if this connection originated from the server side.
        /// </summary>
        private readonly bool isServer;

        /// <summary>
        /// The AES decryptor transform. Regenerated after each receive.
        /// </summary>
        private ICryptoTransform _decryptor;

        private bool _disconnected = false;

        /// <summary>
        /// The AES encryptor transform. Regenerated after each send.
        /// </summary>
        private ICryptoTransform _encryptor;

        /// <summary>
        /// The underlying socket connection. Can be replaced if reconnection occurs.
        /// </summary>
        private Socket _socket;

        #endregion Fields

        #region Events

        /// <summary>
        /// Occurs when the connection role is successfully reverted via the <see cref="Revert"/> or
        /// <see cref="RevertAsync"/> methods, typically allowing a server-initiated connection to
        /// start receiving requests.
        /// </summary>
        public event EventHandler Reverted;

        #endregion Events

        #region Public Methods

        /// <summary>
        /// Returns a hash code for this connection based on the underlying socket and friendly name.
        /// </summary>
        /// <returns>An integer hash code.</returns>
        public override int GetHashCode() => _socket.GetHashCode() ^ FriendlyName.GetHashCode();

        /// <summary>
        /// Synchronously receives and decrypts data from the remote endpoint. Regenerates the
        /// decryptor for the next message. Blocks until data is received or a timeout occurs
        /// (default infinite).
        /// </summary>
        /// <returns>
        /// The decrypted data payload as a byte array. Returns null if a revert signal is received.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the receive operation times out.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        /// <exception cref="CryptographicException">Thrown if decryption fails.</exception>
        public byte[] Receive() => ReceiveAsync(-1, CancellationToken.None).Result;

        /// <summary>
        /// Asynchronously receives and decrypts data from the remote endpoint with timeout and
        /// cancellation support. Regenerates the decryptor for the next message.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the receive operation. Use -1 for infinite timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the decrypted data payload as
        /// a byte array. Returns null if a revert signal is received.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the operation times out or is canceled.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        /// <exception cref="CryptographicException">Thrown if decryption fails.</exception>
        public async Task<byte[]> ReceiveAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            _receiveSignal.WaitOne(millisecondsTimeout);
            try
            {
                byte[] received = await _socket.ReceiveWithProtocolAsync(millisecondsTimeout, cancellationToken).ConfigureAwait(false);
                if (received.Length == 0)
                {
                    _disconnected = true;
                    throw new SocketException((int)SocketError.Disconnecting);
                }
                if (received.Length == 1 && received[0] == 0)
                {
                    CanRequest = true;
                    Reverted?.Invoke(this, EventArgs.Empty);
                    return null;
                }
                using MemoryStream decryptedMemoryStream = new MemoryStream();
                await new CryptoStream(new MemoryStream(received), _decryptor, CryptoStreamMode.Read).CopyToAsync(decryptedMemoryStream, cancellationToken).ConfigureAwait(false);
                byte[] key = new byte[32];
                _random.NextBytes(key);
                byte[] iv = new byte[16];
                _random.NextBytes(iv);
                _decryptor = _aes.CreateDecryptor(key, iv);
                return decryptedMemoryStream.ToArray();
            }
            finally
            {
                _receiveSignal.Set();
            }
        }

        /// <summary>
        /// Synchronously sends an encrypted request and waits for an encrypted response. Includes
        /// retry logic. Requires <see cref="CanRequest"/> to be true.
        /// </summary>
        /// <param name="data">The data payload to send in the request.</param>
        /// <param name="retry">
        /// The number of times to retry the request if it fails. Defaults to 3.
        /// </param>
        /// <returns>The decrypted response payload, or null if canceled during retry.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="CanRequest"/> is false.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the request/response cycle times out after all retries.
        /// </exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        /// <exception cref="CryptographicException">Thrown if encryption/decryption fails.</exception>
        public byte[]? Request(ReadOnlyMemory<byte> data, int retry = 3) => RequestAsync(data, -1, CancellationToken.None, retry).Result;

        /// <summary>
        /// Asynchronously sends an encrypted request and waits for an encrypted response with
        /// timeout, cancellation, and retry logic. Attempts reconnection if the socket disconnects
        /// and it's not a server-side connection. Requires <see cref="CanRequest"/> to be true.
        /// </summary>
        /// <param name="data">The data payload to send in the request.</param>
        /// <param name="millisecondsTimeout">
        /// The timeout in milliseconds for each send/receive attempt within a retry cycle. Use -1
        /// for infinite timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <param name="retry">
        /// The number of times to retry the request if it fails. Defaults to 3.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the decrypted response
        /// payload, or null if canceled during retry.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="CanRequest"/> is false.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the request/response cycle times out after all retries, or if an individual
        /// attempt times out or is canceled.
        /// </exception>
        /// <exception cref="SocketException">
        /// Thrown if a socket error occurs and retries are exhausted or cancellation occurs.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        /// <exception cref="CryptographicException">Thrown if encryption/decryption fails.</exception>
        public async Task<byte[]> RequestAsync(ReadOnlyMemory<byte> data, int millisecondsTimeout, CancellationToken cancellationToken, int retry = 3)
        {
            if (!CanRequest)
            {
                throw new InvalidOperationException("This client cannot request.");
            }
            for (; retry > 0; retry--)
            {
                try
                {
                    await SendAsync(data, millisecondsTimeout, cancellationToken).ConfigureAwait(false);
                    return await ReceiveAsync(millisecondsTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    if (!_socket.Connected && !isServer)
                    {
                        EndPoint remoteEndPoint = _socket.RemoteEndPoint;
                        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        new SecureSocketClient(_socket, remoteEndPoint, FriendlyName).Connect();
                    }
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                    if (retry <= 1)
                    {
                        throw;
                    }
                }
            }
            throw new TimeoutException($"Could not request from {_socket.RemoteEndPoint} within {retry} retries.");
        }

        /// <summary>
        /// Synchronously sends a signal to the remote endpoint to revert the connection roles.
        /// Requires <see cref="CanRequest"/> to be true. After successful completion, <see
        /// cref="CanRequest"/> becomes false.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="CanRequest"/> is false.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the send operation times out (default infinite).
        /// </exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public void Revert() => RevertAsync(-1, CancellationToken.None).Wait();

        /// <summary>
        /// Asynchronously sends a signal to the remote endpoint to revert the connection roles with
        /// timeout and cancellation support. Requires <see cref="CanRequest"/> to be true. After
        /// successful completion, <see cref="CanRequest"/> becomes false.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the send operation. Use -1 for infinite timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="CanRequest"/> is false.
        /// </exception>
        /// <exception cref="TimeoutException">Thrown if the operation times out or is canceled.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public async Task RevertAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (!CanRequest)
            {
                throw new InvalidOperationException("This client cannot revert.");
            }
            _sendSignal.WaitOne(millisecondsTimeout);
            try
            {
                await _socket.SendWithProtocolAsync(new byte[] { 0 }, millisecondsTimeout, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendSignal.Set();
            }
            CanRequest = false;
        }

        /// <summary>
        /// Synchronously encrypts and sends data to the remote endpoint. Regenerates the encryptor
        /// for the next message. Blocks until data is sent or a timeout occurs (default infinite).
        /// </summary>
        /// <param name="data">The data payload to encrypt and send.</param>
        /// <exception cref="TimeoutException">Thrown if the send operation times out.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        /// <exception cref="CryptographicException">Thrown if encryption fails.</exception>
        public void Send(ReadOnlyMemory<byte> data) => SendAsync(data, -1, CancellationToken.None).Wait();

        /// <summary>
        /// Asynchronously encrypts and sends data to the remote endpoint with timeout and
        /// cancellation support. Regenerates the encryptor for the next message.
        /// </summary>
        /// <param name="data">The data payload to encrypt and send.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the send operation. Use -1 for infinite timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="TimeoutException">Thrown if the operation times out or is canceled.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        /// <exception cref="CryptographicException">Thrown if encryption fails.</exception>
        public async Task SendAsync(ReadOnlyMemory<byte> data, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            _sendSignal.WaitOne(millisecondsTimeout);
            try
            {
                using MemoryStream memoryStream = new MemoryStream();
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, _encryptor, CryptoStreamMode.Write))
                {
                    await cryptoStream.WriteAsync(data).ConfigureAwait(false);
                }
                await _socket.SendWithProtocolAsync(memoryStream.ToArray(), millisecondsTimeout, cancellationToken).ConfigureAwait(false);
                byte[] key = new byte[32];
                _random.NextBytes(key);
                byte[] iv = new byte[16];
                _random.NextBytes(iv);
                _encryptor = _aes.CreateEncryptor(key, iv);
            }
            finally
            {
                _sendSignal.Set();
            }
        }

        /// <summary>
        /// Returns the friendly name of the connection.
        /// </summary>
        /// <returns>The <see cref="FriendlyName"/> of the remote endpoint.</returns>
        public override string ToString() => FriendlyName;

        #endregion Public Methods
    }
}