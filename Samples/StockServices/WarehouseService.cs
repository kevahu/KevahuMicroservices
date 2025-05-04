using Domain.Models;
using Domain.Services;
using Kevahu.Microservices.Core.RemoteProcedureCall.Attributes;

namespace StockServices
{
    [RpcImplementation]
    public class WarehouseService : IStockService
    {
        #region Public Constructors

        public WarehouseService()
        {
            _stockItems.AddRange(new List<Stock>
            {
                new Stock { Id = 1, Title = "Microservice Multiplexer", Description = "Splits one request into many, because why not?", Price = 99.99m, AvailableQuantity = 10 },
                new Stock { Id = 2, Title = "Monolith Mincer", Description = "Turns legacy code into micro-sized chunks (batteries not included).", Price = 149.50m, AvailableQuantity = 5 },
                new Stock { Id = 3, Title = "API Gateway Gargle", Description = "Cleanses your API requests. Minty fresh!", Price = 19.99m, AvailableQuantity = 50 },
                new Stock { Id = 4, Title = "Service Discovery Spectacles", Description = "Helps services find each other, even in the dark.", Price = 45.00m, AvailableQuantity = 20 },
                new Stock { Id = 5, Title = "Circuit Breaker Clogs", Description = "Stops cascading failures, stylishly.", Price = 35.75m, AvailableQuantity = 30 },
                new Stock { Id = 6, Title = "Container Cuddler", Description = "Keeps your Docker containers warm and cozy.", Price = 29.99m, AvailableQuantity = 15 },
                new Stock { Id = 7, Title = "Event Bus Elixir", Description = "Guaranteed to make your events flow smoothly (or your money back*). *Not really.", Price = 75.00m, AvailableQuantity = 12 },
                new Stock { Id = 8, Title = "Load Balancer Lunchbox", Description = "Distributes your workload, and your sandwiches.", Price = 22.50m, AvailableQuantity = 40 },
                new Stock { Id = 9, Title = "Saga Soother", Description = "Calms down complex distributed transactions.", Price = 55.00m, AvailableQuantity = 18 },
                new Stock { Id = 10, Title = "Observability Ointment", Description = "Makes invisible problems visible. Apply liberally.", Price = 65.25m, AvailableQuantity = 25 },
                new Stock { Id = 11, Title = "Idempotency Idol", Description = "Worship this to prevent duplicate requests.", Price = 15.99m, AvailableQuantity = 100 },
                new Stock { Id = 12, Title = "Serverless Slippers", Description = "So comfy, you'll forget about managing servers.", Price = 42.00m, AvailableQuantity = 22 },
                new Stock { Id = 13, Title = "Chaos Monkey Mallet", Description = "For when you *really* want to test resilience.", Price = 88.88m, AvailableQuantity = 8 },
                new Stock { Id = 14, Title = "Distributed Cache Cologne", Description = "Smells like fresh data.", Price = 33.00m, AvailableQuantity = 35 },
                new Stock { Id = 15, Title = "Blue-Green Deployment Dye", Description = "Switch traffic seamlessly, now in vibrant colors!", Price = 28.50m, AvailableQuantity = 28 },
                new Stock { Id = 16, Title = "Canary Release Cage", Description = "Slowly introduce new features, safely contained.", Price = 49.99m, AvailableQuantity = 14 },
                new Stock { Id = 17, Title = "Polyglot Persistence Pills", Description = "Helps your system speak multiple database languages.", Price = 95.00m, AvailableQuantity = 9 },
                new Stock { Id = 18, Title = "Rate Limiter Ruler", Description = "Measures requests and smacks the noisy ones.", Price = 18.00m, AvailableQuantity = 60 },
                new Stock { Id = 19, Title = "Health Check Hammer", Description = "Regularly taps your services to ensure they're alive.", Price = 12.95m, AvailableQuantity = 75 },
                new Stock { Id = 20, Title = "Message Queue Mittens", Description = "Keeps your messages warm until they're ready.", Price = 24.00m, AvailableQuantity = 33 },
                new Stock { Id = 21, Title = "Configuration Cauldron", Description = "Mix all your settings in one magical pot.", Price = 60.00m, AvailableQuantity = 11 },
                new Stock { Id = 22, Title = "Secret Management Safe", Description = "Keeps your API keys safer than Fort Knox.", Price = 199.99m, AvailableQuantity = 7 },
                new Stock { Id = 23, Title = "Strangler Fig Fertilizer", Description = "Helps your new microservices slowly consume the monolith.", Price = 40.00m, AvailableQuantity = 19 },
                new Stock { Id = 24, Title = "Bounded Context Binoculars", Description = "See the clear boundaries between your domains.", Price = 58.50m, AvailableQuantity = 16 },
                new Stock { Id = 25, Title = "Zero-Downtime Draught", Description = "Drink this before deployments for uninterrupted service (placebo effect).", Price = 77.77m, AvailableQuantity = 13 }
            });
        }

        #endregion Public Constructors

        #region Fields

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly List<Stock> _stockItems = [];

        #endregion Fields

        #region Public Methods

        public Stock CreateStockItem(string title, string description, decimal price, int quantity)
        {
            _semaphore.Wait();
            try
            {
                var newStockItem = new Stock
                {
                    Id = _stockItems.Count + 1,
                    Title = title,
                    Description = description,
                    Price = price,
                    AvailableQuantity = quantity
                };
                _stockItems.Add(newStockItem);
                return newStockItem;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public Stock[] GetAllStockItems()
        {
            _semaphore.Wait();
            try
            {
                return _stockItems.Where(s => s.AvailableQuantity > 0).ToArray();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public Stock GetStockDetails(int id)
        {
            _semaphore.Wait();
            try
            {
                var stockItem = _stockItems.FirstOrDefault(s => s.Id == id);
                if (stockItem == null)
                {
                    throw new KeyNotFoundException($"Stock item with ID {id} not found.");
                }
                return stockItem;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void RemoveStock(int id, int quantity)
        {
            _semaphore.Wait();
            try
            {
                var stockItem = _stockItems.FirstOrDefault(s => s.Id == id);
                if (stockItem == null)
                {
                    throw new KeyNotFoundException($"Stock item with ID {id} not found.");
                }
                if (stockItem.AvailableQuantity < quantity)
                {
                    throw new InvalidOperationException($"Not enough stock available for item {stockItem.Title}. Available: {stockItem.AvailableQuantity}, Requested: {quantity}");
                }
                stockItem.AvailableQuantity -= quantity;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void ReturnStock(int id, int quantity)
        {
            _semaphore.Wait();
            try
            {
                var stockItem = _stockItems.FirstOrDefault(s => s.Id == id);
                if (stockItem == null)
                {
                    throw new KeyNotFoundException($"Stock item with ID {id} not found.");
                }
                stockItem.AvailableQuantity += quantity;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        #endregion Public Methods
    }
}