using System.Collections.ObjectModel;
using System.Net.Sockets;

namespace Kevahu.Microservices.Core.SecureSocket
{
    /// <summary>
    /// Provides the abstract base class for secure socket communication (client and server).
    /// Manages the local instance's RSA keys and a collection of trusted public keys for remote
    /// peers. This class defines the static infrastructure for key management used by derived
    /// classes like <see cref="SecureSocketClient"/> and <see cref="SecureSocketServer"/>.
    /// </summary>
    public abstract class SecureSocket
    {
        #region Protected Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureSocket"/> class, associating it with
        /// an existing <see cref="System.Net.Sockets.Socket"/>.
        /// </summary>
        /// <param name="socket">The underlying <see cref="System.Net.Sockets.Socket"/> connection.</param>
        protected SecureSocket(Socket socket)
        {
            _socket = socket;
        }

        #endregion Protected Constructors

        #region Fields

        /// <summary>
        /// Stores the collection of trusted public keys associated with friendly names.
        /// Key: Friendly name (string). Value: Public key (ReadOnlyCollection&lt;byte&gt; in PKCS#1 format).
        /// </summary>
        protected static readonly Dictionary<string, ReadOnlyCollection<byte>> _TrustedKeys = [];

        /// <summary>
        /// Stores the private key (PKCS#8 format) of the local instance. Set via <see cref="SetMyKeys"/>.
        /// </summary>
        protected static ReadOnlyCollection<byte> _MyPrivateKey;

        /// <summary>
        /// Stores the public key (PKCS#1 format) of the local instance. Set via <see cref="SetMyKeys"/>.
        /// </summary>
        protected static ReadOnlyCollection<byte> _MyPublicKey;

        /// <summary>
        /// The underlying <see cref="System.Net.Sockets.Socket"/> used for communication by the
        /// derived class instance.
        /// </summary>
        protected readonly Socket _socket;

        #endregion Fields

        #region Public Methods

        /// <summary>
        /// Adds or updates a trusted public key associated with a specific friendly name. This key
        /// is used to verify the identity of remote peers connecting with that name.
        /// </summary>
        /// <param name="publicKey">The public key (PKCS#1 format) to trust.</param>
        /// <param name="friendlyName">The unique friendly name to associate with this public key.</param>
        public static void AddTrustedKey(ReadOnlyCollection<byte> publicKey, string friendlyName) => _TrustedKeys[friendlyName] = publicKey;

        /// <summary>
        /// Removes all trusted public keys from the collection.
        /// </summary>
        public static void ClearTrustedKeys() => _TrustedKeys.Clear();

        /// <summary>
        /// Checks if a trusted public key associated with the specified friendly name exists in the collection.
        /// </summary>
        /// <param name="friendlyName">The friendly name to check for.</param>
        /// <returns><c>true</c> if a key with the specified name exists; otherwise, <c>false</c>.</returns>
        public static bool ContainsTrustedKey(string friendlyName) => _TrustedKeys.ContainsKey(friendlyName);

        /// <summary>
        /// Retrieves the trusted public key associated with the specified friendly name.
        /// </summary>
        /// <param name="friendlyName">The friendly name of the public key to retrieve.</param>
        /// <returns>The trusted public key (PKCS#1 format) as a <see cref="ReadOnlyCollection{Byte}"/>.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if no key is associated with the specified friendly name.
        /// </exception>
        public static ReadOnlyCollection<byte> GetTrustedKey(string friendlyName) => _TrustedKeys[friendlyName];

        /// <summary>
        /// Returns an enumerator that iterates through the collection of trusted friendly names and
        /// their associated public keys.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/> containing
        /// friendly names and public keys.
        /// </returns>
        public static IEnumerable<KeyValuePair<string, ReadOnlyCollection<byte>>> IterateTrustedKeys()
        {
            foreach (KeyValuePair<string, ReadOnlyCollection<byte>> pair in _TrustedKeys)
            {
                yield return pair;
            }
        }

        /// <summary>
        /// Removes the trusted public key associated with the specified friendly name from the collection.
        /// </summary>
        /// <param name="friendlyName">The friendly name of the public key to remove.</param>
        /// <returns><c>true</c> if the key was found and removed; otherwise, <c>false</c>.</returns>
        public static bool RemoveTrustedKey(string friendlyName) => _TrustedKeys.Remove(friendlyName);

        /// <summary>
        /// Sets the RSA public and private keys for this SecureSocket instance (and globally for
        /// all instances). These keys are used for encrypting/decrypting the symmetric key during
        /// the handshake and for signing/verifying identity.
        /// </summary>
        /// <param name="publicKey">The public key (PKCS#1 format) for this instance.</param>
        /// <param name="privateKey">The private key (PKCS#8 format) for this instance.</param>
        public static void SetMyKeys(ReadOnlyCollection<byte> publicKey, ReadOnlyCollection<byte> privateKey)
        {
            _MyPublicKey = publicKey;
            _MyPrivateKey = privateKey;
        }

        #endregion Public Methods
    }
}