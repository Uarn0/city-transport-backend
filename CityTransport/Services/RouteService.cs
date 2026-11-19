using CityTransport.Data;
using CityTransport.DTOs.Routes;
using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;
using Route = CityTransport.Entities.Route;

namespace CityTransport.Services;

public class RouteService : IRouteService
{
    private readonly AppDbContext _context;

    public RouteService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RouteWithStopsDto>> GetAllRoutesAsync()
    {
        var routes = await _context.Routes
            .Include(r => r.Stops.OrderBy(s => s.Order))
            .ToListAsync();

        return routes.Select(MapToDto);
    }

    public async Task<RouteWithStopsDto?> GetRouteByIdAsync(Guid id)
    {
        var route = await _context.Routes
            .Include(r => r.Stops.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(r => r.Id == id);

        return route == null ? null : MapToDto(route);
    }

    public async Task<RouteWithStopsDto> CreateRouteAsync(CreateRouteRequestDto dto)
    {
        var route = new Route
        {
            Number = dto.Number,
            Name = dto.Name,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            Stops = dto.Stops.Select((s, i) => new Stop
            {
                Name = s.Name,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Order = s.Order > 0 ? s.Order : i + 1
            }).ToList()
        };

        _context.Routes.Add(route);
        await _context.SaveChangesAsync();

        return MapToDto(route);
    }

    public async Task<RouteWithStopsDto?> UpdateRouteStopsAsync(Guid id, UpdateRouteStopsDto dto)
    {
        var route = await _context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (route == null) return null;

        route.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;

        // Remove existing stops
        var existingStops = route.Stops.ToList();
        _context.Stops.RemoveRange(existingStops);

        // Add new stops directly to DbContext
        var newStops = new List<Stop>();
        var order = 1;
        foreach (var s in dto.Stops)
        {
            var stop = new Stop
            {
                Id = Guid.NewGuid(),
                RouteId = route.Id,
                Name = s.Name,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Order = s.Order > 0 ? s.Order : order++
            };
            newStops.Add(stop);
            _context.Stops.Add(stop);
        }

        await _context.SaveChangesAsync();

        route.Stops = newStops;
        return MapToDto(route);
    }

    public async Task<bool> DeleteRouteAsync(Guid id)
    {
        var route = await _context.Routes.FindAsync(id);
        if (route == null) return false;

        _context.Routes.Remove(route);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<RouteSearchResultDto>> FindRoutesAsync(
        double fromLat, double fromLng, double toLat, double toLng)
    {
        var routes = await _context.Routes
            .Include(r => r.Stops.OrderBy(s => s.Order))
            .Where(r => r.Stops.Any())
            .ToListAsync();

        var results = new List<RouteSearchResultDto>();

        foreach (var route in routes)
        {
            if (!route.Stops.Any()) continue;

            var nearestFrom = route.Stops
                .OrderBy(s => HaversineDistance(fromLat, fromLng, s.Latitude, s.Longitude))
                .First();

            var nearestTo = route.Stops
                .OrderBy(s => HaversineDistance(toLat, toLng, s.Latitude, s.Longitude))
                .First();

            // Only include if stops are in proper direction (from order < to order)
            if (nearestFrom.Order >= nearestTo.Order) continue;

            var distanceFromUser = HaversineDistance(fromLat, fromLng, nearestFrom.Latitude, nearestFrom.Longitude);

            results.Add(new RouteSearchResultDto
            {
                RouteId = route.Id,
                RouteNumber = route.Number,
                RouteName = route.Name,
                NearestFromStop = MapStopToDto(nearestFrom),
                NearestToStop = MapStopToDto(nearestTo),
                DistanceMeters = distanceFromUser
            });
        }

        return results.OrderBy(r => r.DistanceMeters);
    }

    // Haversine formula: distance in meters between two lat/lng points
    private static double HaversineDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000; // Earth radius in meters
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;

    private static RouteWithStopsDto MapToDto(Route route) => new()
    {
        Id = route.Id,
        Number = route.Number,
        Name = route.Name,
        EstimatedDurationMinutes = route.EstimatedDurationMinutes,
        Stops = route.Stops.OrderBy(s => s.Order).Select(MapStopToDto).ToList()
    };

    private static StopDto MapStopToDto(Stop stop) => new()
    {
        Id = stop.Id,
        Name = stop.Name,
        Latitude = stop.Latitude,
        Longitude = stop.Longitude,
        Order = stop.Order
    };
}
