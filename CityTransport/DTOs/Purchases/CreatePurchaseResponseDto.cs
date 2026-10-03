namespace CityTransport.DTOs.Purchases;

public record CreatePurchaseResponseDto
{
    public required Guid OrderId { get; init; }
    public required string PaymentUrl { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}