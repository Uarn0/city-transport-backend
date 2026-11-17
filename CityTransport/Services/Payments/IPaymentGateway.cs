using CityTransport.Entities;

namespace CityTransport.Services.Payments;

public interface IPaymentGateway
{
    /// <summary>
    /// Ініціалізує платіжну сесію для замовлення та повертає посилання на оплату (checkout URL)
    /// </summary>
    Task<string> CreatePaymentSessionAsync(Order order);
}

