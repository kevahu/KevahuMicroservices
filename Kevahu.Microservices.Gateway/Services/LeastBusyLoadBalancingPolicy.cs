using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Kevahu.Microservices.Gateway.Services
{
    /// <summary>
    /// Implements a load balancing policy for YARP that selects the destination with the lowest
    /// number of concurrent requests. If multiple destinations have the same lowest count, one is
    /// chosen randomly among them.
    /// </summary>
    public class LeastBusyLoadBalancingPolicy : ILoadBalancingPolicy
    {
        #region Properties

        /// <summary>
        /// Gets the unique name of this load balancing policy ("LeastBusy").
        /// </summary>
        public string Name => "LeastBusy";

        #endregion Properties

        #region Public Methods

        /// <summary>
        /// Selects a destination from the available destinations based on the lowest concurrent
        /// request count. Uses random selection as a tie-breaker.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the incoming request.</param>
        /// <param name="cluster">The state of the cluster.</param>
        /// <param name="availableDestinations">
        /// The list of currently available destinations within the cluster.
        /// </param>
        /// <returns>
        /// The selected <see cref="DestinationState"/>, or null if no destinations are available.
        /// </returns>
        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            return availableDestinations.OrderBy(d => d.ConcurrentRequestCount).ThenBy(d => Random.Shared.Next()).FirstOrDefault();
        }

        #endregion Public Methods
    }
}