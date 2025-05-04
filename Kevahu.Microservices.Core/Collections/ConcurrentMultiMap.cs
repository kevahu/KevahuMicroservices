using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Kevahu.Microservices.Core.Collections
{
    /// <summary>
    /// Represents a thread-safe collection of key/value pairs that can contain multiple values for
    /// the same key.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the collection.</typeparam>
    /// <typeparam name="TValue">The type of the values in the collection.</typeparam>
    internal class ConcurrentMultiMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        #region Properties

        /// <summary>
        /// Gets the total number of key/value pairs contained in the <see
        /// cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        public int Count => _pairs.Count;

        #endregion Properties

        #region Fields

        /// <summary>
        /// The lock used for thread synchronization.
        /// </summary>
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// The underlying set storing the key/value pairs.
        /// </summary>
        private readonly HashSet<KeyValuePair<TKey, TValue>> _pairs = [];

        #endregion Fields

        #region Indexers

        /// <summary>
        /// Gets an enumerator for the values associated with the specified key.
        /// </summary>
        /// <param name="key">The key to retrieve values for.</param>
        /// <returns>An enumerator for the values associated with the key.</returns>
        public IEnumerator<TValue> this[TKey key]
        {
            get
            {
                return GetEnumeratorByKey(key);
            }
        }

        #endregion Indexers

        #region Public Methods

        /// <summary>
        /// Adds the specified key/value pair to the <see cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="pair">The key/value pair to add.</param>
        /// <returns><c>true</c> if the pair was added successfully; otherwise, <c>false</c>.</returns>
        public bool Add(KeyValuePair<TKey, TValue> pair)
        {
            try
            {
                _lock.EnterWriteLock();
                return _pairs.Add(pair);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds the specified key and value to the <see cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <returns><c>true</c> if the key/value pair was added successfully; otherwise, <c>false</c>.</returns>
        public bool Add(TKey key, TValue value)
        {
            return Add(new KeyValuePair<TKey, TValue>(key, value));
        }

        /// <summary>
        /// Adds the specified values for the given key to the <see cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the elements to add.</param>
        /// <param name="values">The values to add for the specified key.</param>
        public void Add(TKey key, IEnumerable<TValue> values)
        {
            foreach (var value in values)
            {
                Add(key, value);
            }
        }

        /// <summary>
        /// Adds the specified values for the given key to the <see cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the elements to add.</param>
        /// <param name="values">The values to add for the specified key.</param>
        public void Add(TKey key, params TValue[] values)
        {
            Add(key, (IEnumerable<TValue>)values);
        }

        /// <summary>
        /// Determines whether the <see cref="ConcurrentMultiMap{TKey, TValue}"/> contains the
        /// specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <see cref="ConcurrentMultiMap{TKey, TValue}"/>.</param>
        /// <returns>
        /// <c>true</c> if the <see cref="ConcurrentMultiMap{TKey, TValue}"/> contains an element
        /// with the specified key; otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsKey(TKey key)
        {
            try
            {
                _lock.EnterReadLock();
                return _pairs.Any(p => p.Key?.Equals(key) ?? false);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Determines whether the <see cref="ConcurrentMultiMap{TKey, TValue}"/> contains the
        /// specified value.
        /// </summary>
        /// <param name="value">
        /// The value to locate in the <see cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="ConcurrentMultiMap{TKey, TValue}"/> contains an element
        /// with the specified value; otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsValue(TValue value)
        {
            try
            {
                _lock.EnterReadLock();
                return _pairs.Any(p => p.Value?.Equals(value) ?? false);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets the number of values associated with the specified key.
        /// </summary>
        /// <param name="key">The key to count values for.</param>
        /// <returns>The number of values associated with the specified key.</returns>
        public int CountByKey(TKey key)
        {
            try
            {
                _lock.EnterReadLock();
                return _pairs.Count(p => p.Key?.Equals(key) ?? false);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets the number of keys associated with the specified value.
        /// </summary>
        /// <param name="value">The value to count keys for.</param>
        /// <returns>The number of keys associated with the specified value.</returns>
        public int CountByValue(TValue value)
        {
            try
            {
                _lock.EnterReadLock();
                return _pairs.Count(p => p.Value?.Equals(value) ?? false);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection of key/value pairs.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            try
            {
                _lock.EnterReadLock();
                // Return a copy to avoid issues with collection modification during enumeration
                return _pairs.ToHashSet().GetEnumerator();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the values associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose values to enumerate.</param>
        /// <returns>An enumerator for the values associated with the specified key.</returns>
        public IEnumerator<TValue> GetEnumeratorByKey(TKey key)
        {
            // This method uses yield return, which handles locking implicitly per iteration.
            // However, taking a read lock for the duration ensures consistency if the underlying
            // collection could be modified by other threads during enumeration. A snapshot approach
            // (like in GetEnumerator) might be safer depending on use case.
            try
            {
                _lock.EnterReadLock();
                foreach (var pair in _pairs.Where(p => p.Key?.Equals(key) ?? false))
                {
                    yield return pair.Value;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the keys associated with the specified value.
        /// </summary>
        /// <param name="value">The value whose keys to enumerate.</param>
        /// <returns>An enumerator for the keys associated with the specified value.</returns>
        public IEnumerator<TKey> GetEnumeratorByValue(TValue value)
        {
            try
            {
                _lock.EnterReadLock();
                foreach (var pair in _pairs.Where(p => p.Value?.Equals(value) ?? false))
                {
                    yield return pair.Key;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific key/value pair from the <see
        /// cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">The value of the element to remove.</param>
        /// <returns>
        /// <c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>.
        /// </returns>
        public bool Remove(TKey key, TValue value)
        {
            return Remove(new KeyValuePair<TKey, TValue>(key, value));
        }

        /// <summary>
        /// Removes the specified key/value pair from the <see cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="pair">The key/value pair to remove.</param>
        /// <returns><c>true</c> if the pair was removed successfully; otherwise, <c>false</c>.</returns>
        public bool Remove(KeyValuePair<TKey, TValue> pair)
        {
            try
            {
                _lock.EnterWriteLock();
                return _pairs.Remove(pair);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes all values associated with the specified key from the <see
        /// cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the elements to remove.</param>
        /// <returns><c>true</c> if any elements were removed; otherwise, <c>false</c>.</returns>
        public bool RemoveByKey(TKey key)
        {
            try
            {
                _lock.EnterWriteLock();
                return _pairs.RemoveWhere(p => p.Key?.Equals(key) ?? false) > 0;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes all key/value pairs with the specified value from the <see
        /// cref="ConcurrentMultiMap{TKey, TValue}"/>.
        /// </summary>
        /// <param name="value">The value of the elements to remove.</param>
        /// <returns><c>true</c> if any elements were removed; otherwise, <c>false</c>.</returns>
        public bool RemoveByValue(TValue value)
        {
            try
            {
                _lock.EnterWriteLock();
                return _pairs.RemoveWhere(p => p.Value?.Equals(value) ?? false) > 0;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempts to get the set of values associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose values to get.</param>
        /// <param name="values">
        /// When this method returns, contains the set of values associated with the specified key,
        /// if the key is found; otherwise, <c>null</c>. This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="ConcurrentMultiMap{TKey, TValue}"/> contains elements with
        /// the specified key; otherwise, <c>false</c>.
        /// </returns>
        public bool TryGetValues(TKey key, [MaybeNullWhen(false)] out HashSet<TValue> values)
        {
            try
            {
                _lock.EnterReadLock();
                var pairs = _pairs.Where(p => p.Key?.Equals(key) ?? false).Select(p => p.Value);
                if (pairs.Any())
                {
                    values = pairs.ToHashSet();
                    return true;
                }
                values = null;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        #endregion Public Methods
    }
}