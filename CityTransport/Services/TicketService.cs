using CityTransport.Data;
using CityTransport.DTOs.Tickets;
using CityTransport.Entities;
using CityTransport.Utils.TicketUtils;
using Microsoft.EntityFrameworkCore;

namespace CityTransport.Services;

public class TicketService : ITicketService
{
    private readonly AppDbContext _dbContext;
    private readonly QRCodeGeneratorService _qrCodeGeneratorService;
    private readonly ILogger<TicketService> _logger;

    public TicketService(AppDbContext dbContext, QRCodeGeneratorService qrCodeGeneratorService, ILogger<TicketService> logger)
    {
        _dbContext = dbContext;
        _qrCodeGeneratorService = qrCodeGeneratorService;
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

    public async Task<TicketVerificationResultDto> VerifyAndUseTicketAsync(string encryptedPayload)
    {
        // Cryptographic verification and decryption of a QR code
        if (!_qrCodeGeneratorService.TryDecryptPayload(encryptedPayload, out var ticketGuidStr, out var generatedAt))
        {
            _logger.LogWarning("Failed to decrypt or verify QR code payload.");
            return new TicketVerificationResultDto
            {
                IsValid = false,
                Message = "Недійсний або підроблений QR-код (помилка криптографічної перевірки)."
            };
        }

        if (!Guid.TryParse(ticketGuidStr, out var ticketId))
        {
            return new TicketVerificationResultDto
            {
                IsValid = false,
                Message = "Некоректний ідентифікатор квитка в payload."
            };
        }

        //Checking ticket status in the db
        var ticket = await _dbContext.Tickets
            .Include(t => t.Order)
                .ThenInclude(o => o.Tariff)
            .Include(t => t.Order)
                .ThenInclude(o => o.Route)
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
        {
            return new TicketVerificationResultDto
            {
                IsValid = false,
                Message = "Квиток не знайдено в системі."
            };
        }

        var now = DateTime.UtcNow;

        if (ticket.Status == TicketStatus.Active && ticket.ValidUntil <= now)
        {
            ticket.Status = TicketStatus.Expired;
            await _dbContext.SaveChangesAsync();
        }

        if (ticket.Status == TicketStatus.Expired)
        {
            return new TicketVerificationResultDto
            {
                IsValid = false,
                Message = "Термін дії квитка вичерпано.",
                TicketId = ticket.Id,
                Status = ticket.Status.ToString(),
                ValidUntil = ticket.ValidUntil
            };
        }

        if (ticket.Status == TicketStatus.Used)
        {
            return new TicketVerificationResultDto
            {
                IsValid = false,
                Message = "Цей квиток вже був використаний раніше!",
                TicketId = ticket.Id,
                Status = ticket.Status.ToString(),
                ValidUntil = ticket.ValidUntil
            };
        }

        if (ticket.Status == TicketStatus.Active)
        {
            ticket.Status = TicketStatus.Used;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Ticket {TicketId} successfully verified and marked as Used.", ticket.Id);

            return new TicketVerificationResultDto
            {
                IsValid = true,
                Message = "Квиток успішно перевірено та погашено!",
                TicketId = ticket.Id,
                TariffName = ticket.Order?.Tariff?.Name,
                RouteNumber = ticket.Order?.Route?.Number,
                Status = ticket.Status.ToString(),
                ValidUntil = ticket.ValidUntil
            };
        }

        return new TicketVerificationResultDto
        {
            IsValid = false,
            Message = "Невідомий статус квитка."
        };
    }

    public Task<IEnumerable<string>> GetAvailableTicketTypesAsync()
    {
        //TODO: Implement logic to fetch available ticket types from the database or configuration and add types field in ticket entity(optional)
        throw new NotImplementedException();
    }
}