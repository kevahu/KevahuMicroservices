using Domain.Models;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;

namespace WebshopServices
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        #region Classes

        public class Result
        {
            #region Properties

            [JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonPropertyName("success")]
            public bool Success { get; set; }

            #endregion Properties
        }

        #endregion Classes

        #region Public Constructors

        public PaymentController(IPaymentService paymentService, IStockService stockService)
        {
            _paymentService = paymentService;
            _stockService = stockService;
        }

        #endregion Public Constructors

        #region Fields

        private readonly IPaymentService _paymentService;
        private readonly IStockService _stockService;

        #endregion Fields

        #region Public Methods

        [HttpPost]
        public Result ProcessPayment(Payment payment)
        {
            payment.Amount = 0;
            Dictionary<int, int> cart = [];
            foreach (var stockId in payment.StockIds)
            {
                var stock = _stockService.GetStockDetails(stockId);
                if (stock == null)
                {
                    return new Result
                    {
                        Message = $"Stock with ID {stockId} not found.",
                        Success = false
                    };
                }
                else
                {
                    if (cart.ContainsKey(stockId))
                    {
                        cart[stockId]++;
                    }
                    else
                    {
                        cart[stockId] = 1;
                    }
                }
                payment.Amount += stock.Price;
            }
            foreach (var stockId in cart.Keys)
            {
                try
                {
                    _stockService.RemoveStock(stockId, cart[stockId]);
                }
                catch (Exception ex)
                {
                    if (ex is TargetInvocationException targetEx)
                    {
                        ex = targetEx.InnerException;
                    }
                    return new Result
                    {
                        Message = ex.Message,
                        Success = false
                    };
                }
            }
            string? errorMessage = _paymentService.TryProcessPayment(payment);
            if (errorMessage != null)
            {
                foreach (var stockId in cart.Keys)
                {
                    _stockService.ReturnStock(stockId, cart[stockId]);
                }
                return new Result
                {
                    Message = errorMessage,
                    Success = false
                };
            }
            return new Result
            {
                Message = "Payment processed successfully.",
                Success = true
            };
        }

        #endregion Public Methods
    }
}