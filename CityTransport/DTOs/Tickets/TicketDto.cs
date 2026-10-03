using CityTransport.Entities;

namespace CityTransport.DTOs.Tickets;

public class TicketDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    
    // Tariff and Route metadata
    public string TariffName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? RouteNumber { get; set; }
    public string? RouteName { get; set; }

    //QR
    public string QrCodePayload { get; set; } = string.Empty;
  
    public TicketStatus Status { get; set; }
    public DateTime PurchasedAt { get; set; }
    public DateTime ValidUntil { get; set; }
    public bool IsActive => Status == TicketStatus.Active && ValidUntil > DateTime.UtcNow;
}