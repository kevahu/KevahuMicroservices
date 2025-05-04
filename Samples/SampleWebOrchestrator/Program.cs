using Kevahu.Microservices.Orchestrator.Builder;
using Kevahu.Microservices.WebOrchestrator;
using System.Net;
using System.Net.Sockets;

namespace SampleWebOrchestrator
{
    public class Program
    {
        #region Public Methods

        public static void Main(string[] args)
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

            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseUrls($"http://{(gatewayIp == IPAddress.Loopback ? IPAddress.Loopback : IPAddress.Any)}:{Random.Shared.Next(5000, 6000)}");

            builder.Services.AddWebOrchestrator()
                .AddGateway("Gateway", 4, new FileInfo("./gateway.public.key"), new Uri($"http://{gatewayIp}:5000/"), "Hayg2IqMTUKTxXPhGpSQaQH8gUCqbPP0WD1vSm7bcEwQ-K70CBp10kO-l-V8TtCr1w")
                .WithMyKeys(new FileInfo("./public.key"), new FileInfo("./private.key"))
                .WithServices(new DirectoryInfo("./Services"));

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapWebOrchestrator();

            app.Run();
        }

        #endregion Public Methods
    }
}