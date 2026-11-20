using CityTransport.DTOs.Routes;

namespace CityTransport.Services;

public interface IStopService
{
    Task<IEnumerable<StopDto>> GetAllStopsAsync(double? minLat = null, double? minLng = null, double? maxLat = null, double? maxLng = null);
    Task<StopDto?> GetStopByIdAsync(Guid id);
    Task<StopDto> CreateStopAsync(CreateStopRequestDto dto);
    Task<BatchImportResultDto> BatchImportStopsAsync(BatchImportStopsRequestDto request);
    Task<bool> DeleteStopAsync(Guid id);
}
