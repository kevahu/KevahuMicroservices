using System.Net.Sockets;

namespace Kevahu.Microservices.Core.SecureSocket.Extensions
{
    /// <summary>
    /// Provides extension methods for <see cref="Socket"/> to send and receive data using a simple
    /// length-prefix protocol. The protocol prepends a 4-byte integer representing the length of
    /// the subsequent data payload.
    /// </summary>
    internal static class SecureSocketExtensions
    {
        #region Public Methods

        /// <summary>
        /// Synchronously receives data from a connected socket using a length-prefix protocol.
        /// Reads 4 bytes for the length, then reads the specified number of bytes for the data
        /// payload. Blocks until the complete message is received.
        /// </summary>
        /// <param name="socket">The connected socket to receive data from.</param>
        /// <returns>The received data payload as a byte array.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs during receive.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static byte[] ReceiveWithProtocol(this Socket socket) => socket.ReceiveWithProtocolAsync(-1, CancellationToken.None).Result;

        /// <summary>
        /// Asynchronously receives data from a connected socket using a length-prefix protocol with
        /// timeout and cancellation support. Reads 4 bytes for the length, then reads the specified
        /// number of bytes for the data payload.
        /// </summary>
        /// <param name="socket">The connected socket to receive data from.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the data. Use -1 for infinite timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the received data payload as
        /// a byte array.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the operation times out or is canceled before completion.
        /// </exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs during receive.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static async Task<byte[]> ReceiveWithProtocolAsync(this Socket socket, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (!socket.Connected)
            {
                throw new InvalidOperationException("The socket is not connected.");
            }
            try
            {
                using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(millisecondsTimeout);
                cancellationToken.Register(cancellationTokenSource.Cancel);
                byte[] lengthBytes = new byte[4];
                await socket.ReceiveAsync(lengthBytes, cancellationTokenSource.Token).ConfigureAwait(false);
                byte[] data = new byte[BitConverter.ToInt32(lengthBytes)];
                await socket.ReceiveAsync(data, cancellationTokenSource.Token).ConfigureAwait(false);
                return data;
            }
            catch (OperationCanceledException ex)
            {
                throw new TimeoutException($"The data could not be received in time or was canceled.", ex);
            }
        }

        /// <summary>
        /// Asynchronously receives data from a connected socket using a length-prefix protocol with
        /// a timeout. Reads 4 bytes for the length, then reads the specified number of bytes for
        /// the data payload.
        /// </summary>
        /// <param name="socket">The connected socket to receive data from.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the data. Use -1 for infinite timeout.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the received data payload as
        /// a byte array.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="TimeoutException">Thrown if the operation times out before completion.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs during receive.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static async Task<byte[]> ReceiveWithProtocolAsync(this Socket socket, int millisecondsTimeout) => await socket.ReceiveWithProtocolAsync(millisecondsTimeout, CancellationToken.None).ConfigureAwait(false);

        /// <summary>
        /// Asynchronously receives data from a connected socket using a length-prefix protocol with
        /// cancellation support. Reads 4 bytes for the length, then reads the specified number of
        /// bytes for the data payload.
        /// </summary>
        /// <param name="socket">The connected socket to receive data from.</param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, containing the received data payload as
        /// a byte array.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="TimeoutException">Thrown if the operation is canceled before completion.</exception>
        /// ///
        /// <exception cref="SocketException">Thrown if a socket error occurs during receive.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static async Task<byte[]> ReceiveWithProtocolAsync(this Socket socket, CancellationToken cancellationToken) => await socket.ReceiveWithProtocolAsync(-1, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Synchronously sends data to a connected socket using a length-prefix protocol. Prepends
        /// the data payload with 4 bytes representing its length. Blocks until the entire message
        /// is sent.
        /// </summary>
        /// <param name="socket">The connected socket to send data to.</param>
        /// <param name="data">The data payload to send.</param>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs during send.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static void SendWithProtocol(this Socket socket, byte[] data) => socket.SendWithProtocolAsync(data, -1, CancellationToken.None).ConfigureAwait(false);

        /// <summary>
        /// Asynchronously sends data to a connected socket using a length-prefix protocol with
        /// cancellation support. Prepends the data payload with 4 bytes representing its length.
        /// </summary>
        /// <param name="socket">The connected socket to send data to.</param>
        /// <param name="data">The data payload to send.</param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="TimeoutException">Thrown if the operation is canceled before completion.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs during send.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static async Task SendWithProtocolAsync(this Socket socket, byte[] data, CancellationToken cancellationToken) => await socket.SendWithProtocolAsync(data, -1, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Asynchronously sends data to a connected socket using a length-prefix protocol with a
        /// timeout. Prepends the data payload with 4 bytes representing its length.
        /// </summary>
        /// <param name="socket">The connected socket to send data to.</param>
        /// <param name="data">The data payload to send.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the send operation to complete. Use -1 for
        /// infinite timeout.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="TimeoutException">Thrown if the operation times out before completion.</exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs during send.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static async Task SendWithProtocolAsync(this Socket socket, byte[] data, int millisecondsTimeout) => await socket.SendWithProtocolAsync(data, millisecondsTimeout, CancellationToken.None).ConfigureAwait(false);

        /// <summary>
        /// Asynchronously sends data to a connected socket using a length-prefix protocol with
        /// timeout and cancellation support. Prepends the data payload with 4 bytes representing
        /// its length.
        /// </summary>
        /// <param name="socket">The connected socket to send data to.</param>
        /// <param name="data">The data payload to send.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum time in milliseconds to wait for the send operation to complete. Use -1 for
        /// infinite timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> to observe for cancellation requests.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the socket is not connected.</exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the operation times out or is canceled before completion.
        /// </exception>
        /// <exception cref="SocketException">Thrown if a socket error occurs during send.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the socket has been closed.</exception>
        public static async Task SendWithProtocolAsync(this Socket socket, byte[] data, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (!socket.Connected)
            {
                throw new InvalidOperationException("The socket is not connected.");
            }
            try
            {
                using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(millisecondsTimeout);
                cancellationToken.Register(cancellationTokenSource.Cancel);
                await socket.SendAsync([BitConverter.GetBytes(data.Length), data], SocketFlags.None).WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                throw new TimeoutException($"The data could not be sent in time or was canceled.", ex);
            }
        }

        #endregion Public Methods
    }
}