using System.Net;

namespace Kevahu.Microservices.Gateway
{
    /// <summary>
    /// Defines configuration options for the RPC (Remote Procedure Call) functionality within the
    /// gateway. These options are typically configured using <see cref="Builder.RpcBuilderExtensions"/>.
    /// </summary>
    public class RpcOptions
    {
        #region Properties

        /// <summary>
        /// Gets the host name or IP address the gateway's RPC server should bind to. Set via <see cref="Builder.RpcBuilderExtensions.SetServer"/>.
        /// </summary>
        public IPEndPoint ServerEndpoint { get; set; }

        /// <summary>
        /// Gets the authentication token used by the <see
        /// cref="Middleware.RemoteProcedureCall.RpcSignInMiddleware"/> to validate incoming sign-in
        /// requests from remote RPC nodes. Set via <see cref="Builder.RpcBuilderExtensions.SetToken"/>.
        /// </summary>
        public string Token { get; set; }

        #endregion Properties
    }
}