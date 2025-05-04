using System.Text.Json.Serialization;

namespace Domain.Models
{
    public class Payment
    {
        #region Properties

        [JsonIgnore]
        public decimal Amount { get; set; }

        [JsonPropertyName("cardCVV")]
        public string CardCvv { get; set; }

        [JsonPropertyName("cardExpiryDate")]
        public string CardExpiryDate { get; set; }

        [JsonPropertyName("cardHolderName")]
        public string CardHolderName { get; set; }

        [JsonPropertyName("cardNumber")]
        public string CardNumber { get; set; }

        [JsonPropertyName("stockIds")]
        public List<int> StockIds { get; set; }

        #endregion Properties
    }
}