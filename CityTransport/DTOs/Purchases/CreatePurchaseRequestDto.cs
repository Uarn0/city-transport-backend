using System.ComponentModel.DataAnnotations;

namespace CityTransport.DTOs.Purchases;

public record CreatePurchaseRequestDto
{
    [Required(ErrorMessage = "TariffId є обов'язковим")]
    public required Guid TariffId { get; init; }

    public Guid? RouteId { get; init; }

    [Range(1, 50, ErrorMessage = "Кількість квитків має бути від 1 до 50")]
    public int Quantity { get; init; } = 1;

    [Required(ErrorMessage = "Вкажіть платіжного провайдера")]
    public required string PaymentProvider { get; init; } = "Monobank";
}