using CityTransport.Data;
using CityTransport.DTOs.Purchases;
using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityTransport.Services;

public class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _context;

    public PurchaseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CreatePurchaseResponseDto> CreatePurchaseAsync(CreatePurchaseRequestDto request, Guid userId)
    {
        // does tariff exist in the database
        var tariff = await _context.Tariffs
            .Include(t => t.Routes)
            .FirstOrDefaultAsync(t => t.Id == request.TariffId)
            ?? throw new KeyNotFoundException($"Тариф з ID '{request.TariffId}' не знайдено.");

        if (!tariff.IsActive)
        {
            throw new InvalidOperationException($"Тариф '{tariff.Name}' неактивний.");
        }

        //  Check if the route exists in the database (if provided)
        if (request.RouteId.HasValue)
        {
            var routeExists = await _context.Routes.AnyAsync(r => r.Id == request.RouteId.Value);
            if (!routeExists)
            {
                throw new KeyNotFoundException($"Маршрут з ID '{request.RouteId.Value}' не знайдено.");
            }

            // if the tariff has associated routes, check if the provided route is valid for this tariff
            if (tariff.Routes.Any() && !tariff.Routes.Any(r => r.Id == request.RouteId.Value))
            {
                throw new InvalidOperationException($"Тариф '{tariff.Name}' не діє на обраний маршрут.");
            }
        }

        var totalAmount = tariff.Price * request.Quantity;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TariffId = request.TariffId,
            RouteId = request.RouteId,
            Quantity = request.Quantity,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        //paymentmock URL
        var paymentUrl = $"https://checkout.sandbox.payment.com/pay/{order.Id}";

        //ResponseDto
        return new CreatePurchaseResponseDto
        {
            OrderId = order.Id,
            PaymentUrl = paymentUrl,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt
        };
    }
}