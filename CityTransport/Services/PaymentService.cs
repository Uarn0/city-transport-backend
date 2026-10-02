using CityTransport.Data;
using CityTransport.DTOs.Payments;
using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityTransport.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext dbContext, ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PaymentWebhookResponseDto> ProcessMonobankWebhookAsync(MonobankWebhookDto webhookDto)
    {
        if (!Guid.TryParse(webhookDto.Reference, out var orderId))
        {
            _logger.LogWarning("Invalid Order ID format in reference: {Reference}", webhookDto.Reference);
            throw new InvalidOperationException("Invalid Order ID format in webhook reference.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Tariff)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            _logger.LogWarning("Order with ID {OrderId} was not found.", orderId);
            throw new KeyNotFoundException($"Order with ID {orderId} was not found.");
        }

        // Check if order is already processed
        if (order.Status == OrderStatus.Paid)
        {
            _logger.LogInformation("Order {OrderId} is already paid. Skipping duplicate processing.", orderId);

            var existingTicket = await _dbContext.Tickets
                .FirstOrDefaultAsync(t => t.OrderId == orderId);

            return new PaymentWebhookResponseDto
            {
                Success = true,
                Message = "Order already processed.",
                OrderId = order.Id,
                TicketId = existingTicket?.Id
            };
        }

        // Verify payment status (Monobank returns 'success' for completed payments)
        if (!string.Equals(webhookDto.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Payment for order {OrderId} failed with status: {Status}", orderId, webhookDto.Status);
            order.Status = OrderStatus.Cancelled;
            await _dbContext.SaveChangesAsync();

            return new PaymentWebhookResponseDto
            {
                Success = false,
                Message = $"Payment failed with status: {webhookDto.Status}",
                OrderId = order.Id
            };
        }

        order.Status = OrderStatus.Paid;

        var durationMinutes = order.Tariff?.DurationMinutes ?? 60;

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            UserId = order.UserId,
            OrderId = order.Id,
            QrCodePayload = Guid.NewGuid().ToString("N"), // Unique payload for mobile App QR rendering
            Status = TicketStatus.Active,
            PurchasedAt = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddMinutes(durationMinutes)
        };

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Payment successful for Order {OrderId}. Generated Ticket {TicketId}.", order.Id, ticket.Id);

        return new PaymentWebhookResponseDto
        {
            Success = true,
            Message = "Payment successfully processed and ticket generated.",
            OrderId = order.Id,
            TicketId = ticket.Id
        };
    }
}