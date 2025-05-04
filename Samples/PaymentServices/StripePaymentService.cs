using Domain.Models;
using Domain.Services;
using Kevahu.Microservices.Core.RemoteProcedureCall.Attributes;

namespace PaymentServices
{
    [RpcImplementation]
    public class StripePaymentService : IPaymentService
    {
        #region Public Methods

        public string? TryProcessPayment(Payment payment)
        {
            if (payment.StockIds.Count == 0)
            {
                return "No stock items selected for payment.";
            }
            if (payment.Amount <= 0)
            {
                return "Invalid payment amount.";
            }
            if (string.IsNullOrEmpty(payment.CardNumber) || string.IsNullOrEmpty(payment.CardHolderName) || string.IsNullOrEmpty(payment.CardExpiryDate) || string.IsNullOrEmpty(payment.CardCvv))
            {
                return "Invalid card details.";
            }
            if (!DateTime.TryParseExact(payment.CardExpiryDate, "MM/yy", null, System.Globalization.DateTimeStyles.None, out DateTime expiryDate) || expiryDate < DateTime.Now)
            {
                return "Invalid card expiry date.";
            }
            // Luhn algorithm for card number validation
            int sum = 0;
            bool alternate = false;
            for (int i = payment.CardNumber.Length - 1; i >= 0; i--)
            {
                int n = int.Parse(payment.CardNumber[i].ToString());
                if (alternate)
                {
                    n *= 2;
                    if (n > 9)
                    {
                        n -= 9;
                    }
                }
                sum += n;
                alternate = !alternate;
            }
            if (sum % 10 != 0)
            {
                return "Invalid card number.";
            }

            return null;
        }

        #endregion Public Methods
    }
}