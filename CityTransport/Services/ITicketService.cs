using CityTransport.DTOs.Tickets;

namespace CityTransport.Services;

public interface ITicketService
{
    Task<IEnumerable<TicketDto>> GetUserTicketsAsync(Guid userId, bool onlyActive = false);
    Task<TicketDto?> GetTicketByIdAsync(Guid ticketId, Guid userId);
}