using System.Text.Json.Serialization;

namespace CityTransport.DTOs.Payments;

public class MonobankWebhookDto
{
    [JsonPropertyName("invoiceId")]
    public string InvoiceId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "created", "processing", "success", "failure"

    [JsonPropertyName("amount")]
    public long Amount { get; set; } //3000 = 30.00 UAH

    [JsonPropertyName("ccy")]
    public int CurrencyCode { get; set; } = 980; // UAH

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty; //OrderId (Guid)

    [JsonPropertyName("createdDate")]
    public long CreatedDate { get; set; }

    [JsonPropertyName("modifiedDate")]
    public long ModifiedDate { get; set; }
}