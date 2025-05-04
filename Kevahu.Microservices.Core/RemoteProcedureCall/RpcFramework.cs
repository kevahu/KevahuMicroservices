using Kevahu.Microservices.Core.Collections;
using Kevahu.Microservices.Core.RemoteProcedureCall.Attributes;
using Kevahu.Microservices.Core.RemoteProcedureCall.DependencyInjection;
using Kevahu.Microservices.Core.RemoteProcedureCall.MessagePack;
using Kevahu.Microservices.Core.SecureSocket;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using static Kevahu.Microservices.Core.SecureSocket.SecureSocketServer;

namespace Kevahu.Microservices.Core.RemoteProcedureCall
{
    /// <summary>
    /// Facilitates remote procedure calls (RPC) between applications using SecureSockets. Assumes
    /// SecureSocket keys are pre-configured. Automatically generates client-side implementations
    /// for interfaces marked with <see cref="RpcInterfaceAttribute"/> if no local implementation
    /// exists. Method calls on these generated interfaces are routed to the appropriate remote server.
    /// </summary>
    public static class RpcFramework
    {
        /// <summary>
        /// Base interface for RPC request and response messages, used for serialization purposes.
        /// </summary>
        [Union(0, typeof(Request))]
        [Union(1, typeof(Response))]
        public interface ITransaction;

        /// <summary>
        /// Provides data for the <see cref="RpcFramework.OnConnected"/> event.
        /// </summary>
        public class ConnectedEventArgs : EventArgs
        {
            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="ConnectedEventArgs"/> class.
            /// </summary>
            /// <param name="friendlyName">The friendly name of the connected server or client.</param>
            public ConnectedEventArgs(string friendlyName)
            {
                FriendlyName = friendlyName;
            }

            #endregion Public Constructors

            #region Properties

            /// <summary>
            /// Gets the friendly name of the connected server or client.
            /// </summary>
            public string FriendlyName { get; }

            #endregion Properties
        }

        /// <summary>
        /// Provides data for the <see cref="RpcFramework.OnDisconnected"/> event.
        /// </summary>
        public class DisconnectedEventArgs : EventArgs
        {
            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="DisconnectedEventArgs"/> class.
            /// </summary>
            /// <param name="friendlyName">The friendly name of the disconnected server or client.</param>
            public DisconnectedEventArgs(string friendlyName)
            {
                FriendlyName = friendlyName;
            }

            #endregion Public Constructors

            #region Properties

            /// <summary>
            /// Gets the friendly name of the disconnected server or client.
            /// </summary>
            public string FriendlyName { get; }

            #endregion Properties
        }

        /// <summary>
        /// Provides data for the <see cref="RpcFramework.OnIncoming"/> event.
        /// </summary>
        public class IncomingEventArgs : EventArgs
        {
            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="IncomingEventArgs"/> class.
            /// </summary>
            /// <param name="elapsed">The time taken to process the incoming request.</param>
            /// <param name="exception">The exception that occurred during processing, if any.</param>
            /// <param name="forwarded">
            /// Indicates whether the request was forwarded to another server.
            /// </param>
            /// <param name="friendlyName">The friendly name of the client that sent the request.</param>
            /// <param name="isError">Indicates whether an error occurred during processing.</param>
            /// <param name="procedure">The name of the procedure that was invoked.</param>
            /// <param name="scope">The scope identifier associated with the request, if applicable.</param>
            public IncomingEventArgs(TimeSpan elapsed, Exception? exception, bool forwarded, string friendlyName, bool isError, string procedure, Guid? scope)
            {
                Elapsed = elapsed;
                Exception = exception;
                Forwarded = forwarded;
                FriendlyName = friendlyName;
                IsError = isError;
                Procedure = procedure;
                Scope = scope;
            }

            #endregion Public Constructors

            #region Properties

            /// <summary>
            /// Gets the time taken to process the incoming request.
            /// </summary>
            public TimeSpan Elapsed { get; }

            /// <summary>
            /// Gets the exception that occurred during processing, if any.
            /// </summary>
            public Exception? Exception { get; }

            /// <summary>
            /// Gets a value indicating whether the request was forwarded to another server.
            /// </summary>
            public bool Forwarded { get; }

            /// <summary>
            /// Gets the friendly name of the client that sent the request.
            /// </summary>
            public string FriendlyName { get; }

            /// <summary>
            /// Gets a value indicating whether an error occurred during processing.
            /// </summary>
            public bool IsError { get; }

            /// <summary>
            /// Gets the name of the procedure that was invoked.
            /// </summary>
            public string Procedure { get; }

            /// <summary>
            /// Gets the scope identifier associated with the request, if applicable.
            /// </summary>
            public Guid? Scope { get; }

            #endregion Properties
        }

        /// <summary>
        /// Provides data for the <see cref="RpcFramework.OnUnauthorized"/> event.
        /// </summary>
        public class UnauthorizedEventArgs : EventArgs
        {
            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="UnauthorizedEventArgs"/> class.
            /// </summary>
            /// <param name="friendlyName">The friendly name of the client that attempted to connect.</param>
            /// <param name="message">The message describing the unauthorized access attempt.</param>
            /// <param name="exception">
            /// The exception that occurred during the connection attempt, if any.
            /// </param>
            public UnauthorizedEventArgs(string remoteEndpoint, string message, Exception? exception)
            {
                RemoteEndpoint = remoteEndpoint;
                Message = message;
                Exception = exception;
            }

            #endregion Public Constructors

            #region Properties

            /// <summary>
            /// Gets the friendly name of the client that attempted to connect.
            /// </summary>
            public string RemoteEndpoint { get; }

            /// <summary>
            /// Gets the message describing the unauthorized access attempt.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Gets the exception that occurred during the connection attempt, if any.
            /// </summary>
            public Exception? Exception { get; }

            #endregion Properties
        }

        /// <summary>
        /// Represents an RPC request message.
        /// </summary>
        [MessagePackObject(true)]
        public class Request : ITransaction
        {
            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="Request"/> class for serialization purposes.
            /// </summary>
            public Request()
            { }

            /// <summary>
            /// Initializes a new instance of the <see cref="Request"/> class.
            /// </summary>
            /// <param name="id">The unique identifier for the request.</param>
            /// <param name="scope">The scope identifier, if the request is for a scoped service.</param>
            /// <param name="procedure">The name of the procedure to invoke (e.g., "ClassName.MethodName").</param>
            /// <param name="args">The arguments for the procedure.</param>
            public Request(Guid id, Guid? scope, string procedure, byte[] args)
            {
                Id = id;
                Scope = scope;
                Procedure = procedure;
                Args = args;
            }

            #endregion Public Constructors

            #region Properties

            /// <summary>
            /// Gets the arguments for the procedure.
            /// </summary>
            public byte[] Args { get; }

            /// <summary>
            /// Gets the unique identifier for the request.
            /// </summary>
            public Guid Id { get; }

            /// <summary>
            /// Gets the name of the procedure to invoke (e.g., "ClassName.MethodName").
            /// </summary>
            public string Procedure { get; }

            /// <summary>
            /// Gets the scope identifier, if the request is for a scoped service.
            /// </summary>
            public Guid? Scope { get; }

            #endregion Properties
        }

        /// <summary>
        /// Represents an RPC response message.
        /// </summary>
        [MessagePackObject(true)]
        public class Response : ITransaction
        {
            #region Public Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="Response"/> class for serialization purposes.
            /// </summary>
            public Response()
            { }

            /// <summary>
            /// Initializes a new instance of the <see cref="Response"/> class.
            /// </summary>
            /// <param name="id">The unique identifier of the corresponding request.</param>
            /// <param name="data">The return value of the procedure, if successful.</param>
            /// <param name="exception">The exception that occurred during execution, if any.</param>
            public Response(Guid id, byte[]? data, Exception? exception)
            {
                Id = id;
                Data = data;
                Exception = exception;
            }

            #endregion Public Constructors

            #region Properties

            /// <summary>
            /// Gets the exception that occurred during execution, if any.
            /// </summary>
            public Exception? Exception { get; }

            /// <summary>
            /// Gets or sets the unique identifier of the corresponding request.
            /// </summary>
            public Guid Id { get; internal set; }

            /// <summary>
            /// Gets the return value of the procedure, if successful.
            /// </summary>
            public byte[]? Data { get; }

            #endregion Properties
        }

        /// <summary>
        /// Static constructor for initializing the RpcFramework. Sets up the service provider,
        /// dynamic assembly for implementations, finds initial services, initializes collections,
        /// sets default timeout, and registers for process exit cleanup.
        /// </summary>
        static RpcFramework()
        {
            ServiceProvider = new RpcServiceProvider();
            _MyServices = [];

            string name = "RPC.Implementation";
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.RunAndCollect);
            _ModuleBuilder = assemblyBuilder.DefineDynamicModule(name);

            FindAndAddServices();

            _ConnectionPool = [];
            TimeoutMilliseconds = -1;
            _ServiceCatalogue = [];
            _RootServers = [];
            _InternalSerializerOptions = new RpcMessagePackSerializerOptions(MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(ContractlessStandardResolver.Instance));
            _ExternalSerializerOptions = new RpcMessagePackSerializerOptions(MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData).WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(TypelessContractlessStandardResolver.Instance));
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                foreach (BlockingCollection<Request> queries in _ConnectionPool.Values)
                {
                    queries.CompleteAdding();
                }
                foreach (KeyValuePair<Guid, Query> query in _Queries)
                {
                    query.Value.Callback.SetResult(new Response(query.Key, null, new TaskCanceledException("The request was canceled because the application is exiting.")));
                }
            };
        }

        /// <summary>
        /// Gets or sets a value indicating whether to allow mesh networking. If true, incoming
        /// requests for services not hosted locally will be forwarded to other known servers in the
        /// network that advertise the required service. Defaults to false.
        /// </summary>
        public static bool AllowMesh { get; set; }

        /// <summary>
        /// Gets the custom <see cref="RpcServiceProvider"/> used for managing the lifetimes and
        /// instances of local and remote RPC services.
        /// </summary>
        public static RpcServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Gets or sets the timeout in milliseconds for waiting for a response to an RPC request. A
        /// value of -1 indicates an infinite timeout. Defaults to -1.
        /// </summary>
        public static int TimeoutMilliseconds { get; set; }

        /// <summary>
        /// Stores outgoing request queues for each connected server.
        /// Key: Friendly name of the server. Value: BlockingCollection of requests to send.
        /// </summary>
        private static readonly ConcurrentDictionary<string, BlockingCollection<Request>> _ConnectionPool;

        /// <summary>
        /// The dynamic module builder used to create runtime implementations of RPC interfaces.
        /// </summary>
        private static readonly ModuleBuilder _ModuleBuilder;

        /// <summary>
        /// Stores locally implemented RPC services.
        /// Key: Trimmed interface name (without 'I'). Value: Implementation type.
        /// </summary>
        private static readonly Dictionary<string, Type> _MyServices;

        /// <summary>
        /// Stores pending outgoing queries and their corresponding TaskCompletionSource for
        /// receiving responses.
        /// Key: Unique request ID (Guid). Value: Query object containing callback and target server name.
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, Query> _Queries = [];

        /// <summary>
        /// Stores the friendly names of designated root servers used for service discovery fallback.
        /// </summary>
        private static readonly HashSet<string> _RootServers; // TODO: Make concurrent

        /// <summary>
        /// MessagePack serialization options configured for contractless resolving and untrusted
        /// data security.
        /// </summary>
        private static readonly RpcMessagePackSerializerOptions _InternalSerializerOptions;

        /// <summary>
        /// MessagePack serialization options configured for contractless resolving and untrusted
        /// data security.
        /// </summary>
        private static readonly RpcMessagePackSerializerOptions _ExternalSerializerOptions;

        /// <summary>
        /// Stores the mapping of known remote service interfaces to the friendly names of servers
        /// that provide them.
        /// Key: Trimmed interface name (without 'I'). Value: Friendly name of the server.
        /// </summary>
        private static readonly ConcurrentMultiMap<string, string> _ServiceCatalogue;

        /// <summary>
        /// The main listening socket when running in server mode. Null if not started or running as
        /// client only.
        /// </summary>
        private static Socket _Server;

        /// <summary>
        /// Occurs when a new client connects to the server or a connection to a remote server is established.
        /// </summary>
        public static event EventHandler<ConnectedEventArgs>? OnConnected;

        /// <summary>
        /// Occurs when a client disconnects from the server or a connection to a remote server is lost.
        /// </summary>
        public static event EventHandler<DisconnectedEventArgs>? OnDisconnected;

        /// <summary>
        /// Occurs when an incoming RPC request is received and processed by the server. Provides
        /// details about the request processing, including duration, success/failure, and any exceptions.
        /// </summary>
        public static event EventHandler<IncomingEventArgs>? OnIncoming;

        /// <summary>
        /// Calculates the processor affinity mask for distributing connection handling threads.
        /// </summary>
        /// <param name="current">The index of the current thread/connection (0-based).</param>
        /// <param name="max">The total number of threads/connections being distributed.</param>
        /// <returns>The affinity mask ( <see cref="nint"/>) for the specified thread index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="current"/> is not less than <paramref name="max"/>, or if
        /// <paramref name="max"/> is greater than the system's processor count.
        /// </exception>
        private static nint CalculateAffinity(byte current, byte max)
        {
            int processors = Environment.ProcessorCount;
            if (current >= max)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "Current must be less than the maximum.");
            }
            if (max > processors)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, $"Maximum must be less than or equal to the number of processors ({processors}).");
            }

            nint affinityMask = 0;
            int bitsPerGroup = processors / max;
            for (int i = 0; i < max; i++)
            {
                if (i == current)
                {
                    for (int j = 0; j < bitsPerGroup; j++)
                    {
                        affinityMask |= (nint)1 << (i * bitsPerGroup + j);
                    }
                }
            }

            return affinityMask;
        }

        /// <summary>
        /// Establishes connections to a remote RPC server and registers its advertised services.
        /// </summary>
        /// <param name="host">The hostname or IP address of the remote server.</param>
        /// <param name="port">The port number the remote server is listening on.</param>
        /// <param name="friendlyName">
        /// The expected friendly name of the remote server (must match its public key).
        /// </param>
        /// <param name="connections">
        /// The number of parallel connections to establish for sending requests. Defaults to 2.
        /// </param>
        /// <param name="allowReverse">
        /// If true, establishes additional connections where the remote server can send requests
        /// back to this instance. The total number of connections will be doubled. Defaults to false.
        /// </param>
        /// <param name="isRoot">
        /// If true, designates this server as a root server for service discovery fallback.
        /// Defaults to false.
        /// </param>
        public static void AddServer(string host, ushort port, string friendlyName, byte connections = 2, bool allowReverse = false, bool isRoot = false)
        {
            if (isRoot)
            {
                _RootServers.Add(friendlyName);
            }
            BlockingCollection<Request> queries = _ConnectionPool.GetOrAdd(friendlyName, (key) => []);
            for (byte i = 0; i < connections; i++)
            {
                SecureSocketConnection connection = Connect(host, port, friendlyName);
                new Thread(ClientRequestHandler).Start(new ClientRequestHandlerArguments(queries, connection, CalculateAffinity(i, connections)));
                new Thread(ClientResponseHandler).Start(new ClientResponseHandlerArguments(connection, CalculateAffinity(i, connections)));
            }
            if (allowReverse)
            {
                for (byte i = 0; i < connections; i++)
                {
                    SecureSocketConnection connection = Connect(host, port, friendlyName);
                    connection.Revert();
                    connection.Send(MessagePackSerializer.Serialize(_MyServices.Keys.ToArray(), _InternalSerializerOptions));
                    new Thread(ClientResponseHandler).Start(new ClientResponseHandlerArguments(connection, CalculateAffinity(i, connections)));
                }
            }
        }

        /// <summary>
        /// Scans all assemblies in the current application domain for RPC interfaces and
        /// implementations and registers them with the <see cref="ServiceProvider"/>. Generates
        /// dynamic implementations for interfaces marked with <see cref="RpcInterfaceAttribute"/>
        /// that do not have a corresponding local implementation marked with <see cref="RpcImplementationAttribute"/>.
        /// </summary>
        public static void FindAndAddServices()
        {
            FindAndAddServices(AppDomain.CurrentDomain.GetAssemblies());
        }

        /// <summary>
        /// Scans the specified assemblies for RPC interfaces and implementations and registers them
        /// with the <see cref="ServiceProvider"/>. Generates dynamic implementations for interfaces
        /// marked with <see cref="RpcInterfaceAttribute"/> that do not have a corresponding local
        /// implementation marked with <see cref="RpcImplementationAttribute"/>.
        /// </summary>
        /// <param name="assemblies">The collection of assemblies to scan.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a class marked with <see cref="RpcImplementationAttribute"/> does not
        /// implement at least one interface marked with <see cref="RpcInterfaceAttribute"/>.
        /// </exception>
        public static void FindAndAddServices(IEnumerable<Assembly> assemblies)
        {
            List<Type> interfaces = [];
            foreach (Assembly assembly in assemblies)
            {
                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if (type.IsInterface && type.GetCustomAttribute<RpcInterfaceAttribute>() != null && !ServiceProvider.ContainsService(type))
                    {
                        interfaces.Add(type);
                    }
                    else if (type.IsClass && type.GetCustomAttribute<RpcImplementationAttribute>() != null)
                    {
                        var implementedInterfaces = type.FindInterfaces((interfaceType, criteria) =>
                        {
                            RpcInterfaceAttribute? rpcInterfaceAttribute = interfaceType.GetCustomAttribute<RpcInterfaceAttribute>();
                            if (rpcInterfaceAttribute != null)
                            {
                                interfaces.Remove(interfaceType);
                                ServiceProvider.AddService(interfaceType, type, rpcInterfaceAttribute.Type);
                                return true;
                            }

                            return false;
                        }, null);
                        if (implementedInterfaces.Length == 0)
                        {
                            throw new InvalidOperationException($"The class {type.FullName} does not implement any interfaces with the RpcInterface attribute.");
                        }
                        foreach (var implementation in implementedInterfaces)
                        {
                            _MyServices[implementation.Name.TrimStart('I')] = type;
                        }
                    }
                }
            }

            foreach (Type type in interfaces)
            {
                RpcInterfaceAttribute rpcInterfaceAttribute = type.GetCustomAttribute<RpcInterfaceAttribute>();
                string procedureName = type.Name.TrimStart('I');
                TypeBuilder typeBuilder = _ModuleBuilder.DefineType(type.FullName + ".Implementation", TypeAttributes.Public | TypeAttributes.Class, null, [type]);
                List<MethodInfo> methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).ToList();

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, Type.EmptyTypes);

                    if (methods.Remove(property.GetMethod))
                    {
                        MethodBuilder getMethodBuilder = typeBuilder.DefineMethod($"get_{propertyBuilder.Name}", MethodAttributes.Public | MethodAttributes.Virtual, propertyBuilder.PropertyType, Type.EmptyTypes);
                        ILGenerator iLGenerator = getMethodBuilder.GetILGenerator();
                        iLGenerator.EmitInvoke(procedureName + "." + getMethodBuilder.Name, Type.EmptyTypes, property.PropertyType);
                        propertyBuilder.SetGetMethod(getMethodBuilder);
                    }
                    if (methods.Remove(property.SetMethod))
                    {
                        MethodBuilder setMethodBuilder = typeBuilder.DefineMethod($"set_{propertyBuilder.Name}", MethodAttributes.Public | MethodAttributes.Virtual, null, [property.PropertyType]);
                        ILGenerator iLGenerator = setMethodBuilder.GetILGenerator();
                        iLGenerator.EmitInvoke(procedureName + "." + setMethodBuilder.Name, [property.PropertyType]);
                        propertyBuilder.SetSetMethod(setMethodBuilder);
                    }
                }
                foreach (MethodInfo method in methods)
                {
                    MethodBuilder methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual, method.ReturnType, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
                    ILGenerator iLGenerator = methodBuilder.GetILGenerator();
                    iLGenerator.EmitInvoke(procedureName + "." + methodBuilder.Name, [.. method.GetParameters().Select(parameter => parameter.ParameterType)], method.ReturnType);
                }
                Type implementation = typeBuilder.CreateType();
                ServiceProvider.AddService(type, implementation, rpcInterfaceAttribute.Type);
            }
        }

        /// <summary>
        /// Invokes a remote procedure call (RPC) identified by the procedure name and arguments,
        /// targeting the appropriate server based on the service catalogue. Returns the result.
        /// This overload is typically used by the dynamically generated interface implementations.
        /// </summary>
        /// <typeparam name="T">The expected return type of the procedure.</typeparam>
        /// <param name="instance">
        /// The instance of the dynamically generated RPC interface proxy, used to potentially
        /// resolve scoped services.
        /// </param>
        /// <param name="procedure">The name of the procedure to invoke (e.g., "ClassName.MethodName").</param>
        /// <param name="args">The arguments to pass to the remote procedure.</param>
        /// <returns>
        /// The result returned by the remote procedure, cast to type <typeparamref name="T"/>.
        /// </returns>
        /// <exception cref="TargetInvocationException">
        /// Wraps any exception thrown by the remote procedure.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the procedure name is invalid, or no server is found for the service, or no
        /// connection pool exists.
        /// </exception>
        /// <exception cref="TaskCanceledException">
        /// Thrown if the request times out or the connection is lost before a response is received.
        /// </exception>
        public static T Invoke<T>(object? instance, string procedure, params object[] args)
        {
            Response response = ProcessInvoke(ServiceProvider.GetServiceScopeId(instance), procedure, MessagePackSerializer.Serialize(args, _ExternalSerializerOptions)).Result;
            if (response.Exception != null)
            {
                throw response.Exception;
            }
            return MessagePackSerializer.Deserialize<T>(response.Data, _ExternalSerializerOptions);
        }

        /// <summary>
        /// Invokes a remote procedure call (RPC) identified by the procedure name and arguments,
        /// targeting the appropriate server based on the service catalogue. Does not return a
        /// value. This overload is typically used by the dynamically generated interface
        /// implementations for void methods.
        /// </summary>
        /// <param name="instance">
        /// The instance of the dynamically generated RPC interface proxy, used to potentially
        /// resolve scoped services.
        /// </param>
        /// <param name="procedure">The name of the procedure to invoke (e.g., "ClassName.MethodName").</param>
        /// <param name="args">The arguments to pass to the remote procedure.</param>
        /// <exception cref="TargetInvocationException">
        /// Wraps any exception thrown by the remote procedure.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the procedure name is invalid, or no server is found for the service, or no
        /// connection pool exists.
        /// </exception>
        /// <exception cref="TaskCanceledException">
        /// Thrown if the request times out or the connection is lost before a response is received.
        /// </exception>
        public static void Invoke(object? instance, string procedure, params object[] args)
        {
            Response response = ProcessInvoke(ServiceProvider.GetServiceScopeId(instance), procedure, MessagePackSerializer.Serialize(args, _ExternalSerializerOptions)).Result;
            if (response.Exception != null)
            {
                throw response.Exception;
            }
        }

        /// <summary>
        /// Checks if a connection pool exists for the specified server friendly name, indicating an
        /// active or recently active connection attempt.
        /// </summary>
        /// <param name="friendlyName">The friendly name of the server.</param>
        /// <returns><c>true</c> if a connection pool exists for the server; otherwise, <c>false</c>.</returns>
        public static bool IsConnected(string friendlyName)
        {
            return _ConnectionPool.ContainsKey(friendlyName);
        }

        /// <summary>
        /// Gets the list of currently connected servers. This includes servers that are connected,
        /// as well as those that have been recently disconnected but still have an active
        /// connection pool.
        /// </summary>
        public static HashSet<string> Connections => _ConnectionPool.Keys.ToHashSet();

        /// <summary>
        /// This event is triggered when an unauthorized client attempts to connect to the server.
        /// </summary>
        public static event EventHandler<UnauthorizedEventArgs>? OnUnauthorized;

        /// <summary>
        /// Starts the RPC server, listening for incoming client connections on the specified host
        /// and port.
        /// </summary>
        /// <param name="host">The IP address or hostname to bind the server to.</param>
        /// <param name="port">The port number to listen on.</param>
        /// <param name="block">
        /// If true, the method blocks until the server is stopped (e.g., by cancellation). If
        /// false, the server runs in the background. Defaults to true.
        /// </param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> to signal server shutdown.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the server is already running ( <see cref="_Server"/> is not null).
        /// </exception>
        public static void StartServer(string host, ushort port, bool block = true, CancellationToken cancellation = default)
        {
            if (_Server != null)
            {
                throw new InvalidOperationException("The server is already running.");
            }
            _Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _Server.Bind(new IPEndPoint(IPAddress.Parse(host), port));
            _Server.Listen();
            SecureSocketServer secureSocketServer = new SecureSocketServer(_Server);
            secureSocketServer.OnClientConnected += ClientConnected;
            secureSocketServer.OnClientUnauthorised += (sender, e) =>
            {
                OnUnauthorized?.Invoke(null, new UnauthorizedEventArgs(e.Client.RemoteEndPoint?.ToString(), e.Message, e.Exception));
            };
            secureSocketServer.Start(block, cancellation);
        }

        /// <summary>
        /// Handles the event when a new client connects via the SecureSocketServer. Sets up
        /// communication, exchanges service catalogues, and starts handling incoming requests.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">Event arguments containing the connected client.</param>
        private static void ClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            SecureSocketConnection client = e.Client;
            ThreadPool.QueueUserWorkItem(state => OnConnected?.Invoke(null, new ConnectedEventArgs(client.FriendlyName)));

            bool hasReverted = false;
            client.Reverted += (sender, e) =>
            {
                hasReverted = true;
            };
            client.Send(MessagePackSerializer.Serialize(_MyServices.Keys.ToArray(), _InternalSerializerOptions));

            while (client.Connected)
            {
                try
                {
                    byte[] bytes = client.Receive();
                    if (hasReverted)
                    {
                        hasReverted = false;
                        try
                        {
                            byte[] catalogueResponse = client.Receive();
                            foreach (string interfaceName in MessagePackSerializer.Deserialize<string[]>(catalogueResponse, _InternalSerializerOptions))
                            {
                                _ServiceCatalogue.Add(interfaceName, client.FriendlyName);
                            }
                            new Thread(() => HandleOutAsync(_ConnectionPool.GetOrAdd(client.FriendlyName, key => []), client)).Start();
                        }
                        catch (Exception ex)
                        {
                            Disconnect(client.FriendlyName);
                        }
                        continue;
                    }
                    Task.Run(() => HandleInAsync(client, bytes));
                }
                catch (Exception ex)
                {
                    if (!client.Connected)
                    {
                        Disconnect(client.FriendlyName);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            Disconnect(client.FriendlyName);
        }

        /// <summary>
        /// Handles the outgoing request queue for a specific client connection thread. Runs on a
        /// dedicated thread with specific processor affinity.
        /// </summary>
        /// <param name="state">
        /// A <see cref="ClientRequestHandlerArguments"/> object containing the queue, connection,
        /// and affinity mask.
        /// </param>
        private static async void ClientRequestHandler(object? state)
        {
            ClientRequestHandlerArguments arguments = (ClientRequestHandlerArguments)state;
            Process.GetCurrentProcess().ProcessorAffinity = arguments.AffinityMask;
            await HandleOutAsync(arguments.Queries, arguments.Connection);
        }

        /// <summary>
        /// Handles incoming responses on a specific client connection thread. Runs on a dedicated
        /// thread with specific processor affinity.
        /// </summary>
        /// <param name="state">
        /// A <see cref="ClientResponseHandlerArguments"/> object containing the connection and
        /// affinity mask.
        /// </param>
        private static void ClientResponseHandler(object? state)
        {
            ClientResponseHandlerArguments arguments = (ClientResponseHandlerArguments)state;
            Process.GetCurrentProcess().ProcessorAffinity = arguments.AffinityMask;
            SecureSocketConnection client = arguments.Connection;

            while (client.Connected)
            {
                try
                {
                    byte[] bytes = client.Receive();
                    Task.Run(() => HandleInAsync(client, bytes));
                }
                catch (Exception ex)
                {
                    if (!client.Connected)
                    {
                        Disconnect(client.FriendlyName);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            Disconnect(client.FriendlyName);
        }

        /// <summary>
        /// Establishes a single SecureSocket connection to a remote server and exchanges service catalogues.
        /// </summary>
        /// <param name="host">The hostname or IP address of the server.</param>
        /// <param name="port">The port number of the server.</param>
        /// <param name="friendlyName">The expected friendly name of the server.</param>
        /// <returns>The established <see cref="SecureSocketConnection"/>.</returns>
        private static SecureSocketConnection Connect(string host, ushort port, string friendlyName)
        {
            SecureSocketConnection connection = new SecureSocketClient(new Socket(SocketType.Stream, ProtocolType.Tcp), new IPEndPoint(IPAddress.Parse(host), port), friendlyName).Connect();
            byte[] catalogResponse = connection.Receive();
            foreach (string interfaceName in MessagePackSerializer.Deserialize<string[]>(catalogResponse, _InternalSerializerOptions))
            {
                _ServiceCatalogue.Add(interfaceName, friendlyName);
            }
            return connection;
        }

        /// <summary>
        /// Cleans up resources associated with a disconnected server. Removes the server from the
        /// service catalogue, completes its connection pool queue, cancels pending queries, and
        /// raises the OnDisconnected event.
        /// </summary>
        /// <param name="friendlyName">The friendly name of the disconnected server.</param>
        private static void Disconnect(string friendlyName)
        {
            _ServiceCatalogue.RemoveByValue(friendlyName);
            if (_ConnectionPool.TryRemove(friendlyName, out BlockingCollection<Request> queries))
            {
                queries.CompleteAdding();
                ThreadPool.QueueUserWorkItem(state => OnDisconnected?.Invoke(null, new DisconnectedEventArgs(friendlyName)));
            }
            foreach (KeyValuePair<Guid, Query> query in _Queries.Where(kv => kv.Value.FriendlyName == friendlyName).ToList())
            {
                query.Value.Callback.SetResult(new Response(query.Key, null, new TaskCanceledException("The request was canceled because the server was disconnected.")));
                _Queries.TryRemove(query.Key, out _);
            }
            _RootServers.Remove(friendlyName);
        }

        /// <summary>
        /// Emits the Intermediate Language (IL) instructions to invoke the appropriate
        /// RpcFramework.Invoke method. Used during the dynamic generation of RPC interface implementations.
        /// </summary>
        /// <param name="iLGenerator">The IL generator for the method being built.</param>
        /// <param name="procedure">The procedure name string (e.g., "ClassName.MethodName").</param>
        /// <param name="argTypes">The types of the arguments being passed.</param>
        /// <param name="returnType">
        /// The return type of the method, or null/void if it returns void.
        /// </param>
        private static void EmitInvoke(this ILGenerator iLGenerator, string procedure, Type[] argTypes, Type? returnType = null)
        {
            iLGenerator.Emit(OpCodes.Ldarg_0);
            iLGenerator.Emit(OpCodes.Ldstr, procedure);
            iLGenerator.Emit(OpCodes.Ldc_I4, argTypes.Length);
            iLGenerator.Emit(OpCodes.Newarr, typeof(object));
            for (int i = 0; i < argTypes.Length; i++)
            {
                iLGenerator.Emit(OpCodes.Dup);
                iLGenerator.Emit(OpCodes.Ldc_I4, i);
                iLGenerator.Emit(OpCodes.Ldarg, i + 1);
                if (argTypes[i].IsValueType)
                {
                    iLGenerator.Emit(OpCodes.Box, argTypes[i]);
                }
                iLGenerator.Emit(OpCodes.Stelem_Ref);
            }
            if (returnType == null || returnType == typeof(void))
            {
                iLGenerator.Emit(OpCodes.Call, typeof(RpcFramework).GetMethods().Single(method => method.Name == nameof(Invoke) && !method.IsGenericMethod));
            }
            else
            {
                iLGenerator.Emit(OpCodes.Call, typeof(RpcFramework).GetMethods().Single(method => method.Name == nameof(Invoke) && method.IsGenericMethod).MakeGenericMethod(returnType));
            }
            iLGenerator.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Handles an incoming data packet (request or response) received on a connection.
        /// Deserializes the transaction, processes requests locally or forwards them, and completes
        /// pending queries upon receiving responses.
        /// </summary>
        /// <param name="connection">The connection the data was received on.</param>
        /// <param name="requestData">The raw byte data received.</param>
        private static async Task HandleInAsync(SecureSocketConnection connection, byte[] requestData)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ITransaction transaction = MessagePackSerializer.Deserialize<ITransaction>(requestData, _InternalSerializerOptions);

            if (transaction is Request request)
            {
                string typeName = null;
                string methodName = null;
                try
                {
                    typeName = request.Procedure.Split('.')[0];
                    methodName = request.Procedure.Split('.')[1];
                }
                catch
                {
                    InvalidOperationException exception = new InvalidOperationException("Invalid procedure name.");
                    await connection.SendAsync(MessagePackSerializer.Serialize<ITransaction>(new Response(request.Id, null, exception), _InternalSerializerOptions), TimeoutMilliseconds, CancellationToken.None);
                    stopwatch.Stop();
                    ThreadPool.QueueUserWorkItem(state => OnIncoming?.Invoke(null, new IncomingEventArgs(stopwatch.Elapsed, exception, false, connection.FriendlyName, true, request.Procedure, request.Scope)));
                    return;
                }
                MethodInfo methodInfo = null;

                if (_MyServices.TryGetValue(typeName, out Type implementationType))
                {
                    try
                    {
                        methodInfo = implementationType.GetMethod(methodName);
                    }
                    catch (Exception ex)
                    {
                        await connection.SendAsync(MessagePackSerializer.Serialize<ITransaction>(new Response(request.Id, null, ex), _InternalSerializerOptions), TimeoutMilliseconds, CancellationToken.None);
                        stopwatch.Stop();
                        ThreadPool.QueueUserWorkItem(state => OnIncoming?.Invoke(null, new IncomingEventArgs(stopwatch.Elapsed, ex, false, connection.FriendlyName, true, request.Procedure, request.Scope)));
                        return;
                    }
                }
                else if (AllowMesh && _ServiceCatalogue.ContainsKey(typeName))
                {
                    Response response = null;
                    for (int retry = 3; retry > 0; retry--)
                    {
                        try
                        {
                            response = await ProcessInvoke(request.Scope, request.Procedure, request.Args);
                            response.Id = request.Id;
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (retry <= 1)
                            {
                                response = new Response(request.Id, null, ex);
                            }
                        }
                    }
                    await connection.SendAsync(MessagePackSerializer.Serialize<ITransaction>(response, _InternalSerializerOptions), TimeoutMilliseconds, CancellationToken.None);
                    stopwatch.Stop();
                    ThreadPool.QueueUserWorkItem(state => OnIncoming?.Invoke(null, new IncomingEventArgs(stopwatch.Elapsed, null, true, connection.FriendlyName, false, request.Procedure, request.Scope)));
                    return;
                }
                else
                {
                    InvalidOperationException exception = new InvalidOperationException("Procedure not found.");
                    await connection.SendAsync(MessagePackSerializer.Serialize<ITransaction>(new Response(request.Id, null, exception), _InternalSerializerOptions), TimeoutMilliseconds, CancellationToken.None);
                    stopwatch.Stop();
                    ThreadPool.QueueUserWorkItem(state => OnIncoming?.Invoke(null, new IncomingEventArgs(stopwatch.Elapsed, exception, false, connection.FriendlyName, true, request.Procedure, request.Scope)));
                    return;
                }
                try
                {
                    ServiceLifetime? serviceLifetime = ServiceProvider.GetServiceLifetime(implementationType);
                    object? implementation;
                    if (serviceLifetime == ServiceLifetime.Scoped)
                    {
                        implementation = ServiceProvider.GetService(implementationType, request.Scope);
                    }
                    else
                    {
                        implementation = ServiceProvider.GetService(implementationType);
                    }
                    object[] args = MessagePackSerializer.Deserialize<object[]>(request.Args, _ExternalSerializerOptions);
                    object? value = methodInfo.Invoke(implementation, args);
                    await connection.SendAsync(MessagePackSerializer.Serialize<ITransaction>(new Response(request.Id, methodInfo.ReturnType == typeof(void) ? null : MessagePackSerializer.Serialize(methodInfo.ReturnType, value, _ExternalSerializerOptions), null), _InternalSerializerOptions), TimeoutMilliseconds, CancellationToken.None);
                    stopwatch.Stop();
                    ThreadPool.QueueUserWorkItem(state => OnIncoming?.Invoke(null, new IncomingEventArgs(stopwatch.Elapsed, null, false, connection.FriendlyName, false, request.Procedure, request.Scope)));
                }
                catch (Exception ex)
                {
                    if (ex is TargetInvocationException targetInvocationException)
                    {
                        ex = targetInvocationException.InnerException ?? targetInvocationException;
                    }
                    await connection.SendAsync(MessagePackSerializer.Serialize<ITransaction>(new Response(request.Id, null, ex), _InternalSerializerOptions), TimeoutMilliseconds, CancellationToken.None);
                    stopwatch.Stop();
                    ThreadPool.QueueUserWorkItem(state => OnIncoming?.Invoke(null, new IncomingEventArgs(stopwatch.Elapsed, ex, false, connection.FriendlyName, true, request.Procedure, request.Scope)));
                }
            }
            else if (transaction is Response response)
            {
                if (_Queries.TryRemove(response.Id, out Query query))
                {
                    if (response.Exception != null)
                    {
                        query.Callback.SetResult(new Response(response.Id, null, response.Exception));
                    }
                    else
                    {
                        query.Callback.SetResult(new Response(response.Id, response.Data, null));
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Invalid transaction type.");
            }
        }

        /// <summary>
        /// Handles sending outgoing requests from a blocking collection over a specific connection.
        /// Runs typically in a dedicated thread per connection.
        /// </summary>
        /// <param name="requests">The blocking collection containing requests to send.</param>
        /// <param name="connection">The connection to send requests over.</param>
        private static async Task HandleOutAsync(BlockingCollection<Request> requests, SecureSocketConnection connection)
        {
            foreach (Request request in requests.GetConsumingEnumerable())
            {
                try
                {
                    var test = MessagePackSerializer.Serialize<ITransaction>(request, _InternalSerializerOptions);
                    await connection.SendAsync(test, TimeoutMilliseconds, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    if (!connection.Connected)
                    {
                        Disconnect(connection.FriendlyName);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Determines the target server for an RPC invocation, creates the request, adds it to the
        /// appropriate connection pool queue, and returns a task that completes when the response
        /// is received.
        /// </summary>
        /// <param name="scope">The scope identifier, if applicable.</param>
        /// <param name="procedure">The procedure name (e.g., "ClassName.MethodName").</param>
        /// <param name="args">The arguments for the procedure.</param>
        /// <returns>A <see cref="Task{Response}"/> that represents the asynchronous RPC operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the procedure name is invalid, or no server is found for the service, or no
        /// connection pool exists.
        /// </exception>
        private static Task<Response> ProcessInvoke(Guid? scope, string procedure, byte[] args)
        {
            string typeName;
            try
            {
                typeName = procedure.Split('.')[0];
                _ = procedure.Split('.')[1];
            }
            catch
            {
                throw new InvalidOperationException($"Invalid procedure name: {procedure}.");
            }
            if (_ServiceCatalogue.TryGetValues(typeName, out HashSet<string> potentialFriendlyNames) || _RootServers.Count > 0)
            {
                potentialFriendlyNames = potentialFriendlyNames ?? _RootServers;
                string chosenFriendlyName;
                BlockingCollection<Request>? queries = null;
                if (potentialFriendlyNames.Count == 0)
                {
                    throw new InvalidOperationException($"No server found for {typeName}.");
                }
                else if (potentialFriendlyNames.Count == 1)
                {
                    chosenFriendlyName = potentialFriendlyNames.First();
                }
                else
                {
                    int min = int.MaxValue;
                    var result = _ConnectionPool.Where(kv => potentialFriendlyNames.Contains(kv.Key)).OrderBy(kv =>
                    {
                        min = Math.Min(min, kv.Value.Count);
                        return kv.Value.Count;
                    }).ThenBy(kv => Random.Shared.Next()).First();
                    chosenFriendlyName = result.Key;
                    queries = result.Value;
                }

                if (queries != null || _ConnectionPool.TryGetValue(chosenFriendlyName, out queries))
                {
                    Guid id = Guid.NewGuid();
                    TaskCompletionSource<Response> taskCompletionSource = _Queries.GetOrAdd(id, id => new Query(chosenFriendlyName, new TaskCompletionSource<Response>())).Callback;
                    queries.Add(new Request(id, scope, procedure, args));
                    return taskCompletionSource.Task;
                }
                else
                {
                    throw new InvalidOperationException($"No connection pool found for {chosenFriendlyName}.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Implementation of {typeName} not found.");
            }
        }

        /// <summary>
        /// Record holding arguments for the ClientResponseHandler thread.
        /// </summary>
        /// <param name="Connection">The client connection.</param>
        /// <param name="AffinityMask">The processor affinity mask.</param>
        private sealed record ClientResponseHandlerArguments(SecureSocketConnection Connection, nint AffinityMask);
        /// <summary>
        /// Record holding arguments for the ClientRequestHandler thread.
        /// </summary>
        /// <param name="Queries">The outgoing request queue.</param>
        /// <param name="Connection">The client connection.</param>
        /// <param name="AffinityMask">The processor affinity mask.</param>
        private sealed record ClientRequestHandlerArguments(BlockingCollection<Request> Queries, SecureSocketConnection Connection, nint AffinityMask);
        /// <summary>
        /// Record holding information about a pending query.
        /// </summary>
        /// <param name="FriendlyName">The friendly name of the target server.</param>
        /// <param name="Callback">The TaskCompletionSource to signal when the response arrives.</param>
        private sealed record Query(string FriendlyName, TaskCompletionSource<Response> Callback);
    }
}