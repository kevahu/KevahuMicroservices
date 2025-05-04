using Kevahu.Microservices.Orchestrator;
using Kevahu.Microservices.Orchestrator.Builder;
using System.Net;
using System.Net.Sockets;

namespace SampleOrchestrator
{
    internal class Program
    {
        #region Private Methods

        private static void Main(string[] args)
        {
            IPAddress gatewayIp = null;
            try
            {
                gatewayIp = Dns.GetHostEntry("gateway").AddressList.FirstOrDefault();
            }
            catch (SocketException)
            {
                gatewayIp = IPAddress.Loopback;
            }

            OrchestratorInitiator initiator = new OrchestratorBuilder()
                .AddGateway("Gateway", 4, new FileInfo("./gateway.public.key"), new Uri($"http://{gatewayIp}:5000/"), "Hayg2IqMTUKTxXPhGpSQaQH8gUCqbPP0WD1vSm7bcEwQ-K70CBp10kO-l-V8TtCr1w")
                .WithMyKeys(new FileInfo("./public.key"), new FileInfo("./private.key"))
                .WithServices(new DirectoryInfo("./Services"))
                .Build();

            initiator.ConnectAllAsync().Wait();
        }

        #endregion Private Methods
    }
}