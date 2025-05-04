# Kevahu's Web Orchestrator

The Web Orchestrator extends the standard [Orchestrator](../Kevahu.Microservices.Orchestrator/README.md) by integrating seamlessly with ASP.NET Core. This allows you to host web-specific components like MVC Controllers, Minimal APIs, Blazor applications, or serve static files, while still participating fully in the microservices mesh.

---

## Features

The Web Orchestrator inherits all the capabilities of the standard Orchestrator:

*   Loading and hosting backend microservice implementations (`[RpcImplementation]`) from DLLs.
*   Connecting to and registering services with the Gateway.
*   Participating in the RPC mesh for inter-service communication.
*   Utilizing the `RpcServiceProvider` for resolving service dependencies.

In addition, it adds specific features for web integration:

*   **ASP.NET Core Hosting:** It runs as a standard ASP.NET Core application, allowing you to use the familiar `WebApplicationBuilder` and configure middleware, services, and endpoints as usual.
*   **Web Service Loading:** You can package ASP.NET Core components (like Controllers) into separate class libraries (`.dll` files) and load them dynamically via the `WithServices` configuration, just like standard microservices.
*   **Unified Dependency Injection:** I've integrated ASP.NET Core's built-in DI container with my `RpcServiceProvider`. When you inject an `[RpcInterface]` into your Controllers, Razor Pages, or other services, the container will resolve it correctly – either providing a local instance if available within the Web Orchestrator or an RPC proxy instance to communicate with a remote service.
*   **Automatic Route Registration:** *After* the `WebApplication` starts, the Web Orchestrator automatically discovers:
    *   Endpoints mapped using standard methods (e.g., `MapControllers`, `MapGet`, `MapRazorPages`).
    *   Static files served via `UseStaticFiles` (it attempts to enumerate files in the configured `WebRootPath`).
    It then registers these discovered HTTP routes/paths with the Gateway, allowing the Gateway's reverse proxy (YARP) to route external requests correctly to this Web Orchestrator instance.
    *   **Note:** Routes defined *only* within custom middleware might not be automatically discovered. You can register these manually using `WithRoutes`.

**Recommendation:** Use the Web Orchestrator when you need to expose web endpoints (APIs, UI) or leverage ASP.NET Core features directly within a service host. If an orchestrator only hosts backend, non-web-facing services, the standard [Orchestrator](../Kevahu.Microservices.Orchestrator/README.md) is more lightweight due to the absence of the ASP.NET Core overhead.

---

## Configuration Options

Configuration involves setting up both ASP.NET Core services/pipeline and the Orchestrator components.

### Core Setup (`IServiceCollection`)

*   `AddWebOrchestrator()`: Registers necessary services for the Web Orchestrator, including MVC Controller support. It returns an `OrchestratorBuilder` instance, allowing you to chain standard orchestrator configurations. Internally, it calls `AddControllers()` and sets up hooks to integrate loaded service assemblies (`.dll` from `WithServices`) as MVC Application Parts and register discovered RPC services into the ASP.NET Core DI container.
*   `AddWebOrchestrator(Action<MvcOptions>? configure)`: Same as above, but allows you to provide custom configuration for MVC services via the `MvcOptions` action.
*   **Standard Orchestrator Options:** After calling `AddWebOrchestrator()`, you chain the standard `OrchestratorBuilder` methods:
    *   `AddGateway(...)`: **Required.** Configures connection(s) to the Gateway.
    *   `WithMyKeys(...)`: **Required.** Sets the Web Orchestrator's unique identity keys.
    *   `WithServices(...)`: **Required.** Specifies the directory to load service DLLs (including those containing Controllers or other web components).
    *   `WithFriendlyName(...)`: Optional. Sets a custom name for this instance.
    *   See [Orchestrator Configuration Options](../Kevahu.Microservices.Orchestrator/README.md#configuration-options) for details on these.

### Web Specific (`OrchestratorBuilder`)

These methods are specific extensions for the `OrchestratorBuilder` when used in a Web Orchestrator context:

*   `WithBase(Uri baseUri)`: **Optional.** Explicitly sets the base URI (scheme, host, port) that this Web Orchestrator instance serves. If not set, the orchestrator attempts to automatically determine this from the `IServerAddressesFeature` after the application starts. This base URI information might be used during registration with the Gateway.
*   `WithRoutes(params string[] routes)`: **Optional.** Explicitly defines the list of HTTP route templates or static file paths served by this instance. This supplements the automatic route discovery. Useful for routes defined in middleware or for fine-grained control over what gets registered with the Gateway.

### Application Pipeline (`IEndpointRouteBuilder` / `IApplicationBuilder`)

*   `MapWebOrchestrator()`: **Required.** This extension method for `IEndpointRouteBuilder` does two crucial things:
    1.  It calls `MapControllers()` to enable routing for dynamically loaded MVC controllers.
    2.  It registers a callback (`IHostApplicationLifetime.ApplicationStarted`) that executes *after* the web host has fully started. This callback:
        *   Retrieves the configured `OrchestratorBuilder`.
        *   Performs automatic discovery of mapped `RouteEndpoint` instances and static files.
        *   Merges discovered routes with any explicitly set via `WithRoutes`.
        *   Determines the base URI if not set via `WithBase`.
        *   Logs the final list of routes being managed.
        *   Finally, calls `Build().ConnectAllAsync()` on the `OrchestratorBuilder` to initiate the connection and registration process with the configured Gateway(s).
*   **Standard ASP.NET Core Middleware:** You configure other middleware (`UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, etc.) as you normally would in an ASP.NET Core application *before* calling `MapWebOrchestrator()`.

---

## Setting up your Web Orchestrator

1.  ### Create your Web Microservice Project(s)

    *   Use the standard .NET `Class Library` project template for services that contain web components (like Controllers).
    *   Add the necessary ASP.NET Core framework reference to your class library's `.csproj` file:
        ```xml
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework> <!-- Or your target framework -->
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

          <!-- Add this FrameworkReference -->
          <ItemGroup>
            <FrameworkReference Include="Microsoft.AspNetCore.App" />
          </ItemGroup>

          <!-- Add references to Domain/Core projects as needed -->
          <ItemGroup>
            <ProjectReference Include="..\..\Kevahu.Microservices.Core\Kevahu.Microservices.Core.csproj" />
            <ProjectReference Include="..\..\Domain\Domain.csproj" />
          </ItemGroup>
        </Project>
        ```

2.  ### Write Web Microservices (e.g., Controllers)

    Create your ASP.NET Core Controllers, Minimal APIs, etc., within these class libraries just as you normally would. You can use standard attributes like `[Route]`, `[ApiController]`, `[HttpGet]`, etc. Leverage the unified DI to inject both standard ASP.NET Core services and your custom `[RpcInterface]` services.

    ```csharp
    // Example Controller in a separate Class Library project
    // filepath: <YourWebServiceLibrary>/Controllers/MyWebApiController.cs
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Domain.Interfaces; // Assuming IMyBackendService is defined here
    using Domain.DTOs;      // Assuming MyComplexData is defined here

    namespace MyWebServiceLibrary.Controllers
    {
        [Route("api/[controller]")]
        [ApiController]
        public class MyWebApiController : ControllerBase
        {
            private readonly ILogger<MyWebApiController> _logger;
            private readonly IMyBackendService _backendService; // Injected RPC service

            // Constructor Injection works for both ASP.NET Core and RPC services
            public MyWebApiController(ILogger<MyWebApiController> logger, IMyBackendService backendService)
            {
                _logger = logger;
                _backendService = backendService; // This could be a local impl or an RPC proxy
            }

            [HttpGet("process/{id}")]
            public async Task<IActionResult> ProcessData(int id, [FromQuery] string config)
            {
                _logger.LogInformation("Processing request for ID {Id} via web API", id);

                // Call the backend microservice
                string result = await _backendService.PerformOperationAsync(id, config);

                return Ok(new { Message = "Processed successfully", BackendResult = result });
            }

            [HttpPost("validate")]
            public IActionResult ValidateComplexData([FromBody] MyComplexData data)
            {
                 _logger.LogInformation("Validating complex data via web API");
                 bool isValid = _backendService.ValidateData(data); // Another call
                 if (!isValid)
                 {
                     return BadRequest("Validation failed by backend service.");
                 }
                 return Ok("Data is valid.");
            }
        }
    }
    ```

3.  ### Configure the Web Orchestrator Host

    Create an `ASP.NET Core Empty` project (or another suitable template like Web API) to act as the host process. Configure it in `Program.cs`:

    ```csharp
    // filepath: <YourWebOrchestratorHostProject>/Program.cs
    using Kevahu.Microservices.WebOrchestrator; // For WebOrchestrator extensions

    var builder = WebApplication.CreateBuilder(args);

    // --- Configure Services ---

    // 1. Add Web Orchestrator services and get the builder
    var orchestratorBuilder = builder.Services.AddWebOrchestrator()
        // Chain standard orchestrator configurations
        .AddGateway(
            friendlyName: "PrimaryGateway",
            connections: 4,
            publicKey: new FileInfo("gateway_public.key"), // Gateway's public key
            signInUrl: new Uri($"http://gateway.example.com:5000/"), // Gateway sign-in URL
            token: "YourSuperSecretToken!ChangeMe!" // Gateway token
        )
        .WithMyKeys(
            publicKey: new FileInfo("web_orchestrator_public.key"), // This host's public key
            privateKey: new FileInfo("web_orchestrator_private.key") // This host's private key
        )
        .WithServices(new DirectoryInfo("Services")); // Directory for ALL service DLLs (backend + web)
        // Optional: Explicitly define routes if needed
        // .WithRoutes("/api/custom", "/health")
        // Optional: Explicitly define base URI if needed
        // .WithBase(new Uri("http://my-service.internal:8080"))

    // 2. Add other standard ASP.NET Core services if needed
    // builder.Services.AddAuthentication(...);
    // builder.Services.AddAuthorization(...);
    // builder.Services.AddSwaggerGen(...);

    // --- Build the App ---
    var app = builder.Build();

    // --- Configure HTTP Pipeline ---

    // Optional: Configure standard middleware like Swagger, HTTPS redirection, etc.
    // if (app.Environment.IsDevelopment())
    // {
    //     app.UseSwagger();
    //     app.UseSwaggerUI();
    // }
    // app.UseHttpsRedirection();

    // Enable serving static files (e.g., for SPA frontends or images)
    // Place static files in wwwroot by default
    app.UseDefaultFiles(); // Serves index.html for root requests
    app.UseStaticFiles();

    // Optional: Add routing, authentication, authorization middleware if used
    // app.UseRouting();
    // app.UseAuthentication();
    // app.UseAuthorization();

    // 1. IMPORTANT: Map Web Orchestrator endpoints. This enables controllers
    //    and triggers the connection to the Gateway after startup.
    app.MapWebOrchestrator();

    // Optional: Map other minimal APIs or endpoints if not using controllers
    // app.MapGet("/minimal-api/ping", () => "pong");

    // --- Run the Application ---
    app.Run();
    ```

4.  ### Prepare and Deploy Microservices

    *   **Build:** Build *all* your microservice projects – both the backend ones (`[RpcImplementation]`) and the web ones (containing Controllers, etc.).
    *   **Copy:** Copy *all* resulting `.dll` files (and `.pdb` for debugging) into the `Services` directory relative to your Web Orchestrator host executable (or the absolute path configured in `WithServices`).

---

## Next Steps

You now have a powerful host capable of serving web requests while fully participating in the microservices mesh. You can deploy this like any other ASP.NET Core application, often within containers (Docker, Kubernetes) for scalability and management in production environments.

Consider exploring containerization strategies to take full advantage of the microservices architecture.

---

## Learn More

-   [Core Library README](../Kevahu.Microservices.Core/README.md): RPC, security, serialization, and defining services.
-   [Orchestrator README](../Kevahu.Microservices.Orchestrator/README.md): Details on the standard orchestrator and service loading.
-   [Gateway README](../Kevahu.Microservices.Gateway/README.md): Understanding the entry point, reverse proxying, and mesh coordination.
-   Microsoft's Guide: [.NET Microservices: Architecture for Containerized .NET Applications](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/)

---
