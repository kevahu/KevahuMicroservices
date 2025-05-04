# Kevahu's Microservices Gateway

The Gateway is the central hub and primary entry point for my microservices framework. It serves two main purposes:

1.  **External Entry Point:** It acts as a reverse proxy, exposing HTTP(S) endpoints to external clients (like web browsers or mobile apps) and routing these requests to the appropriate backend microservices hosted within Orchestrators.
2.  **Internal RPC Hub:** It functions as the central connection point for Orchestrators using the secure RPC mechanism defined in [Kevahu.Microservices.Core](../Kevahu.Microservices.Core/README.md). It facilitates service discovery and enables communication *between* orchestrators.

---

## What is a Gateway?

Think of the Gateway like the main switchboard operator and receptionist for a large company.

*   **Receptionist (External):** When an external client sends an HTTP request (like visiting a webpage or calling an API), the Gateway receives it, figures out which internal "department" (Orchestrator hosting a specific service) needs to handle it, and forwards the request using YARP.
*   **Switchboard (Internal):** When an Orchestrator connects, it registers its "services" (RPC interfaces) with the Gateway. The Gateway maintains this directory. If one Orchestrator needs to call a method on a service hosted by another, it sends an RPC request to the Gateway, which then routes the call to the correct destination Orchestrator.

It efficiently directs traffic without needing to know the intricate details of *how* each service performs its work.

---

## Features

The Gateway leverages several components and custom logic:

*   **YARP Reverse Proxy:** I use YARP (Yet Another Reverse Proxy) for handling incoming HTTP(S) requests. It's highly performant and configurable for routing external traffic to internal services. See [Getting started with YARP](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/getting-started).
*   **Dynamic YARP Configuration:** Unlike static YARP setups, my Gateway dynamically updates its routing configuration (`YarpConfigService`). When an Orchestrator connects via RPC and registers its services (including any HTTP routes they expose), the Gateway automatically adds the necessary YARP routes. When an Orchestrator disconnects, its routes are removed.
*   **Custom Load Balancing:** While YARP offers various load balancing strategies, I've implemented a custom policy (`LeastBusyLoadBalancingPolicy`). When multiple Orchestrators host the same service or route, this policy prioritizes the instance with the fewest concurrent active requests, falling back to a random choice among equally loaded instances.
*   **Fault Tolerance (HTTP Retries):** YARP itself doesn't automatically retry failed requests. I've added custom middleware (`RpcRetryMiddleware` - note: the name reflects its origin, but it acts on HTTP requests proxied by YARP) that automatically retries failed requests *up to 3 times* for HTTP methods considered "safe" (GET, HEAD, OPTIONS). POST, PUT, DELETE requests are *not* retried automatically to prevent unintended side effects.
*   **RPC Server:** The Gateway hosts its own RPC server endpoint using the Core library's `SecureSocket` for Orchestrators to connect to.
*   **RPC Mesh Routing:** With `AllowMesh` enabled (which is essential for this framework), the Gateway acts as a central router for RPC calls between connected Orchestrators.
*   **Fluent Configuration API:** Setting up the Gateway is streamlined using extension methods for `IServiceCollection` and `IApplicationBuilder`.

---

## Configuration Options

You configure the Gateway using a fluent API when setting up your `WebApplication`.

### Core Setup (`IServiceCollection`)

*   `AddRemoteProcedureCall()`: Registers essential Gateway services (like `YarpConfigService`, middleware, load balancer) and the core `RpcOptions`. You can optionally pass an `Action<RpcOptions>` to configure options directly, but all settings are exposed via the fluent methods below.

### Security (`RpcBuilder`)

*   `WithMyKeys(FileInfo publicKey, FileInfo privateKey)`: **Required.** Sets the Gateway's own unique RSA public/private key pair used for establishing secure RPC connections with Orchestrators. If the specified files don't exist, new keys (8192-bit) will be generated and saved.
*   `SetToken(string token)`: **Highly Recommended.** Defines a pre-shared secret token. Orchestrators must provide this exact token when connecting via RPC to be authorized. This prevents unauthorized applications that might know the Gateway's public key from joining the mesh. Store this securely (e.g., user secrets, environment variables, Azure Key Vault).
*   `AddTrustedKey(FileInfo publicKey, string friendlyName)`: *Typically not needed for the Gateway.* This is used by clients (like Orchestrators) to trust the server's public key. The Gateway primarily verifies incoming connections using its keys and the token.

### Server & Mesh (`RpcBuilder`)

*   `SetServer(string host, ushort port)`: **Required.** Specifies the IP address and port the Gateway's *internal RPC server* will listen on for incoming connections from Orchestrators. Ensure this endpoint is reachable by your Orchestrator instances (e.g., within your Docker network or private cloud network). `0.0.0.0` or `::` usually means listen on all available network interfaces.
*   `AllowMesh()`: **Required for the microservices framework.** Enables the Gateway to route RPC calls *between* connected Orchestrators. Without this, Orchestrators could only call services hosted directly on the Gateway (which is usually none).

### Timeouts (`RpcBuilder`)

*   `WithTimeout(int timeout)`: Sets the default timeout (in milliseconds) for *outgoing* RPC requests initiated *by the Gateway itself* (when forwarding). Default is -1 (infinite).

### Middleware Registration (`IApplicationBuilder`)

*   `UseRemoteProcedureCall()`: **Required.** Adds the necessary middleware to the ASP.NET Core pipeline (like `RpcSignInMiddleware` for handling orchestrator connections/authentication and `RpcRetryMiddleware` for HTTP fault tolerance). It also starts the Gateway's RPC server and registers internal event handlers for logging and managing YARP configuration based on orchestrator connections/disconnections.

## Setting up your Gateway

1.  ### Create the Project

    Start with an `ASP.NET Core Empty` project template. Add references to the `Kevahu.Microservices.Gateway` NuGet package (or project reference) and the `Yarp.ReverseProxy` NuGet package.

2.  ### Configure in `Program.cs`

    The setup involves configuring services and the application pipeline. Here’s a typical example:

    ```csharp
    // filepath: <YourGatewayProject>/Program.cs
    using Kevahu.Microservices.Gateway.Builder; // For RpcBuilderExtensions
    using Yarp.ReverseProxy.Configuration; // For LoadFromMemory

    var builder = WebApplication.CreateBuilder(args);

    // Configure Kestrel endpoints
    // Example: Port 80 for external HTTP traffic, Port 5000 for orchestrators to register themselves.
    // The RPC port (e.g., 3456 below) is configured separately via SetServer.
    builder.WebHost.UseUrls("http://*:80", "http://*:5000");

    // --- Configure Gateway Services ---

    // 1. Add and configure RPC services for the Gateway
    builder.Services.AddRemoteProcedureCall()
        .AllowMesh() // ESSENTIAL: Enable routing between orchestrators
        .SetServer("0.0.0.0", 3456) // Define where Orchestrators connect TO (RPC Port)
        .SetToken("YourSuperSecretToken!ChangeMe!") // IMPORTANT: Secure this token!
        .WithMyKeys(
            new FileInfo("gateway_public.key"), // Path to Gateway's public key
            new FileInfo("gateway_private.key") // Path to Gateway's private key
        ); // Keys will be generated if they don't exist

    // 2. Add YARP services. LoadFromMemory with empty lists because config
    //    is managed dynamically by YarpConfigService via RPC events.
    builder.Services.AddReverseProxy()
        .LoadFromMemory(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());

    // --- Build the Application ---
    var app = builder.Build();

    // --- Configure Middleware Pipeline ---

    // 1. Enable RPC middleware (handles orchestrator connections, auth, logging)
    //    and starts the RPC server.
    app.UseRemoteProcedureCall();

    // 2. Enable YARP reverse proxy middleware.
    app.MapReverseProxy();

    // Optional: Add other middleware like HTTPS redirection, AuthN/AuthZ for external endpoints, etc.
    // app.UseHttpsRedirection();

    // --- Run the Gateway ---
    app.Run();
    ```

    Remember to replace placeholder values (like the token and key paths) and manage secrets securely.

---

## Next Steps

With the Gateway configured, you can now set up Orchestrators to host your services and connect them to the Gateway:

-   [Orchestrator README](../Kevahu.Microservices.Orchestrator/README.md): Learn how to host your service implementation libraries (`.dll` files) and connect them to the Gateway.
-   [Web Orchestrator README](../Kevahu.Microservices.Orchestrator.Web/README.md): Use this if your services need to host ASP.NET Core components (APIs, MVC).
-   [Core Library README](../Kevahu.Microservices.Core/README.md): Review details about RPC, security, and serialization if needed.

---
