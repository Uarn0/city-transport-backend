using CityTransport.DTOs.Purchases;

namespace CityTransport.Services;

public interface IPurchaseService
{
    Task<CreatePurchaseResponseDto> CreatePurchaseAsync(CreatePurchaseRequestDto request, Guid userId);
}