using Microsoft.Extensions.DependencyInjection;

namespace Kevahu.Microservices.Core.RemoteProcedureCall.Attributes
{
    /// <summary>
    /// Marks an interface as a Remote Procedure Call (RPC) contract. This attribute is used by the
    /// system to identify interfaces that define RPC operations and optionally specify the desired
    /// service lifetime for their implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class RpcInterfaceAttribute : Attribute
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcInterfaceAttribute"/> class.
        /// </summary>
        /// <param name="type">
        /// The desired <see cref="ServiceLifetime"/> for the RPC implementation. Defaults to <see cref="ServiceLifetime.Singleton"/>.
        /// </param>
        public RpcInterfaceAttribute(ServiceLifetime type = ServiceLifetime.Singleton)
        {
            Type = type;
        }

        #endregion Public Constructors

        #region Properties

        /// <summary>
        /// Gets the specified <see cref="ServiceLifetime"/> for the RPC interface implementation.
        /// </summary>
        public ServiceLifetime Type { get; private set; }

        #endregion Properties
    }
}