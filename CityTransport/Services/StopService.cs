using CityTransport.Data;
using CityTransport.DTOs.Routes;
using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;

namespace CityTransport.Services;

public class StopService : IStopService
{
    private readonly AppDbContext _context;

    public StopService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<StopDto>> GetAllStopsAsync(
        double? minLat = null, double? minLng = null, double? maxLat = null, double? maxLng = null)
    {
        var query = _context.Stops.AsNoTracking();

        if (minLat.HasValue && maxLat.HasValue && minLng.HasValue && maxLng.HasValue)
        {
            query = query.Where(s =>
                s.Latitude >= minLat.Value && s.Latitude <= maxLat.Value &&
                s.Longitude >= minLng.Value && s.Longitude <= maxLng.Value);
        }

        var stops = await query
            .OrderBy(s => s.Name)
            .ToListAsync();

        return stops.Select(MapToDto);
    }

    public async Task<StopDto?> GetStopByIdAsync(Guid id)
    {
        var stop = await _context.Stops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        return stop == null ? null : MapToDto(stop);
    }

    public async Task<StopDto> CreateStopAsync(CreateStopRequestDto dto)
    {
        // Deduplication check: by OsmId or very close coordinates (< 30m)
        Stop? existing = null;

        if (dto.OsmId.HasValue)
        {
            existing = await _context.Stops.FirstOrDefaultAsync(s => s.OsmId == dto.OsmId.Value);
        }

        if (existing == null)
        {
            var candidates = await _context.Stops
                .Where(s => Math.Abs(s.Latitude - dto.Latitude) < 0.001 && Math.Abs(s.Longitude - dto.Longitude) < 0.001)
                .ToListAsync();

            existing = candidates.FirstOrDefault(s =>
                HaversineDistance(s.Latitude, s.Longitude, dto.Latitude, dto.Longitude) < 30);
        }

        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name != existing.Name)
            {
                existing.Name = dto.Name;
            }
            if (dto.OsmId.HasValue && !existing.OsmId.HasValue)
            {
                existing.OsmId = dto.OsmId;
            }
            if (!string.IsNullOrWhiteSpace(dto.TransportType))
            {
                existing.TransportType = dto.TransportType;
            }

            await _context.SaveChangesAsync();
            return MapToDto(existing);
        }

        var newStop = new Stop
        {
            Name = string.IsNullOrWhiteSpace(dto.Name)
                ? (dto.OsmId.HasValue ? $"Зупинка #{dto.OsmId}" : "Нова зупинка")
                : dto.Name.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            OsmId = dto.OsmId,
            TransportType = dto.TransportType
        };

        _context.Stops.Add(newStop);
        await _context.SaveChangesAsync();

        return MapToDto(newStop);
    }

    public async Task<BatchImportResultDto> BatchImportStopsAsync(BatchImportStopsRequestDto request)
    {
        var result = new BatchImportResultDto
        {
            TotalReceived = request.Stops.Count
        };

        if (request.Stops.Count == 0)
        {
            return result;
        }

        // Fetch existing stops in memory for deduplication
        var existingStops = await _context.Stops.ToListAsync();
        var existingOsmIds = new HashSet<long>(existingStops.Where(s => s.OsmId.HasValue).Select(s => s.OsmId!.Value));

        var newlyAdded = new List<Stop>();

        foreach (var item in request.Stops)
        {
            // 1. Check OsmId duplicate
            if (item.OsmId.HasValue && existingOsmIds.Contains(item.OsmId.Value))
            {
                result.SkippedDuplicatesCount++;
                continue;
            }

            // 2. Check coordinates proximity (< 30m) against both DB and newly added in this batch
            var isDuplicateCoord = existingStops.Any(s =>
                Math.Abs(s.Latitude - item.Latitude) < 0.001 &&
                Math.Abs(s.Longitude - item.Longitude) < 0.001 &&
                HaversineDistance(s.Latitude, s.Longitude, item.Latitude, item.Longitude) < 30)
                ||
                newlyAdded.Any(s =>
                Math.Abs(s.Latitude - item.Latitude) < 0.001 &&
                Math.Abs(s.Longitude - item.Longitude) < 0.001 &&
                HaversineDistance(s.Latitude, s.Longitude, item.Latitude, item.Longitude) < 30);

            if (isDuplicateCoord)
            {
                result.SkippedDuplicatesCount++;
                continue;
            }

            var stop = new Stop
            {
                Name = string.IsNullOrWhiteSpace(item.Name)
                    ? (item.OsmId.HasValue ? $"Зупинка #{item.OsmId}" : "Зупинка громадського транспорту")
                    : item.Name.Trim(),
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                OsmId = item.OsmId,
                TransportType = item.TransportType
            };

            if (item.OsmId.HasValue)
            {
                existingOsmIds.Add(item.OsmId.Value);
            }

            newlyAdded.Add(stop);
            _context.Stops.Add(stop);
        }

        if (newlyAdded.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        result.ImportedCount = newlyAdded.Count;
        result.Stops = newlyAdded.Select(MapToDto).ToList();

        return result;
    }

    public async Task<bool> DeleteStopAsync(Guid id)
    {
        var stop = await _context.Stops
            .Include(s => s.RouteStops)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (stop == null) return false;

        _context.Stops.Remove(stop);
        await _context.SaveChangesAsync();
        return true;
    }

    private static double HaversineDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static StopDto MapToDto(Stop stop) => new()
    {
        Id = stop.Id,
        Name = stop.Name,
        Latitude = stop.Latitude,
        Longitude = stop.Longitude,
        OsmId = stop.OsmId,
        TransportType = stop.TransportType,
        Order = 0
    };
}
