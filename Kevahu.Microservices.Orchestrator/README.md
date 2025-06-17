# Kevahu's Microservices Orchestrator

The Orchestrator is a dedicated host process within my microservices framework. Its primary responsibility is to load, host, and manage the lifecycle of your microservice implementations (defined using [Kevahu.Microservices.Core](../Kevahu.Microservices.Core/README.md)). It connects to one or more Gateways, making the hosted services discoverable and accessible to the rest of the microservices mesh.

---

## What is an Orchestrator?

Continuing the company analogy, if the Gateway is the receptionist/switchboard, an Orchestrator is like a specialized department within the company.

When the Orchestrator starts up (the department opens), it loads its specific set of "employees" – the microservice implementation DLLs you provide. It then reports to the Gateway ("reception") which services it has available.

When a request comes in (forwarded by the Gateway), the Orchestrator directs it to the correct service implementation ("employee") to handle the work and sends the result back. It doesn't need to worry about external client communication; it focuses solely on executing the business logic defined in its hosted services.

This design allows for flexible scaling. You can run multiple Orchestrator instances, each potentially hosting different combinations of services. If one service experiences high demand, you can deploy more Orchestrator instances specifically configured to host that service, distributing the load effectively.

While running all services in a single Orchestrator might offer the lowest latency for inter-service calls *within* that instance, it creates a single point of failure. A balanced approach, splitting services across multiple Orchestrators based on domain, dependencies, and scaling needs, generally provides better resilience (fault tolerance) and availability.

---

## Features

The Orchestrator provides several key features for hosting your services:

*   **Dynamic Service Loading:** It discovers and loads microservice implementations from `.dll` files located in a specified directory. I achieve this using a custom `AssemblyLoadContext` (`ServicesLoadContext`). This isolates service assemblies and opens possibilities for future enhancements like hot-reloading services without restarting the Orchestrator.
*   **Debugging Support:** If you include the corresponding `.pdb` (debug symbols) files alongside your service `.dll` files in the services directory, Visual Studio can attach to the running Orchestrator process. This allows you to set breakpoints and debug your microservice code as if it were part of the Orchestrator project itself, greatly simplifying development and troubleshooting.
*   **Gateway Registration:** Upon loading services, the Orchestrator connects to the configured Gateway(s) using the secure RPC channel. It authenticates itself and registers the `[RpcImplementation]` types it hosts, making them discoverable.
*   **RPC Request Handling:** It listens for incoming RPC requests forwarded by the Gateway (or potentially other Orchestrators in a mesh scenario) and routes them to the appropriate local service implementation.
*   **Integrated Dependency Injection:** When resolving dependencies for your service implementations (via constructor injection), the Orchestrator uses the underlying `RpcServiceProvider`. It prioritizes resolving dependencies with *local* implementations hosted within the *same* Orchestrator instance for optimal performance. If a required service (`[RpcInterface]`) is not found locally, it seamlessly requests a proxy instance from the RPC framework, which will route calls to a remote Orchestrator via the Gateway.

---

## Configuration Options

You configure the Orchestrator using a fluent API builder (`OrchestratorBuilder`) typically in your `Program.cs` or application entry point.

*   `AddGateway(string friendlyName, byte connections, FileInfo publicKey, Uri signInUrl, string token)`: **Required (at least one).** Configures a Gateway connection.
    *   `friendlyName`: A unique name to identify this specific Gateway connection (e.g., "PrimaryGateway"). Used internally for managing connections and referencing the Gateway's public key.
    *   `connections`: The number of parallel RPC connections to establish *to this Gateway*. More connections can improve responsiveness under load but consume more resources. The optimal number depends on expected load, service complexity, and available CPU cores. Note: If `AllowMesh` is enabled on the Gateway side (which it usually is), the actual socket count might differ as connections can be bidirectional.
    *   `publicKey`: The `FileInfo` pointing to the *Gateway's* public key file (`.key`). This is crucial for the Orchestrator to verify the Gateway's identity during the secure handshake.
    *   `signInUrl`: The specific URL endpoint on the Gateway that the Orchestrator uses to initiate the connection and registration process (e.g., `http://gateway-hostname:5000/`). This endpoint is handled by the Gateway's `RpcSignInMiddleware`.
    *   `token`: The pre-shared secret token that must match the token configured on the Gateway (`SetToken`). Used for initial authentication. **Store this securely!**
*   `WithFriendlyName(string friendlyName)`: **Optional.** Sets a custom friendly name for *this Orchestrator instance*. This name appears in Gateway logs and helps identify the instance. If not set, it defaults to `Orchestrator-{MachineName}-{ProcessId}`, which is usually unique enough for identification. Ensure uniqueness if running multiple orchestrators on the same machine manually.
*   `WithMyKeys(FileInfo publicKey, FileInfo privateKey)`: **Required.** Sets the Orchestrator's *own* unique RSA public/private key pair. These are used to authenticate the Orchestrator to the Gateway during the secure handshake. Similar to the Gateway, keys will be generated (8192-bit) and saved to the specified paths if the files don't exist.
*   `WithServices(DirectoryInfo servicesPath)`: **Required.** Specifies the directory where the Orchestrator should look for microservice implementation `.dll` files.
    *   **Security Warning:** Ensure this directory path is secure and only contains trusted assemblies. Loading untrusted DLLs from this path is a significant security risk, potentially compromising the entire microservices network.
    *   This method immediately triggers the loading process using `ServicesLoadContext`, scans the assemblies for `[RpcImplementation]` classes, and registers them with the internal `RpcFramework`.

---

## Setting up your Orchestrator

1.  ### Create the Project

    Start with a standard .NET `Console App` project template. Add a reference to the `Kevahu.Microservices.Orchestrator` NuGet package (or the project itself if you're building from source).

2.  ### Configure in `Program.cs`

    Use the `OrchestratorBuilder` to configure and initialize the Orchestrator.

    ```csharp
    // filepath: <YourOrchestratorProject>/Program.cs
    using Kevahu.Microservices.Orchestrator.Builder; // Required for the builder extensions

    Console.Title = "My Service Orchestrator"; // Optional: Set a console title

    // 1. Create and configure the Orchestrator Builder
    OrchestratorInitiator initiator = new OrchestratorBuilder()
        // Define connection to the Gateway
        .AddGateway(
            friendlyName: "PrimaryGateway", // Identifier for this gateway connection
            connections: 4,                 // Number of connections to establish
            publicKey: new FileInfo("gateway_public.key"), // Gateway's public key
            signInUrl: new Uri($"http://gateway.example.com:5000/"), // Gateway sign-in URL (use correct hostname/IP)
            token: "YourSuperSecretToken!ChangeMe!" // Must match Gateway's token
        )
        // Set the Orchestrator's own identity keys
        .WithMyKeys(
            publicKey: new FileInfo("orchestrator_public.key"),
            privateKey: new FileInfo("orchestrator_private.key")
        )
        // Optional: Set a custom name for this orchestrator instance
        // .WithFriendlyName("StockServiceOrchestrator-Instance1")
        // Specify the directory containing service DLLs
        .WithServices(new DirectoryInfo("Services")) // Relative path from executable
        // Build the initiator object (doesn't connect yet)
        .Build();

    Console.WriteLine("Orchestrator configured. Connecting to Gateway...");

    // 2. Initiate connections to all configured Gateways
    // This blocks until the initial connection and registration attempt completes.
    // Consider using the async version in a real async Main method.
    initiator.ConnectAllAsync().Wait();

    Console.WriteLine("Connected to Gateway. Orchestrator running. Press Enter to exit.");
    
    // The RPC framework keeps the application from shutting down.
    ```

3.  ### Prepare and Deploy Microservices

    *   **Build:** Build your microservice implementation projects (the class libraries containing your `[RpcImplementation]` classes).
    *   **Copy:** Copy the resulting `.dll` files (e.g., `MyFirstService.Impl.dll`, `MySecondService.Impl.dll`) into the `Services` directory relative to your Orchestrator executable (or the absolute path you configured in `WithServices`).
    *   **(Optional but Recommended for Debugging):** Also copy the corresponding `.pdb` files into the same `Services` directory. This enables source-level debugging in Visual Studio when attaching to the Orchestrator process.

---

## Next Steps

Your Orchestrator is now ready to host backend services. Depending on your application's needs, you might:

*   Deploy multiple instances of this Orchestrator for scalability or high availability.
*   Create different Orchestrators hosting distinct sets of services.
*   Integrate with a web front-end or API layer, using the Web Orchestrator.

Refer to these related READMEs for more context:

-   [Core Library README](../Kevahu.Microservices.Core/README.md): Details on RPC, security, serialization, and defining services (`[RpcInterface]`, `[RpcImplementation]`).
-   [Gateway README](../Kevahu.Microservices.Gateway/README.md): Understanding the HTTP entry point, mesh coordination, and Gateway configuration.
-   [Web Orchestrator README](../Kevahu.Microservices.Orchestrator.Web/README.md): Use this specialized orchestrator if your services need to host or interact directly with ASP.NET Core web features (APIs, MVC controllers, static files).

---
