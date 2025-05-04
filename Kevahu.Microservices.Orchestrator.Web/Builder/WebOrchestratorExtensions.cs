using Kevahu.Microservices.Core.RemoteProcedureCall;
using Kevahu.Microservices.Orchestrator.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kevahu.Microservices.WebOrchestrator
{
    /// <summary>
    /// Provides extension methods for integrating the Orchestrator functionality with ASP.NET Core
    /// web applications.
    /// </summary>
    public static class WebOrchestratorExtensions
    {
        #region Public Methods

        /// <summary>
        /// Adds the necessary services for the Web Orchestrator, including MVC controllers and
        /// orchestrator core services.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>An <see cref="OrchestratorBuilder"/> instance for further configuration.</returns>
        public static OrchestratorBuilder AddWebOrchestrator(this IServiceCollection services)
        {
            return AddWebOrchestrator(services, null);
        }

        /// <summary>
        /// Adds the necessary services for the Web Orchestrator, including MVC controllers with
        /// custom configuration, orchestrator core services, and integrates loaded service
        /// assemblies with MVC. It also registers discovered RPC services into the dependency
        /// injection container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configure">An optional action to configure <see cref="MvcOptions"/>.</param>
        /// <returns>An <see cref="OrchestratorBuilder"/> instance for further configuration.</returns>
        public static OrchestratorBuilder AddWebOrchestrator(this IServiceCollection services, Action<MvcOptions>? configure)
        {
            IMvcBuilder mvcBuilder = services.AddControllers(configure);
            OrchestratorBuilder orchestratorBuilder = new OrchestratorBuilder();
            orchestratorBuilder.OnServicesLoaded += (sender, e) =>
            {
                foreach (Assembly assembly in e.ServicesLoadContext.Assemblies)
                {
                    mvcBuilder.AddApplicationPart(assembly);
                }
                foreach (Type serviceInterfaceType in RpcFramework.ServiceProvider)
                {
                    services.AddSingleton(serviceInterfaceType, RpcFramework.ServiceProvider.GetService(serviceInterfaceType));
                }
            };
            services.AddSingleton(orchestratorBuilder);
            return orchestratorBuilder;
        }

        /// <summary>
        /// Maps controller endpoints and registers a callback to finalize orchestrator
        /// configuration and connect to gateways after the application has started. It
        /// automatically discovers mapped routes and sets the base URI if not explicitly configured.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to map endpoints on.</param>
        /// <returns>
        /// A <see cref="ControllerActionEndpointConventionBuilder"/> for further endpoint configuration.
        /// </returns>
        public static ControllerActionEndpointConventionBuilder MapWebOrchestrator(this IEndpointRouteBuilder endpoints)
        {
            endpoints.ServiceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(async () =>
            {
                var orchestratorBuilder = endpoints.ServiceProvider.GetRequiredService<OrchestratorBuilder>();
                orchestratorBuilder.Options.Routes = endpoints.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
                    .OfType<RouteEndpoint>()
                    .Select(e => new RouteTemplate(e.RoutePattern).TemplateText)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Union(orchestratorBuilder.Options.Routes ?? [])
                    .Union(GetFileRoutes(endpoints.ServiceProvider))
                    .ToArray().AsReadOnly()!;

                IServer server = endpoints.ServiceProvider.GetRequiredService<IServer>();
                IFeatureCollection features = server.Features;
                Uri address = features.Get<IServerAddressesFeature>()?.Addresses
                    .Select(a => new Uri(a)).First();
                orchestratorBuilder.Options.BasePort = (ushort)address.Port;
                orchestratorBuilder.Options.BaseScheme = address.Scheme;

                ILogger<OrchestratorBuilder> logger = endpoints.ServiceProvider.GetRequiredService<ILogger<OrchestratorBuilder>>();
                logger.LogInformation("Orchestrator routes: {Routes}", string.Join(", ", orchestratorBuilder.Options.Routes));

                await orchestratorBuilder.Build().ConnectAllAsync();
            });
            return endpoints.MapControllers();
        }

        /// <summary>
        /// Explicitly sets the base URI for the services hosted by this orchestrator instance. This
        /// URI is used when registering with gateways if not automatically determined from server addresses.
        /// </summary>
        /// <param name="builder">The <see cref="OrchestratorBuilder"/> instance.</param>
        /// <param name="baseUri">
        /// The base URI (including scheme, host, and port) for the hosted services.
        /// </param>
        /// <returns>The <see cref="OrchestratorBuilder"/> instance for chaining.</returns>
        public static OrchestratorBuilder WithBase(this OrchestratorBuilder builder, Uri baseUri)
        {
            builder.Options.BaseHost = baseUri.Host;
            builder.Options.BasePort = (ushort)baseUri.Port;
            return builder;
        }

        /// <summary>
        /// Explicitly sets the routes that this orchestrator instance will serve. This is useful
        /// for customizing the functionality and behavior of the orchestrator.
        /// </summary>
        /// <param name="builder">The <see cref="OrchestratorBuilder"/> instance.</param>
        /// <param name="routes">An array of route strings to be served by the orchestrator.</param>
        /// <returns>The <see cref="OrchestratorBuilder"/> instance for chaining.</returns>
        public static OrchestratorBuilder WithRoutes(this OrchestratorBuilder builder, params string[] routes)
        {
            builder.Options.Routes = routes.AsReadOnly();
            return builder;
        }

        #endregion Public Methods

        #region Private Methods

        private static IEnumerable<string> GetFileRoutes(IServiceProvider serviceProvider, string subPath = "/")
        {
            IWebHostEnvironment hostingEnv = serviceProvider.GetRequiredService<IWebHostEnvironment>();
            IOptions<StaticFileOptions> staticFileOptions = serviceProvider.GetRequiredService<IOptions<StaticFileOptions>>();
            IFileProvider fileProvider = staticFileOptions.Value.FileProvider ?? hostingEnv.WebRootFileProvider;

            if (subPath == "/" && fileProvider.GetDirectoryContents(subPath).Exists)
            {
                yield return "/";
            }

            foreach (var result in fileProvider.GetDirectoryContents(subPath))
            {
                string relativePath = Path.Combine(subPath, result.Name).Replace('\\', '/');
                if (result.IsDirectory)
                {
                    foreach (var subResult in GetFileRoutes(serviceProvider, relativePath))
                    {
                        yield return subResult;
                    }
                }
                else
                {
                    yield return relativePath;
                }
            }
        }

        #endregion Private Methods
    }
}