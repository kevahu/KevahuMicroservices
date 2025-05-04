using Kevahu.Microservices.Core.RemoteProcedureCall;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

namespace Kevahu.Microservices.Gateway.Services
{
    /// <summary>
    /// Service responsible for dynamically updating the YARP (Yet Another Reverse Proxy)
    /// configuration based on connected RPC nodes. It allows adding, updating, and removing routes
    /// and cluster destinations associated with a specific RPC node identified by its friendly name.
    /// </summary>
    public class YarpConfigService
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="YarpConfigService"/> class.
        /// </summary>
        /// <param name="configProvider">
        /// The <see cref="InMemoryConfigProvider"/> used by YARP to manage its configuration.
        /// </param>
        public YarpConfigService(InMemoryConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        #endregion Public Constructors

        #region Fields

        private static readonly SemaphoreSlim _ConfigSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// The in-memory configuration provider for YARP.
        /// </summary>
        private readonly InMemoryConfigProvider _configProvider;

        #endregion Fields

        #region Public Methods

        /// <summary>
        /// Removes all destinations associated with the specified friendly name from all YARP
        /// clusters. If removing a destination leaves a cluster empty, the corresponding route is
        /// also removed.
        /// </summary>
        /// <param name="friendlyName">
        /// The friendly name of the RPC node whose destinations should be removed.
        /// </param>
        public void RemoveConfig(string? friendlyName)
        {
            _ConfigSemaphore.Wait();
            try
            {
                var currentConfig = _configProvider.GetConfig();

                HashSet<string> friendlyNamesToRemove = currentConfig.Clusters.SelectMany(c => c.Destinations).Select(d => d.Key).Where(f => !RpcFramework.Connections.Contains(f)).ToHashSet();
                if (friendlyName != null)
                {
                    friendlyNamesToRemove.Add(friendlyName);
                }

                List<ClusterConfig> clustersToKeep = [];
                List<RouteConfig> routesToKeep = [];

                foreach (var cluster in currentConfig.Clusters)
                {
                    string route = cluster.ClusterId;
                    foreach (var destination in cluster.Destinations)
                    {
                        if (friendlyNamesToRemove.Contains(destination.Key))
                        {
                            ((Dictionary<string, DestinationConfig>)cluster.Destinations).Remove(destination.Key);
                        }
                    }
                    if (cluster.Destinations.Count > 0)
                    {
                        clustersToKeep.Add(cluster);
                        routesToKeep.Add(currentConfig.Routes.FirstOrDefault(r => r.RouteId == route));
                    }
                }

                _configProvider.Update(routesToKeep, clustersToKeep);
            }
            finally
            {
                _ConfigSemaphore.Release();
            }
        }

        /// <summary>
        /// Updates the YARP configuration for a specific RPC node. Adds or updates routes and
        /// cluster destinations based on the provided routes header and host URI. It ensures routes
        /// are correctly formatted, creates new cluster configurations with health checks and load
        /// balancing, and merges these with the existing configuration, removing the node from
        /// clusters it no longer serves.
        /// </summary>
        /// <param name="friendlyName">The friendly name of the RPC node.</param>
        /// <param name="host">
        /// The base URI (including scheme, host, and port) of the RPC node's service endpoints.
        /// </param>
        /// <param name="routesHeader">
        /// A space-separated string of route paths (e.g., "/serviceA /serviceB/api") served by this node.
        /// </param>
        public void UpdateConfig(string friendlyName, Uri host, string routesHeader)
        {
            _ConfigSemaphore.Wait();
            try
            {
                HashSet<string> routes = [.. routesHeader.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(r =>
            {
                if (!r.StartsWith('/'))
                {
                    return '/' + r;
                }
                if (r != "/" && r.EndsWith('/'))
                {
                    return r.Substring(0, r.Length - 1);
                }
                return r;
            })];
                List<RouteConfig> newRoutes = [];
                Dictionary<string, ClusterConfig> newClusters = [];
                foreach (string route in routes)
                {
                    newClusters.Add(route, new ClusterConfig()
                    {
                        ClusterId = route,
                        Destinations = new Dictionary<string, DestinationConfig>()
                    {
                        {friendlyName, new DestinationConfig() {Address = host.ToString()}}
                    },
                        LoadBalancingPolicy = "LeastBusy",
                        HealthCheck = new HealthCheckConfig()
                        {
                            Passive = new PassiveHealthCheckConfig()
                            {
                                Enabled = true,
                                Policy = HealthCheckConstants.PassivePolicy.TransportFailureRate,
                                ReactivationPeriod = TimeSpan.FromSeconds(10)
                            }
                        },
                        HttpClient = new HttpClientConfig()
                        {
                            EnableMultipleHttp2Connections = true,
                        }
                    });
                    newRoutes.Add(new RouteConfig()
                    {
                        ClusterId = route,
                        RouteId = route,
                        Match = new RouteMatch()
                        {
                            Path = route
                        }
                    });
                }
                var currentConfig = _configProvider.GetConfig();
                foreach (ClusterConfig clusterConfig in currentConfig.Clusters)
                {
                    if (!routes.Contains(clusterConfig.ClusterId))
                    {
                        ((Dictionary<string, DestinationConfig>)clusterConfig.Destinations).Remove(friendlyName);
                        if (clusterConfig.Destinations.Count > 0)
                        {
                            newClusters.Add(clusterConfig.ClusterId, clusterConfig);
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, DestinationConfig> destination in clusterConfig.Destinations)
                        {
                            if (destination.Key != friendlyName)
                            {
                                ((Dictionary<string, DestinationConfig>)newClusters[clusterConfig.ClusterId].Destinations).Add(destination.Key, destination.Value);
                            }
                        }
                    }
                }
                foreach (RouteConfig routeConfig in currentConfig.Routes)
                {
                    if (!routes.Contains(routeConfig.RouteId) && newClusters.ContainsKey(routeConfig.ClusterId))
                    {
                        newRoutes.Add(routeConfig);
                    }
                }
                _configProvider.Update(newRoutes, [.. newClusters.Values]);
            }
            finally
            {
                _ConfigSemaphore.Release();
            }
        }

        #endregion Public Methods
    }
}