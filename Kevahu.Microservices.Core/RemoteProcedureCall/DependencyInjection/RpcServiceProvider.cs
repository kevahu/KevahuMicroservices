using Microsoft.Extensions.DependencyInjection;
using System.Collections;

namespace Kevahu.Microservices.Core.RemoteProcedureCall.DependencyInjection
{
    /// <summary>
    /// Provides services for Remote Procedure Call (RPC) implementations, managing their lifetimes
    /// (Singleton, Scoped, Transient). Implements a custom scope management using WeakReferences
    /// and a background thread for cleanup.
    /// </summary>
    public class RpcServiceProvider : IServiceProvider, IEnumerable<Type>
    {
        #region Public Constructors

        /// <summary>
        /// Static constructor to register for full garbage collection notifications. This is used
        /// to trigger cleanup of expired scoped services held by WeakReferences.
        /// </summary>
        static RpcServiceProvider()
        {
            GC.RegisterForFullGCNotification(10, 10);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcServiceProvider"/> class. Starts a
        /// background thread to clean up expired scoped services based on GC notifications.
        /// </summary>
        public RpcServiceProvider()
        {
            bool runCleaner = true;
            new Thread(() =>
            {
                while (runCleaner)
                {
                    GC.WaitForFullGCComplete();
                    foreach (KeyValuePair<Guid, WeakReference> keyValuePair in _scopedServices)
                    {
                        if (!keyValuePair.Value.IsAlive)
                        {
                            _scopedServices.Remove(keyValuePair.Key);
                        }
                    }
                }
            }).Start();
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                runCleaner = false;
            };
        }

        #endregion Public Constructors

        #region Fields

        /// <summary>
        /// Maps interface types to their concrete implementation types.
        /// Key: Interface type, Value: Implementation type.
        /// </summary>
        private Dictionary<Type, Type> _implementations = [];

        /// <summary>
        /// Stores scoped service instances using WeakReferences to allow garbage collection.
        /// Key: Scope identifier (Guid), Value: WeakReference to the service instance.
        /// </summary>
        private Dictionary<Guid, WeakReference> _scopedServices = [];

        /// <summary>
        /// Stores the configured ServiceLifetime for each registered service type (implementation type).
        /// Key: Implementation type, Value: ServiceLifetime.
        /// </summary>
        private Dictionary<Type, ServiceLifetime> _serviceLifetimes = [];

        /// <summary>
        /// Stores singleton service instances.
        /// Key: Implementation type, Value: Singleton instance.
        /// </summary>
        private Dictionary<Type, object> _singletonServices = [];

        #endregion Fields

        #region Public Methods

        /// <summary>
        /// Adds a service mapping from an interface to an implementation type with a specified lifetime.
        /// </summary>
        /// <param name="interfaceType">The interface type.</param>
        /// <param name="implementationType">The implementation type.</param>
        /// <param name="serviceLifetime">The desired service lifetime.</param>
        public void AddService(Type interfaceType, Type implementationType, ServiceLifetime serviceLifetime)
        {
            _implementations[interfaceType] = implementationType;
            AddService(implementationType, serviceLifetime);
        }

        /// <summary>
        /// Adds a service implementation type with a specified lifetime.
        /// </summary>
        /// <param name="serviceType">The service implementation type.</param>
        /// <param name="serviceLifetime">The desired service lifetime.</param>
        public void AddService(Type serviceType, ServiceLifetime serviceLifetime)
        {
            _serviceLifetimes[serviceType] = serviceLifetime;
        }

        /// <summary>
        /// Checks if a service type (interface or implementation) is registered.
        /// </summary>
        /// <param name="serviceType">The service type to check.</param>
        /// <returns><c>true</c> if the service type is registered; otherwise, <c>false</c>.</returns>
        public bool ContainsService(Type serviceType)
        {
            return _serviceLifetimes.ContainsKey(serviceType) || _implementations.ContainsKey(serviceType);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the registered interface types.
        /// </summary>
        /// <returns>An enumerator for the registered interface types.</returns>
        public IEnumerator<Type> GetEnumerator()
        {
            return _implementations.Keys.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the registered interface types.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerator"/> object that can be used to iterate through the registered
        /// interface types.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <param name="serviceType">An object that specifies the type of service object to get.</param>
        /// <returns>
        /// A service object of type <paramref name="serviceType"/>. -or- null if there is no
        /// service object of type <paramref name="serviceType"/>.
        /// </returns>
        public object? GetService(Type serviceType)
        {
            return GetService(serviceType, null);
        }

        /// <summary>
        /// Gets the service object of the specified type, potentially within a specific scope.
        /// </summary>
        /// <param name="serviceType">An object that specifies the type of service object to get.</param>
        /// <param name="scope">
        /// The scope identifier. If null for a scoped service, a new scope is created.
        /// </param>
        /// <returns>
        /// A service object of type <paramref name="serviceType"/>. -or- null if there is no
        /// service object of type <paramref name="serviceType"/>.
        /// </returns>
        public object? GetService(Type serviceType, Guid? scope)
        {
            if (serviceType.IsInterface && !_implementations.TryGetValue(serviceType, out serviceType))
            {
                return null;
            }
            if (_serviceLifetimes.TryGetValue(serviceType, out ServiceLifetime serviceLifetime))
            {
                switch (serviceLifetime)
                {
                    case ServiceLifetime.Scoped:
                        if (scope.HasValue)
                        {
                            if (_scopedServices.TryGetValue(scope.Value, out WeakReference weakReference) && weakReference.IsAlive)
                            {
                                return weakReference.Target;
                            }
                        }
                        else
                        {
                            scope = Guid.NewGuid();
                        }
                        object scopedInstance = Activator.CreateInstance(serviceType);
                        _scopedServices[scope.Value] = new WeakReference(scopedInstance);
                        return scopedInstance;

                    case ServiceLifetime.Singleton:
                        if (_singletonServices.TryGetValue(serviceType, out object singletonInstance))
                        {
                            return singletonInstance;
                        }
                        else
                        {
                            singletonInstance = Activator.CreateInstance(serviceType);
                            _singletonServices[serviceType] = singletonInstance;
                            return singletonInstance;
                        }

                    case ServiceLifetime.Transient:
                        return Activator.CreateInstance(serviceType);

                    default:
                        return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the configured <see cref="ServiceLifetime"/> for the specified service type.
        /// </summary>
        /// <param name="serviceType">The service implementation type.</param>
        /// <returns>
        /// The configured <see cref="ServiceLifetime"/>, or null if the service type is not registered.
        /// </returns>
        public ServiceLifetime? GetServiceLifetime(Type serviceType)
        {
            _serviceLifetimes.TryGetValue(serviceType, out ServiceLifetime serviceLifetime);
            return serviceLifetime;
        }

        /// <summary>
        /// Gets the scope identifier associated with a given scoped service instance.
        /// </summary>
        /// <param name="instance">The scoped service instance.</param>
        /// <returns>
        /// The scope <see cref="Guid"/> if the instance is found and tracked as a scoped service;
        /// otherwise, <c>null</c>.
        /// </returns>
        public Guid? GetServiceScopeId(object instance)
        {
            foreach (KeyValuePair<Guid, WeakReference> keyValuePair in _scopedServices)
            {
                if (keyValuePair.Value.Target == instance)
                {
                    return keyValuePair.Key;
                }
            }
            return null;
        }

        /// <summary>
        /// Removes a registered service (interface and/or implementation) and its associated instances.
        /// </summary>
        /// <param name="serviceType">The service type (interface or implementation) to remove.</param>
        public void RemoveService(Type serviceType)
        {
            if (serviceType.IsInterface && _implementations.TryGetValue(serviceType, out Type implementationType))
            {
                _implementations.Remove(serviceType);
                serviceType = implementationType;
            }
            _serviceLifetimes.Remove(serviceType, out ServiceLifetime serviceLifetime);
            switch (serviceLifetime)
            {
                case ServiceLifetime.Scoped:
                    foreach (KeyValuePair<Guid, WeakReference> keyValuePair in _scopedServices)
                    {
                        if (keyValuePair.Value.Target?.GetType() == serviceType)
                        {
                            _scopedServices.Remove(keyValuePair.Key);
                        }
                    }
                    break;

                case ServiceLifetime.Singleton:
                    _singletonServices.Remove(serviceType);
                    break;

                default:
                    break;
            }
        }

        #endregion Public Methods
    }
}