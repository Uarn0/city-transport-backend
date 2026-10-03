using CityTransport.DTOs.Payments;

namespace CityTransport.Services;

public interface IPaymentService
{
    Task<PaymentWebhookResponseDto> ProcessMonobankWebhookAsync(MonobankWebhookDto webhookDto);
}