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
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
    public int RemainingMinutes { get; init; }
    public bool IsFreeTransfer { get; init; }

    public static TransferValidationResult Success(int remainingMinutes, string message = "Пересадка успішна (в межах діючого часового вікна).")
    {
        return new TransferValidationResult
        {
            IsValid = true,
            IsFreeTransfer = true,
            RemainingMinutes = Math.Max(0, remainingMinutes),
            Message = message
        };
    }

    public static TransferValidationResult Failure(string message, int remainingMinutes = 0)
    {
        return new TransferValidationResult
        {
            IsValid = false,
            IsFreeTransfer = false,
            RemainingMinutes = Math.Max(0, remainingMinutes),
            Message = message
        };
    }
}
