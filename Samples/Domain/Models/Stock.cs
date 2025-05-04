using System.Text.Json.Serialization;

namespace Domain.Models
{
    public class Stock
    {
        #region Properties

        [JsonIgnore]
        public int AvailableQuantity { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        #endregion Properties
    }
}