using Kevahu.Microservices.Core.RemoteProcedureCall;
using Kevahu.Microservices.Core.SecureSocket;
using Kevahu.Microservices.Gateway.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Collections.ObjectModel;

namespace Kevahu.Microservices.Gateway.Middleware.RemoteProcedureCall
{
    /// <summary>
    /// Middleware designed to handle a specific "sign-in" request from remote RPC nodes. It
    /// intercepts PATCH requests to the root path ("/") containing specific headers ("Token",
    /// "Friendly-Name"). Validates the token, adds the sender's public key (from the request body)
    /// to the trusted keys if valid and not already connected, and optionally updates YARP reverse
    /// proxy configuration based on "Routes" and "Base" headers.
    /// </summary>
    public class RpcSignInMiddleware : IMiddleware
    {
        #region Public Methods

        /// <summary>
        /// Processes incoming HTTP requests. Intercepts and handles RPC sign-in requests, otherwise
        /// passes the request to the next middleware.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <param name="next">The delegate representing the next middleware in the pipeline.</param>
        /// <returns>A <see cref="Task"/> representing the completion of the middleware execution.</returns>
        /// <remarks>
        /// The sign-in request expects:
        /// - Method: PATCH
        /// - Path: "/"
        /// - Header "Token": Matches the configured <see cref="RpcOptions.Token"/>.
        /// - Header "Friendly-Name": The unique name of the connecting RPC node.
        /// - Body: The public key (PKCS#1 format) of the connecting RPC node.
        /// - Optional Header "Routes": JSON configuration for YARP routes.
        /// - Optional Header "Base": The base URI for the connecting node's services.
        ///
        /// Responses:
        /// - 202 Accepted: Sign-in successful, key added.
        /// - 208 Already Reported: Node with this friendly name is already connected via RPC.
        /// - 400 Bad Request: Invalid "Base" URI or "Routes" configuration.
        /// - 401 Unauthorized: Invalid "Token".
        /// </remarks>
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            RpcOptions options = context.RequestServices.GetRequiredService<RpcOptions>();
            if (context.Request.Path == "/" &&
                HttpMethods.IsPatch(context.Request.Method) &&
                context.Request.Headers.TryGetValue("Friendly-Name", out StringValues friendlyName))
            {                
                if (string.IsNullOrWhiteSpace(options.Token) || (context.Request.Headers.TryGetValue("Token", out StringValues token) && options.Token == token))
                {
                    if (RpcFramework.IsConnected(friendlyName))
                    {
                        context.Response.StatusCode = 208;
                    }
                    else
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            await context.Request.Body.CopyToAsync(memoryStream);
                            ReadOnlyCollection<byte> publicKeyBytes = memoryStream.ToArray().AsReadOnly();

                            if (SecureSocket.IterateTrustedKeys().Any(kv => kv.Value.SequenceEqual(publicKeyBytes)))
                            {
                                context.Response.StatusCode = 409;
                                return;
                            }

                            SecureSocket.AddTrustedKey(publicKeyBytes, friendlyName);
                        }
                        context.Response.StatusCode = 202;
                    }
                    if (context.Request.Headers.TryGetValue("Routes", out StringValues routes))
                    {
                        if (!context.Request.Headers.TryGetValue("BasePort", out StringValues basePort))
                        {
                            context.Response.StatusCode = 400;
                            return;
                        }

                        if (!context.Request.Headers.TryGetValue("BaseHost", out StringValues baseHost))
                        {
                            baseHost = context.Connection.RemoteIpAddress.ToString();
                            if (context.Connection.RemoteIpAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            {
                                baseHost = $"[{baseHost}]";
                            }
                        }

                        if (!context.Request.Headers.TryGetValue("BaseScheme", out StringValues baseScheme))
                        {
                            baseScheme = "http";
                        }

                        YarpConfigService configService = context.RequestServices.GetRequiredService<YarpConfigService>();
                        try
                        {
                            configService.UpdateConfig(friendlyName, new Uri($"{baseScheme}://{baseHost}:{basePort}"), routes);
                        }
                        catch
                        {
                            context.Response.StatusCode = 400;
                            return;
                        }
                    }
                }
                else
                {
                    context.Response.StatusCode = 401;
                    return;
                }
            }
            else
            {
                await next(context);
                return;
            }
            await context.Response.WriteAsync(options.ServerEndpoint.ToString());
        }

        #endregion Public Methods
    }
}