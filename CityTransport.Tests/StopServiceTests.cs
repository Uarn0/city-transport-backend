using CityTransport.Data;
using CityTransport.DTOs.Routes;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.EntityFrameworkCore;

namespace CityTransport.Tests;

public class StopServiceTests : IAsyncLifetime
{
    private DbContextOptions<AppDbContext> _options = null!;
    private AppDbContext _context = null!;
    private StopService _service = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _context = new AppDbContext(_options);
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();

        _service = new StopService(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.CloseConnectionAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetAllStopsAsync_ReturnsStops_AndFiltersByBBox()
    {
        // Arrange
        _context.Stops.AddRange(
            new Stop { Name = "Центр", Latitude = 49.842, Longitude = 24.031 },
            new Stop { Name = "Сихів", Latitude = 49.790, Longitude = 24.050 }
        );
        await _context.SaveChangesAsync();

        // Act - all
        var all = await _service.GetAllStopsAsync();
        Assert.Equal(2, all.Count());

        // Act - bbox only covering Center
        var inBbox = await _service.GetAllStopsAsync(minLat: 49.83, minLng: 24.02, maxLat: 49.85, maxLng: 24.04);
        var inBboxList = inBbox.ToList();
        Assert.Single(inBboxList);
        Assert.Equal("Центр", inBboxList[0].Name);
    }

    [Fact]
    public async Task CreateStopAsync_PersistsStop()
    {
        var dto = new CreateStopRequestDto
        {
            Name = "Театральна",
            Latitude = 49.843,
            Longitude = 24.029,
            OsmId = 1234567,
            TransportType = "tram"
        };

        var result = await _service.CreateStopAsync(dto);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Театральна", result.Name);
        Assert.Equal(1234567, result.OsmId);
        Assert.Equal("tram", result.TransportType);

        var fromDb = await _context.Stops.FindAsync(result.Id);
        Assert.NotNull(fromDb);
    }

    [Fact]
    public async Task CreateStopAsync_Deduplicates_ByOsmId()
    {
        var dto1 = new CreateStopRequestDto { Name = "Зупинка 1", Latitude = 49.84, Longitude = 24.03, OsmId = 99999 };
        var stop1 = await _service.CreateStopAsync(dto1);

        // Same OsmId, different name/coordinates slightly
        var dto2 = new CreateStopRequestDto { Name = "Зупинка 1 Оновлена", Latitude = 49.84005, Longitude = 24.03005, OsmId = 99999 };
        var stop2 = await _service.CreateStopAsync(dto2);

        // Should return the same stop and not create a new one
        Assert.Equal(stop1.Id, stop2.Id);
        Assert.Equal("Зупинка 1 Оновлена", stop2.Name);

        var total = await _context.Stops.CountAsync();
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task CreateStopAsync_Deduplicates_ByProximity()
    {
        // First stop
        await _service.CreateStopAsync(new CreateStopRequestDto { Name = "Головна", Latitude = 49.84200, Longitude = 24.03100 });

        // Second stop within ~10 meters (lat diff ~0.0001 is ~11 meters)
        var nearby = await _service.CreateStopAsync(new CreateStopRequestDto { Name = "Головна Поруч", Latitude = 49.84205, Longitude = 24.03105 });

        // Count in DB should still be 1
        var total = await _context.Stops.CountAsync();
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task BatchImportStopsAsync_ImportsNew_AndSkipsDuplicates()
    {
        // Existing stop in DB
        _context.Stops.Add(new Stop { Name = "Існуюча", Latitude = 49.840, Longitude = 24.030, OsmId = 1001 });
        await _context.SaveChangesAsync();

        var request = new BatchImportStopsRequestDto
        {
            Stops = new List<ImportStopItemDto>
            {
                // New unique stop
                new() { Name = "Нова 1", Latitude = 49.850, Longitude = 24.050, OsmId = 2001 },
                // Duplicate by OsmId
                new() { Name = "Дублікат Osm", Latitude = 49.900, Longitude = 24.100, OsmId = 1001 },
                // Duplicate by proximity (within ~5 meters of "Існуюча")
                new() { Name = "Дублікат Проксиміті", Latitude = 49.84002, Longitude = 24.03002, OsmId = 3001 },
                // In-batch duplicate of "Нова 1"
                new() { Name = "Нова 1 копія", Latitude = 49.850, Longitude = 24.050, OsmId = 2001 }
            }
        };

        var result = await _service.BatchImportStopsAsync(request);

        Assert.Equal(4, result.TotalReceived);
        Assert.Equal(1, result.ImportedCount); // Only "Нова 1"
        Assert.Equal(3, result.SkippedDuplicatesCount);

        var allStops = await _context.Stops.ToListAsync();
        Assert.Equal(2, allStops.Count); // "Існуюча" + "Нова 1"
    }

    [Fact]
    public async Task DeleteStopAsync_RemovesStop()
    {
        var stop = new Stop { Name = "To Delete", Latitude = 49.84, Longitude = 24.03 };
        _context.Stops.Add(stop);
        await _context.SaveChangesAsync();

        var deleted = await _service.DeleteStopAsync(stop.Id);

        Assert.True(deleted);
        var fromDb = await _context.Stops.FindAsync(stop.Id);
        Assert.Null(fromDb);
    }
}
