# Kevahu's Microservices Core

This project forms the foundation of my microservices framework. It provides the essential building blocks, including a robust, high-performance Remote Procedure Call (RPC) framework, secure socket communication layer, and a distributed service provider (`RpcServiceProvider`) designed to facilitate communication between microservices, orchestrators, and the gateway. It can also be used as a standalone.

---

## Core Features

### Remote Procedure Call (RPC) Framework

The heart of the Core project is the RPC framework. Its primary function is to enable seamless method invocation across process boundaries, making it appear as though you are calling a local method even when the actual implementation resides on a different machine.

Here's how it works:

1.  **Discovery:** The framework scans loaded assemblies for interfaces marked with the `[RpcInterface]` attribute. These interfaces define the contracts for your microservices.
2.  **Proxy Generation:** If your application references an `[RpcInterface]` but does *not* include a corresponding implementation (a class marked with `[RpcImplementation]` that implements the interface), the RPC framework dynamically generates a proxy class at runtime.
3.  **Invocation Routing:** This generated proxy implements the interface. When you call a method (or access a property) on an instance of this proxy, the call is intercepted. The RPC framework serializes the method name and arguments and sends them over the network to another application that hosts the actual implementation.
4.  **Execution & Return:** The receiving application deserializes the request, invokes the real implementation method, serializes the return value (or any exception), and sends it back to the caller. The proxy then deserializes the response and returns it to your code.

The framework respects service lifetimes (`Singleton`, `Scoped`, `Transient`) defined in the `[RpcInterface]` attribute and manages instance tracking using scope IDs. However, **be mindful** that in a distributed environment with multiple instances of the same service potentially running in different applications, there's no guarantee of *instance affinity*. Requests might be load-balanced across different physical instances. For this reason, `Singleton` (the default) is strongly recommended for stateless services. See the "Service Lifetimes" section below for a more detailed discussion.

My RPC framework operates on a client-server model. Typically, clients initiate requests to servers. However, I've included an `allowMesh` option (enabled by default in this microservices setup) which permits bidirectional communication – servers can initiate RPC calls to clients within the mesh.

Furthermore, you can designate a server as a `root` server. This allows clients to send requests for a specific service interface to this `root` server, even if the client hasn't been explicitly informed by *that specific server* that it hosts the implementation. The `root` server acts as a central point that is assumed to know where to find the service (often delegating to the actual host). In my microservices framework, the Gateway acts as the `root` server.

#### Serialization with MessagePack

To ensure efficient communication, all RPC requests (method calls, parameters, return values) are serialized using MessagePack. MessagePack is a binary serialization format designed for speed and compactness, significantly outperforming traditional JSON in benchmarks. It offers near 10x the speed at roughly 1/3 the size. It also supports optional LZ4 compression, further reducing data size with minimal performance impact.

> **Important Serialization Constraints:**
>
> Because MessagePack serializes data into a binary format for network transmission, only data types that can be meaningfully represented as data across process boundaries are supported. Constructs tied to a specific process's memory or runtime state cannot be serialized.
>
> This means:
> *   **`Task` / `Task<T>`:** Returning a `Task` object itself is not useful, as the caller in a different process cannot `await` it or access its result directly. The RPC framework automatically handles awaiting tasks on the server-side and serializing only the *result* (or exception) back to the caller. Your interface methods should return the actual data type (`T`), not `Task<T>`.
> *   **Events / Delegates:** These rely on in-memory subscription lists and cannot be serialized.
> *   **`ref`, `out`, `in` parameters:** These keywords work with memory references, which are meaningless in a different process. Pass data by value.
> *   **Non-Serializable Objects:** Objects containing non-serializable members (like pointers, handles, or complex delegates) will cause serialization errors.
>
> **Recommendation:** Stick to Plain Old Data (POD) objects (classes or structs with properties), primitive types (int, string, bool, etc.), collections (arrays, `List<T>`, `Dictionary<TKey, TValue>`) of serializable types, and other types explicitly supported by MessagePack. Design your service interfaces with serializable data contracts in mind.
>
> For more details, refer to the [MessagePack-CSharp documentation](https://github.com/MessagePack-CSharp/MessagePack-CSharp).

### Secure Socket Communication

Transmitting data, potentially sensitive data, across a network requires robust security. While HTTPS is common, implementing it for pure RPC would introduce the overhead of ASP.NET Core and the HTTP protocol itself, which isn't strictly necessary for direct service-to-service communication.

Therefore, I opted for secure communication over plain TCP sockets. TCP provides a stateful, reliable connection, which is ideal for RPC. To add security, I implemented a mechanism inspired by TLS/SSL:

1.  **Handshake:** When an application connects to the gateway (or another application in mesh mode), they perform a handshake using asymmetric encryption (RSA public/private key pairs).
2.  **Key Exchange:** During the handshake, they securely exchange a symmetric encryption key (AES).
3.  **Symmetric Encryption:** All subsequent RPC communication on that connection is encrypted using this shared symmetric key.
4.  **Key Rolling:** For enhanced security, this symmetric key is automatically rolled (changed) on every request, minimizing the impact if a single key were ever compromised.

**Key Management:** This security model requires careful key management:
*   Each instance **must** have its own unique RSA public/private key pair.
*   Each instance needs the **public keys** of the servers it connects to.
*   Keys must adhere to specific formatting requirements. You can easily generate compatible keys using the provided utility: `RsaTokenGenerator.GenerateKeys()`.

### Distributed Service Provider

The RPC framework handles the low-level communication, but how do you actually *use* the services in your application code? I'm a proponent of the Dependency Injection (DI) pattern, so I created a custom service provider, `RpcServiceProvider`.

This provider integrates seamlessly with the RPC framework. When you request a service (an interface marked with `[RpcInterface]`), the `RpcServiceProvider` determines the appropriate implementation:
*   If a local implementation (marked with `[RpcImplementation]`) exists in the current process, it resolves to that instance, respecting the defined service lifetime.
*   If no local implementation exists, but the RPC framework knows (via the gateway or mesh communication) that the service is available remotely, it resolves to a dynamically generated proxy instance. Subsequent calls to this proxy trigger RPC requests.

You can access this service provider via the `RpcFramework` instance:

```csharp
IMyService myService = RpcFramework.ServiceProvider.GetRequiredService<IMyService>();

// Now you can call methods on myService, regardless of whether
// the implementation is local or remote.
var result = myService.DoSomething("data");
```

This abstraction allows your application code to remain unaware of the physical location of service implementations.

---

## Getting Started: Building Your Services

Here’s a step-by-step guide to defining and implementing services using the Core framework:

1.  ### Define Shared Contracts (Interfaces)

    It's crucial that all parts of your distributed application (clients or other applications hosting implementations) share the exact same definition of the service interfaces. The best practice is to place your `[RpcInterface]` definitions in a dedicated .NET `Class Library` project that can be referenced by all other projects in your solution.

    **Project Structure Example:**

    ```
    Solution/
     ├ Domain/                 # Class Library for shared contracts
     │  ├ Interfaces/
     │  │  ├ IMyFirstService.cs
     │  │  └ IMySecondService.cs
     │  └ DTOs/                 # Data Transfer Objects used in interfaces
     │     └ MyComplexData.cs
     │
     ├ MyFirstService.Impl/    # Class Library for implementation
     │  └ MyFirstService.cs
     │
     ├ MySecondService.Impl/   # Class Library for implementation
     │  └ MySecondService.cs
     │
     ├ Orchestrator/           # Host project (Console App or Web App)
     └ Gateway/                # Gateway project
    ```

    This separation ensures consistency and allows implementations to be deployed independently. You might also bundle related service implementations into a single library if they are always deployed together.

2.  ### Write Your Service Interfaces

    Define your service contracts using C# interfaces. Remember to mark each interface intended for RPC with the `[RpcInterface]` attribute.

    ```csharp
    // In Domain/Interfaces/IMyFirstService.cs
    using Kevahu.Microservices.Core.Attributes; // Namespace for RpcInterface
    using Microsoft.Extensions.DependencyInjection; // Namespace for ServiceLifetime

    namespace Domain.Interfaces
    {
        [RpcInterface] // Default lifetime is Singleton
        public interface IMyFirstService
        {
            // Properties work because getters/setters are methods under the hood
            int MySimpleProperty { get; set; }

            // Methods with primitive parameters and return types
            string ProcessData(int inputId, string configuration);

            // Remember: Return the actual data type, don't use Task<T> because Tasks aren't serializable.
            MyComplexData GetComplexData(Guid id);
        }
    }
    ```

    You can specify the service lifetime within the attribute, although `Singleton` is strongly recommended for microservices (see below). Ensure all parameter and return types are serializable according to the MessagePack constraints mentioned earlier.

    ```csharp
    // In Domain/Interfaces/IMySecondService.cs
    using Kevahu.Microservices.Core.Attributes;
    using Microsoft.Extensions.DependencyInjection;

    namespace Domain.Interfaces
    {
        // Explicitly setting lifetime (though Singleton is usually best)
        [RpcInterface(ServiceLifetime.Scoped)]
        public interface IMySecondService
        {
            bool ValidateRequest(MyComplexData data);
        }
    }
    ```

#### Understanding Service Lifetimes in a Distributed Context

Service lifetimes behave differently in a distributed RPC environment compared to a single monolithic application. It's crucial to understand the implications:

*   **`Singleton` (Default & Recommended):** Only one instance of the implementation class is created *per application* that hosts it. All incoming RPC requests to that application for that service interface will be handled by this single instance.
    *   **Pros:** Efficient, predictable (within a single host).
    *   **Cons:** Must be thread-safe if it holds state. State is not shared across different applications running the same service.
    *   **Recommendation:** **Strongly recommended for microservices.** Design your services to be stateless whenever possible. If state is required, manage it externally (e.g., in a database, cache).

*   **`Scoped`:** A new instance is created for each "scope." In the context of the `RpcServiceProvider`, a scope is typically tied to a specific proxy instance or a logical operation sequence identified by a scope ID.
    *   **Pros:** Allows instance-specific state within a limited context.
    *   **Cons:** **Highly problematic in load-balanced microservices.** Since requests can be routed to *any* available application hosting the implementation, subsequent calls using the same client-side proxy (and thus, the same scope ID) might hit *different* applications, each creating its *own* scoped instance. You lose instance affinity and state consistency.
    *   **Recommendation:** **Avoid `Scoped` unless you have a very specific scenario and fully understand the lack of instance affinity across applications.** It often leads to unexpected behavior.

*   **`Transient`:** A new instance of the implementation class is created *every time* an RPC method is called on the hosting application.
    *   **Pros:** Ensures complete isolation between calls.
    *   **Cons:** Can be inefficient due to frequent object creation. State is never preserved between calls, even if the same application handles them.
    *   **Recommendation:** Use only if absolute call isolation is required and the overhead is acceptable. Generally less useful than `Singleton` for typical microservice patterns.

**In summary: For building reliable and scalable microservices with this framework, design your services to be stateless and use the default `Singleton` lifetime.**

3.  ### Write Your Service Implementations

    Create concrete classes that implement your service interfaces. Mark each implementation class with the `[RpcImplementation]` attribute. Place these implementations in their respective class library projects (e.g., `MyFirstService.Impl`).

    ```csharp
    // In MyFirstService.Impl/MyFirstService.cs
    using Kevahu.Microservices.Core.Attributes;
    using Domain.Interfaces;
    using Domain.DTOs; // Assuming MyComplexData is here

    namespace MyFirstService.Impl
    {
        [RpcImplementation]
        public class MyFirstService : IMyFirstService
        {
            // Simple property implementation
            public int MySimpleProperty { get; set; }

            public string ProcessData(int inputId, string configuration)
            {
                Console.WriteLine($"Processing data for ID: {inputId} with config: {configuration}");
                // ... actual processing logic ...
                return $"Processed {inputId}";
            }

            public MyComplexData GetComplexData(Guid id)
            {
                Console.WriteLine($"Getting complex data for ID: {id}");
                // ... logic to retrieve or build data ...
                return new MyComplexData { /* ... properties ... */ };
            }
        }
    }
    ```

    **Dependency Injection in Implementations:** Your implementation classes can use constructor injection to receive dependencies on *other* RPC services or standard services registered in the application's local DI container. The `RpcServiceProvider` (or the integrated provider in the Web Orchestrator) will resolve these dependencies, potentially injecting proxies if the required service is remote.

    ```csharp
    // In MySecondService.Impl/MySecondService.cs
    using Kevahu.Microservices.Core.Attributes;
    using Domain.Interfaces;
    using Domain.DTOs;

    namespace MySecondService.Impl
    {
        [RpcImplementation]
        public class MySecondService : IMySecondService
        {
            private readonly IMyFirstService _firstService;
            private readonly ILogger<MySecondService> _logger; // Example: Injecting standard logger

            // Constructor injection
            public MySecondService(IMyFirstService firstService, ILogger<MySecondService> logger)
            {
                _firstService = firstService; // Might be a local instance or an RPC proxy
                _logger = logger;
            }

            public bool ValidateRequest(MyComplexData data)
            {
                _logger.LogInformation("Validating request...");

                // Call another microservice via the injected interface
                string processingResult = _firstService.ProcessData(data.Id, "validation-config");

                _logger.LogInformation($"First service processing result: {processingResult}");

                // ... actual validation logic ...
                return true; // Placeholder
            }
        }
    }
    ```

---

## Next Steps

With your service interfaces and implementations defined, you are ready to host them within other applications or continue to set up your microservices application:

-    [Gateway README](../Kevahu.Microservices.Gateway/README.md): Configure the central entry point and mesh coordinator.
-   [Orchestrator README](../Kevahu.Microservices.Orchestrator/README.md): Learn how to host your service implementation libraries (`.dll` files) in a standard orchestrator process.
-   [Web Orchestrator README](../Kevahu.Microservices.Orchestrator.Web/README.md): If your services need to interact with or host web components (APIs, MVC), use the specialized Web Orchestrator.

These guides provide details on configuring the necessary security keys, registering service assemblies, connecting orchestrators to the gateway, and running the complete microservices application.

---
