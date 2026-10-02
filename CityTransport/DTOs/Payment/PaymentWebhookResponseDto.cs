namespace CityTransport.DTOs.Payments;

public class PaymentWebhookResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? OrderId { get; set; }
    public Guid? TicketId { get; set; }
}