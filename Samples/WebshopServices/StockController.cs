using Domain.Models;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace WebshopServices
{
    [Route("api/[controller]")]
    [ApiController]
    public class StockController : ControllerBase
    {
        #region Public Constructors

        public StockController(IStockService stockService)
        {
            _stockService = stockService;
        }

        #endregion Public Constructors

        #region Fields

        private readonly IStockService _stockService;

        #endregion Fields

        #region Public Methods

        [HttpGet("{id}")]
        public Stock GetStock(int id)
        {
            return _stockService.GetStockDetails(id);
        }

        [HttpGet]
        public Stock[] ListStock()
        {
            return _stockService.GetAllStockItems();
        }

        #endregion Public Methods
    }
}