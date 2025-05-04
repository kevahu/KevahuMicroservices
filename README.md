# Kevahu's Microservices Framework

Welcome to Kevahu's Microservices Framework, a project developed by Kellian Van Hullebusch as part of my Bachelor's thesis in Applied Informatics at Odisee Brussel (2024-2025).

My goal with this framework is to provide a comprehensive solution for building robust and scalable applications using the microservices architectural style in C#/.NET.

## Understanding Microservices

*Summarized from **.NET Microservices: Architecture for Containerized .NET Applications** (2023)*

Microservices architecture presents an important approach for distributed mission-critical applications, building applications as a collection of services that can be developed, tested, deployed, and versioned independently. This contrasts with the traditional monolithic approach, where the application is scaled by cloning the entire application across several servers or virtual machines.

One of the primary improvements offered by microservices is **long-term agility and better maintainability** in complex, large, and highly-scalable systems. By enabling the creation of applications based on many independently deployable services, each with granular and autonomous lifecycles, microservices allow for agile changes and rapid iteration on specific, smaller areas of a complex application. This means that you can change the internal implementation of any microservice or add new functionality without breaking other microservices, as long as the interfaces or contracts remain unchanged. This level of independent evolution and deployment provides agility because new versions of microservices can be deployed frequently. In contrast, updating a monolithic application might require redeploying the entire system.

Furthermore, microservices significantly improve **scalability and resource efficiency**. Instead of scaling the entire monolithic application as a single unit, you can scale out specific microservices independently. This is particularly beneficial when only certain functional areas require more processing power or network bandwidth to handle demand, leading to cost savings as you need less hardware overall compared to scaling out a monolithic design where all code for different tasks is deployed multiple times and scaled at the same grade.

From an organizational perspective, microservices facilitate the **division of development work among multiple teams**. Each service can be owned by a single team, allowing them to manage, develop, deploy, and scale their service autonomously. This autonomy is enhanced by the principle of data sovereignty, where each microservice owns its domain data and logic, which is private to the microservice and accessed only through its API or messaging.

Finally, microservices offer **improved issue isolation**. If an issue occurs in one service, only that service is initially impacted (provided there are no direct synchronous dependencies used incorrectly), allowing other services to continue handling requests. This is a key advantage over a monolithic deployment architecture where a single malfunctioning component can potentially bring down the entire system. When an issue is resolved, only the affected microservice needs to be deployed. The smaller size of each microservice also makes it easier for developers to understand and get started quickly, enhancing productivity.

Common design patterns include direct client-to-microservice communication and the API gateway pattern. The API gateway acts as a single entry point, simplifying client interaction and handling cross-cutting concerns like authentication and routing.

## My Microservices Implementation

For my implementation, I chose the API gateway pattern. I aimed to create a framework that simplifies building microservices in C#/.NET. While existing solutions often treat microservices as separate applications communicating via protocols like REST over HTTP, I wanted to abstract this further. My approach defines microservices primarily as interfaces and classes, removing the developer's burden of managing hosting and inter-service communication boilerplate. This allows developers to focus more directly on the business logic of their services.

The framework implements these key features:

### API Gateway

A central entry point (`Kevahu.Microservices.Gateway`) handles incoming HTTP requests. It leverages YARP for efficient reverse proxying, routing requests to the appropriate backend services.

### Service Discovery

When orchestrators connect to the gateway, they register their available services and associated routes. This information is shared across the network, creating a mesh where every orchestrator can discover and access all registered services.

### Remote Procedure Call

The core communication layer (`Kevahu.Microservices.Core`) enables services to invoke methods on other services seamlessly, even if the implementation resides in a different process or orchestrator. My RPC mechanism differs from alternatives like gRPC by:

*   **Simplified Configuration:** Using attributes (`[RpcInterface]`, `[RpcImplementation]`) to define service contracts and implementations, reducing setup code.
*   **Performance Focus:** Employing Intermediate Language (IL) generation for dynamic proxy creation and MessagePack for efficient binary serialization.
*   **Integrated Dependency Injection:** Featuring a built-in `RpcServiceProvider` that supports dependency injection across process boundaries.

### Load Balancing

Load balancing is implemented within both the Gateway (for incoming HTTP traffic via YARP) and the RPC framework (for inter-service calls), distributing requests across available orchestrators.

### Fault Tolerance and High Availability

*   **Fault Tolerance:** Both the Gateway and the RPC framework incorporate mechanisms to detect failing orchestrators and automatically reroute requests to healthy instances. Details can be found in the `Kevahu.Microservices.Core` and `Kevahu.Microservices.Gateway` projects.
*   **High Availability:** When multiple instances of a service are running, the system aims to provide uninterrupted service to users, even if an orchestrator instance fails mid-request.

## Project Structure

The framework is organized into several key projects:

### Kevahu.Microservices.Core

This is the foundation, providing the RPC framework, secure socket communication and the distributed `RpcServiceProvider` for dependency injection.

### Kevahu.Microservices.Gateway

Acts as the API gateway and mesh coordinator. It uses YARP to proxy external HTTP requests and the Core RPC framework to communicate with orchestrators. It manages service discovery and routing within the mesh.

### Kevahu.Microservices.Orchestrator

A host process for running backend microservices. It connects to the gateway, registers its hosted services, and handles incoming RPC requests. Multiple orchestrators can run concurrently for scalability and fault tolerance.

### Kevahu.Microservices.Orchestrator.Web

Extends the standard orchestrator with capabilities to host ASP.NET Core web applications (MVC controllers, APIs, static files). It combines the service providers to allow seamless microservices integration.

## Supporting Libraries

*   **MessagePack:** Used for high-performance binary serialization within the RPC layer.
*   **YARP (Yet Another Reverse Proxy):** Powers the HTTP reverse proxy functionality in the Gateway component.

## Further Reading

For more detailed information on specific components, please refer to their respective README files:

*   [Core README](./Kevahu.Microservices.Core/README.md)
*   [Gateway README](./Kevahu.Microservices.Gateway/README.md)
*   [Orchestrator README](./Kevahu.Microservices.Orchestrator/README.md)
*   [Web Orchestrator README](./Kevahu.Microservices.Orchestrator.Web/README.md)

## Sample Implementation

The `/Samples` directory contains a practical example simulating a webshop with stock management and payment processing. To run it using Docker:

1.  Ensure Docker Desktop is running.
2.  Execute the `Docker Compose.bat` script.
3.  This script builds the necessary Docker images and starts the containers defined in `/Samples/docker-compose.yml`.
4.  Once running, you can access the webshop interface via your browser (typically at `http://localhost/`).

The sample demonstrates how the Gateway, Web Orchestrator (hosting the frontend), and backend Orchestrators (hosting Stock and Payment services) interact within the framework's mesh.

## References

de la Torre, C., Wagner, B., & Rousos, M. (2023). *.NET Microservices: Architecture for Containerized .NET Applications*. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/architecture/microservices/