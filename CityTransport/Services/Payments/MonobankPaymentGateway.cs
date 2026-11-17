using CityTransport.Entities;

namespace CityTransport.Services.Payments;

public class MonobankPaymentGateway : IPaymentGateway
{
    public Task<string> CreatePaymentSessionAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        // Симуляція генерації платіжного посилання через платіжний шлюз
        var paymentUrl = $"https://checkout.sandbox.payment.com/pay/{order.Id}";
        return Task.FromResult(paymentUrl);
    }
}

