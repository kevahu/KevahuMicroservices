@echo off
echo Restoring Kevahu.Microservices...
dotnet restore .\Kevahu.Microservices.Core\Kevahu.Microservices.Core.csproj > nul 2>&1
dotnet restore .\Kevahu.Microservices.Gateway\Kevahu.Microservices.Gateway.csproj > nul 2>&1
dotnet restore .\Kevahu.Microservices.Orchestrator\Kevahu.Microservices.Orchestrator.csproj > nul 2>&1
dotnet restore .\Kevahu.Microservices.Orchestrator.Web\Kevahu.Microservices.Orchestrator.Web.csproj > nul 2>&1

echo Building Kevahu.Microservices and NuGet packages...
dotnet build .\Kevahu.Microservices.Core\Kevahu.Microservices.Core.csproj -c Release > nul 2>&1
dotnet build .\Kevahu.Microservices.Gateway\Kevahu.Microservices.Gateway.csproj -c Release > nul 2>&1
dotnet build .\Kevahu.Microservices.Orchestrator\Kevahu.Microservices.Orchestrator.csproj -c Release > nul 2>&1
dotnet build .\Kevahu.Microservices.Orchestrator.Web\Kevahu.Microservices.Orchestrator.Web.csproj -c Release > nul 2>&1

echo I've put some delay between the gateway and other services startup, so don't panic if the other services are not up yet immediately.
echo Starting Docker Compose...
docker compose -f ./Samples/compose.yaml up