using Domain.Models;
using Kevahu.Microservices.Core.RemoteProcedureCall.Attributes;

namespace Domain.Services
{
    [RpcInterface]
    public interface IPaymentService
    {
        #region Public Methods

        string? TryProcessPayment(Payment payment);

        #endregion Public Methods
    }
}