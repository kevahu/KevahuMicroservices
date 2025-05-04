using Microsoft.Extensions.Options;

namespace Kevahu.Microservices.Gateway.Builder
{
    /// <summary>
    /// Provides access to RPC configuration options. Implements <see cref="IOptions{RpcOptions}"/>
    /// to make <see cref="RpcOptions"/> available through dependency injection patterns, although
    /// it currently holds a directly instantiated options object.
    /// </summary>
    public class RpcBuilder : IOptions<RpcOptions>
    {
        #region Properties

        /// <summary>
        /// Gets the configured <see cref="RpcOptions"/> instance.
        /// </summary>
        public RpcOptions Value { get; } = new RpcOptions();

        #endregion Properties
    }
}