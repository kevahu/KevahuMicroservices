using Kevahu.Microservices.Gateway.Builder;

namespace SampleGateway
{
    public class Program
    {
        #region Public Methods

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Listen on all interfaces for ports 80 (external/proxy) and 5000 (internal)
            builder.WebHost.UseUrls("http://*:80", "http://*:5000");

            builder.Services.AddRemoteProcedureCall()
                .AllowMesh()
                // Listen on all interfaces for port 3456 (RPC)
                .SetServer("0.0.0.0", 3456)
                .SetToken("Hayg2IqMTUKTxXPhGpSQaQH8gUCqbPP0WD1vSm7bcEwQ-K70CBp10kO-l-V8TtCr1w")
                .WithMyKeys(new FileInfo("./public.key"), new FileInfo("./private.key"));

            builder.Services.AddReverseProxy().LoadFromMemory([], []);

            var app = builder.Build();

            app.UseRemoteProcedureCall();
            app.MapReverseProxy();

            app.Run();
        }

        #endregion Public Methods
    }
}