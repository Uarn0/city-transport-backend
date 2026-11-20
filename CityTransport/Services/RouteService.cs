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
            .Include(r => r.RouteStops.OrderBy(rs => rs.Order))
                .ThenInclude(rs => rs.Stop)
            .ToListAsync();

        return routes.Select(MapToDto);
    }

    public async Task<RouteWithStopsDto?> GetRouteByIdAsync(Guid id)
    {
        var route = await _context.Routes
            .Include(r => r.RouteStops.OrderBy(rs => rs.Order))
                .ThenInclude(rs => rs.Stop)
            .FirstOrDefaultAsync(r => r.Id == id);

        return route == null ? null : MapToDto(route);
    }

    public async Task<RouteWithStopsDto> CreateRouteAsync(CreateRouteRequestDto dto)
    {
        var route = new Route
        {
            Number = dto.Number,
            Name = dto.Name,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes
        };

        var order = 1;
        foreach (var s in dto.Stops)
        {
            Stop stop;

            if (s.StopId.HasValue && s.StopId.Value != Guid.Empty)
            {
                var existingStop = await _context.Stops.FindAsync(s.StopId.Value);
                if (existingStop != null)
                {
                    stop = existingStop;
                }
                else
                {
                    stop = new Stop
                    {
                        Id = s.StopId.Value,
                        Name = s.Name,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        OsmId = s.OsmId,
                        TransportType = s.TransportType
                    };
                    _context.Stops.Add(stop);
                }
            }
            else
            {
                var existingStop = await _context.Stops.FirstOrDefaultAsync(st =>
                    (s.OsmId != null && st.OsmId == s.OsmId) ||
                    (Math.Abs(st.Latitude - s.Latitude) < 0.0001 && Math.Abs(st.Longitude - s.Longitude) < 0.0001));

                if (existingStop != null)
                {
                    stop = existingStop;
                }
                else
                {
                    stop = new Stop
                    {
                        Name = string.IsNullOrWhiteSpace(s.Name) ? $"Зупинка {order}" : s.Name,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        OsmId = s.OsmId,
                        TransportType = s.TransportType
                    };
                    _context.Stops.Add(stop);
                }
            }

            var routeStop = new RouteStop
            {
                Route = route,
                Stop = stop,
                Order = s.Order > 0 ? s.Order : order++
            };

            route.RouteStops.Add(routeStop);
        }

        _context.Routes.Add(route);
        await _context.SaveChangesAsync();

        return MapToDto(route);
    }

    public async Task<RouteWithStopsDto?> UpdateRouteStopsAsync(Guid id, UpdateRouteStopsDto dto)
    {
        var route = await _context.Routes
            .Include(r => r.RouteStops)
                .ThenInclude(rs => rs.Stop)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (route == null) return null;

        route.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;

        // Remove existing route stops
        var existingRouteStops = route.RouteStops.ToList();
        _context.RouteStops.RemoveRange(existingRouteStops);

        var newRouteStops = new List<RouteStop>();
        var order = 1;

        foreach (var s in dto.Stops)
        {
            Stop stop;
            if (s.StopId.HasValue && s.StopId.Value != Guid.Empty)
            {
                var existingStop = await _context.Stops.FindAsync(s.StopId.Value);
                if (existingStop != null)
                {
                    stop = existingStop;
                }
                else
                {
                    stop = new Stop
                    {
                        Id = s.StopId.Value,
                        Name = s.Name,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        OsmId = s.OsmId,
                        TransportType = s.TransportType
                    };
                    _context.Stops.Add(stop);
                }
            }
            else
            {
                var existingStop = await _context.Stops.FirstOrDefaultAsync(st =>
                    (s.OsmId != null && st.OsmId == s.OsmId) ||
                    (Math.Abs(st.Latitude - s.Latitude) < 0.0001 && Math.Abs(st.Longitude - s.Longitude) < 0.0001));

                if (existingStop != null)
                {
                    stop = existingStop;
                }
                else
                {
                    stop = new Stop
                    {
                        Name = string.IsNullOrWhiteSpace(s.Name) ? $"Зупинка {order}" : s.Name,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        OsmId = s.OsmId,
                        TransportType = s.TransportType
                    };
                    _context.Stops.Add(stop);
                }
            }

            var routeStop = new RouteStop
            {
                RouteId = route.Id,
                Stop = stop,
                Order = s.Order > 0 ? s.Order : order++
            };

            newRouteStops.Add(routeStop);
            _context.RouteStops.Add(routeStop);
        }

        await _context.SaveChangesAsync();

        route.RouteStops = newRouteStops;
        return MapToDto(route);
    }

    public async Task<bool> DeleteRouteAsync(Guid id)
    {
        var route = await _context.Routes
            .Include(r => r.RouteStops)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (route == null) return false;

        _context.Routes.Remove(route);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<RouteSearchResultDto>> FindRoutesAsync(
        double fromLat, double fromLng, double toLat, double toLng)
    {
        var routes = await _context.Routes
            .Include(r => r.RouteStops.OrderBy(rs => rs.Order))
                .ThenInclude(rs => rs.Stop)
            .Where(r => r.RouteStops.Any())
            .ToListAsync();

        var results = new List<RouteSearchResultDto>();

        foreach (var route in routes)
        {
            if (!route.RouteStops.Any()) continue;

            var nearestFrom = route.RouteStops
                .OrderBy(rs => HaversineDistance(fromLat, fromLng, rs.Stop.Latitude, rs.Stop.Longitude))
                .First();

            var nearestTo = route.RouteStops
                .OrderBy(rs => HaversineDistance(toLat, toLng, rs.Stop.Latitude, rs.Stop.Longitude))
                .First();

            // Only include if stops are in proper direction (from order < to order)
            if (nearestFrom.Order >= nearestTo.Order) continue;

            var distanceFromUser = HaversineDistance(fromLat, fromLng, nearestFrom.Stop.Latitude, nearestFrom.Stop.Longitude);

            results.Add(new RouteSearchResultDto
            {
                RouteId = route.Id,
                RouteNumber = route.Number,
                RouteName = route.Name,
                NearestFromStop = MapStopToDto(nearestFrom.Stop, nearestFrom.Order),
                NearestToStop = MapStopToDto(nearestTo.Stop, nearestTo.Order),
                DistanceMeters = distanceFromUser
            });
        }

        return results.OrderBy(r => r.DistanceMeters);
    }

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
        Stops = route.RouteStops.OrderBy(rs => rs.Order).Select(rs => MapStopToDto(rs.Stop, rs.Order)).ToList()
    };

    private static StopDto MapStopToDto(Stop stop, int order) => new()
    {
        Id = stop.Id,
        Name = stop.Name,
        Latitude = stop.Latitude,
        Longitude = stop.Longitude,
        Order = order,
        OsmId = stop.OsmId,
        TransportType = stop.TransportType
    };
}
