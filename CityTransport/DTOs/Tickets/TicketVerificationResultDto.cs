namespace CityTransport.DTOs.Tickets;

public class TicketVerificationResultDto
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public string? TariffName { get; set; }
    public string? RouteNumber { get; set; }
    public string? Status { get; set; }
    public DateTime? ValidUntil { get; set; }
}