using CityTransport.Data;
using CityTransport.DTOs.Tickets;
using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityTransport.Services;

public class TicketService : ITicketService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TicketService> _logger;

    public TicketService(AppDbContext dbContext, ILogger<TicketService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<TicketDto>> GetUserTicketsAsync(Guid userId, bool onlyActive = false)
    {
        // Fetch tickets with navigation properties via Order
        var query = _dbContext.Tickets
            .Include(t => t.Order)
                .ThenInclude(o => o.Tariff)
            .Include(t => t.Order)
                .ThenInclude(o => o.Route)
            .Where(t => t.UserId == userId);

        var tickets = await query.ToListAsync();

        var now = DateTime.UtcNow;
        var hasUpdates = false;
        foreach (var ticket in tickets)
        {
            if (ticket.Status == TicketStatus.Active && ticket.ValidUntil <= now)
            {
                ticket.Status = TicketStatus.Expired;
                hasUpdates = true;
            }
        }

        if (hasUpdates)
        {
            await _dbContext.SaveChangesAsync();
        }

        if (onlyActive)
        {
            tickets = tickets.Where(t => t.Status == TicketStatus.Active && t.ValidUntil > now).ToList();
        }

        // Map entities to DTOs
        return tickets.Select(t => new TicketDto
        {
            Id = t.Id,
            OrderId = t.OrderId,
            UserId = t.UserId,
            TariffName = t.Order?.Tariff?.Name ?? "Standard Tariff",
            Price = t.Order?.Tariff?.Price ?? 0m,
            RouteNumber = t.Order?.Route?.Number,
            RouteName = t.Order?.Route?.Name,
            QrCodePayload = t.QrCodePayload,
            Status = t.Status,
            PurchasedAt = t.PurchasedAt,
            ValidUntil = t.ValidUntil
        }).OrderByDescending(t => t.PurchasedAt);
    }

    public async Task<TicketDto?> GetTicketByIdAsync(Guid ticketId, Guid userId)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Order)
                .ThenInclude(o => o.Tariff)
            .Include(t => t.Order)
                .ThenInclude(o => o.Route)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId);

        if (ticket == null)
            return null;

        // Auto-expire check
        if (ticket.Status == TicketStatus.Active && ticket.ValidUntil <= DateTime.UtcNow)
        {
            ticket.Status = TicketStatus.Expired;
            await _dbContext.SaveChangesAsync();
        }

        return new TicketDto
        {
            Id = ticket.Id,
            OrderId = ticket.OrderId,
            UserId = ticket.UserId,
            TariffName = ticket.Order?.Tariff?.Name ?? "Standard Tariff",
            Price = ticket.Order?.Tariff?.Price ?? 0m,
            RouteNumber = ticket.Order?.Route?.Number,
            RouteName = ticket.Order?.Route?.Name,
            QrCodePayload = ticket.QrCodePayload,
            Status = ticket.Status,
            PurchasedAt = ticket.PurchasedAt,
            ValidUntil = ticket.ValidUntil
        };
    }
}