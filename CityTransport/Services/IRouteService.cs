using CityTransport.DTOs.Routes;

namespace CityTransport.Services;

public interface IRouteService
{
    Task<IEnumerable<RouteWithStopsDto>> GetAllRoutesAsync();
    Task<RouteWithStopsDto?> GetRouteByIdAsync(Guid id);
    Task<RouteWithStopsDto> CreateRouteAsync(CreateRouteRequestDto dto);
    Task<RouteWithStopsDto?> UpdateRouteStopsAsync(Guid id, UpdateRouteStopsDto dto);
    Task<bool> DeleteRouteAsync(Guid id);
    Task<IEnumerable<RouteSearchResultDto>> FindRoutesAsync(double fromLat, double fromLng, double toLat, double toLng);
}
