using CityTransport.Entities;

namespace CityTransport.Services.Transfers;

public class TransferValidationRequest
{
    public Guid TicketId { get; set; }
    public DateTime PurchasedAt { get; set; }
    public DateTime ValidUntil { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Active;
    public Guid? FirstRouteId { get; set; }
    public Guid TargetRouteId { get; set; }
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
    public DateTime? LastValidationTime { get; set; }
}

public class TransferValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RemainingMinutes { get; set; }
    public bool IsFreeTransfer { get; set; }
}
