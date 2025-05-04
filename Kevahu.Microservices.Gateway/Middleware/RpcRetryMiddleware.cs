using Microsoft.AspNetCore.Http;

namespace Kevahu.Microservices.Gateway.Middleware.RemoteProcedureCall
{
    /// <summary>
    /// Middleware that automatically retries idempotent HTTP requests (GET, HEAD, OPTIONS) that
    /// result in a server error (5xx status code) up to 3 times. Requires request body buffering to
    /// be enabled.
    /// </summary>
    public class RpcRetryMiddleware : IMiddleware
    {
        #region Public Methods

        /// <summary>
        /// Processes the HTTP request, invoking the next middleware in the pipeline. If the request
        /// is idempotent (GET, HEAD, OPTIONS) and the response indicates a server error (5xx)
        /// without the response having started, it retries the request up to 2 additional times by
        /// rewinding the request body stream (if possible) and clearing the response.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <param name="next">The delegate representing the next middleware in the pipeline.</param>
        /// <returns>A <see cref="Task"/> representing the completion of the middleware execution.</returns>
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Method != HttpMethods.Get && context.Request.Method != HttpMethods.Head && context.Request.Method != HttpMethods.Options)
            {
                await next(context);
                return;
            }
            context.Request.EnableBuffering();
            for (int retry = 3; retry > 0; retry--)
            {
                await next(context);
                if (!context.Response.HasStarted && context.Response.StatusCode >= 500 && context.Response.StatusCode < 600 && context.Request.Body.CanSeek)
                {
                    if (retry > 1)
                    {
                        context.Response.Clear();
                        context.Request.Body.Position = 0;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        #endregion Public Methods
    }
}