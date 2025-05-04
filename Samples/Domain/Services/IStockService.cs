using Domain.Models;
using Kevahu.Microservices.Core.RemoteProcedureCall.Attributes;

namespace Domain.Services
{
    [RpcInterface]
    public interface IStockService
    {
        #region Public Methods

        Stock CreateStockItem(string title, string description, decimal price, int quantity);

        Stock[] GetAllStockItems();

        Stock GetStockDetails(int id);

        void RemoveStock(int id, int quantity);

        void ReturnStock(int id, int quantity);

        #endregion Public Methods
    }
}