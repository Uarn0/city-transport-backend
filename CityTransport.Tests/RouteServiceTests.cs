using CityTransport.Data;
using CityTransport.DTOs.Routes;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.EntityFrameworkCore;
using Route = CityTransport.Entities.Route;

namespace CityTransport.Tests;

public class RouteServiceTests : IAsyncLifetime
{
    private DbContextOptions<AppDbContext> _options = null!;
    private AppDbContext _context = null!;
    private RouteService _service = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _context = new AppDbContext(_options);
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();

        // Clear pre-seeded routes from OnModelCreating so each test has clean isolation
        _context.Routes.RemoveRange(_context.Routes);
        await _context.SaveChangesAsync();

        _service = new RouteService(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.CloseConnectionAsync();
        await _context.DisposeAsync();
    }

    // ── Helper ────────────────────────────────────────────
    private async Task<Route> SeedRouteAsync(string number = "10", string name = "Центр - Вокзал",
        int duration = 20, List<Stop>? stops = null)
    {
        var route = new Route
        {
            Number = number,
            Name = name,
            EstimatedDurationMinutes = duration
        };

        var stopsToUse = stops ?? new List<Stop>
        {
            new() { Name = "Центр", Latitude = 49.842, Longitude = 24.031 },
            new() { Name = "Площа Ринок", Latitude = 49.844, Longitude = 24.032 },
            new() { Name = "Вокзал", Latitude = 49.838, Longitude = 24.025 }
        };

        var order = 1;
        foreach (var s in stopsToUse)
        {
            route.RouteStops.Add(new RouteStop
            {
                Route = route,
                Stop = s,
                Order = order++
            });
        }

        _context.Routes.Add(route);
        await _context.SaveChangesAsync();
        return route;
    }

    // ── GetAllRoutes ──────────────────────────────────────

    [Fact]
    public async Task GetAllRoutesAsync_ReturnsEmpty_WhenNoRoutes()
    {
        _context.Routes.RemoveRange(_context.Routes);
        await _context.SaveChangesAsync();

        var result = await _service.GetAllRoutesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllRoutesAsync_ReturnsAllRoutes_WithStops()
    {
        await SeedRouteAsync("11", "Центр - Сихів");
        await SeedRouteAsync("15", "Аеропорт - Центр");

        var result = (await _service.GetAllRoutesAsync()).ToList();

        var seeded = result.Where(r => r.Number == "11" || r.Number == "15").ToList();
        Assert.Equal(2, seeded.Count);
    }

    [Fact]
    public async Task GetAllRoutesAsync_ReturnsStopsInOrder()
    {
        var route = new Route
        {
            Number = "10",
            Name = "Центр - Вокзал",
            EstimatedDurationMinutes = 20
        };

        var stop3 = new Stop { Name = "Вокзал", Latitude = 49.838, Longitude = 24.025 };
        var stop1 = new Stop { Name = "Центр", Latitude = 49.842, Longitude = 24.031 };
        var stop2 = new Stop { Name = "Площа Ринок", Latitude = 49.844, Longitude = 24.032 };

        route.RouteStops.Add(new RouteStop { Route = route, Stop = stop3, Order = 3 });
        route.RouteStops.Add(new RouteStop { Route = route, Stop = stop1, Order = 1 });
        route.RouteStops.Add(new RouteStop { Route = route, Stop = stop2, Order = 2 });

        _context.Routes.Add(route);
        await _context.SaveChangesAsync();

        var result = (await _service.GetAllRoutesAsync()).ToList();
        var fetchedRoute = result.First(r => r.Number == "10");

        Assert.Equal(1, fetchedRoute.Stops[0].Order);
        Assert.Equal(2, fetchedRoute.Stops[1].Order);
        Assert.Equal(3, fetchedRoute.Stops[2].Order);
    }

    // ── GetRouteById ──────────────────────────────────────

    [Fact]
    public async Task GetRouteByIdAsync_ReturnsRoute_WhenExists()
    {
        var seeded = await SeedRouteAsync();

        var result = await _service.GetRouteByIdAsync(seeded.Id);

        Assert.NotNull(result);
        Assert.Equal(seeded.Id, result.Id);
        Assert.Equal("10", result.Number);
        Assert.Equal(3, result.Stops.Count);
    }

    [Fact]
    public async Task GetRouteByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetRouteByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── CreateRoute ───────────────────────────────────────

    [Fact]
    public async Task CreateRouteAsync_PersistsRouteWithStops()
    {
        var dto = new CreateRouteRequestDto
        {
            Number = "42",
            Name = "Лісовий - Центр",
            EstimatedDurationMinutes = 35,
            Stops = new List<StopRequestDto>
            {
                new() { Name = "Лісовий", Latitude = 49.85, Longitude = 24.05, Order = 1 },
                new() { Name = "Парк", Latitude = 49.847, Longitude = 24.04, Order = 2 },
                new() { Name = "Центр", Latitude = 49.842, Longitude = 24.031, Order = 3 }
            }
        };

        var result = await _service.CreateRouteAsync(dto);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("42", result.Number);
        Assert.Equal(3, result.Stops.Count);
        Assert.Equal(35, result.EstimatedDurationMinutes);

        // Verify persisted in DB
        var fromDb = await _context.Routes.Include(r => r.RouteStops).FirstOrDefaultAsync(r => r.Number == "42");
        Assert.NotNull(fromDb);
        Assert.Equal(3, fromDb!.RouteStops.Count);
    }

    [Fact]
    public async Task CreateRouteAsync_AssignsAutoOrder_WhenOrderIsZero()
    {
        var dto = new CreateRouteRequestDto
        {
            Number = "55",
            Name = "Test Route",
            EstimatedDurationMinutes = 10,
            Stops = new List<StopRequestDto>
            {
                new() { Name = "A", Latitude = 49.84, Longitude = 24.03, Order = 0 },
                new() { Name = "B", Latitude = 49.85, Longitude = 24.04, Order = 0 },
            }
        };

        var result = await _service.CreateRouteAsync(dto);

        Assert.Equal(1, result.Stops[0].Order);
        Assert.Equal(2, result.Stops[1].Order);
    }

    [Fact]
    public async Task CreateRouteAsync_CanReuseExistingStops_AcrossMultipleRoutes()
    {
        // 1. Create a physical stop in the catalog
        var sharedStop = new Stop { Name = "Головний вокзал", Latitude = 49.838, Longitude = 24.025 };
        var stopA = new Stop { Name = "Зупинка А", Latitude = 49.840, Longitude = 24.030 };
        var stopB = new Stop { Name = "Зупинка Б", Latitude = 49.850, Longitude = 24.040 };
        _context.Stops.AddRange(sharedStop, stopA, stopB);
        await _context.SaveChangesAsync();

        // 2. Route 1 uses sharedStop at Order 1
        var route1Dto = new CreateRouteRequestDto
        {
            Number = "1",
            Name = "Вокзал - Зупинка А",
            Stops = new List<StopRequestDto>
            {
                new() { StopId = sharedStop.Id, Name = sharedStop.Name, Latitude = sharedStop.Latitude, Longitude = sharedStop.Longitude, Order = 1 },
                new() { StopId = stopA.Id, Name = stopA.Name, Latitude = stopA.Latitude, Longitude = stopA.Longitude, Order = 2 }
            }
        };
        var route1 = await _service.CreateRouteAsync(route1Dto);

        // 3. Route 2 ALSO uses sharedStop at Order 2
        var route2Dto = new CreateRouteRequestDto
        {
            Number = "2",
            Name = "Зупинка Б - Вокзал",
            Stops = new List<StopRequestDto>
            {
                new() { StopId = stopB.Id, Name = stopB.Name, Latitude = stopB.Latitude, Longitude = stopB.Longitude, Order = 1 },
                new() { StopId = sharedStop.Id, Name = sharedStop.Name, Latitude = sharedStop.Latitude, Longitude = sharedStop.Longitude, Order = 2 }
            }
        };
        var route2 = await _service.CreateRouteAsync(route2Dto);

        // Assert: both routes successfully reference the same physical Stop
        Assert.Equal(sharedStop.Id, route1.Stops[0].Id);
        Assert.Equal(1, route1.Stops[0].Order);

        Assert.Equal(sharedStop.Id, route2.Stops[1].Id);
        Assert.Equal(2, route2.Stops[1].Order);

        // Verify physical stop was NOT duplicated
        var allStopsWithThisName = await _context.Stops.Where(s => s.Name == "Головний вокзал").ToListAsync();
        Assert.Single(allStopsWithThisName);
    }

    // ── UpdateRouteStops ──────────────────────────────────

    [Fact]
    public async Task UpdateRouteStopsAsync_ReplacesAllStops()
    {
        var seeded = await SeedRouteAsync();

        var updateDto = new UpdateRouteStopsDto
        {
            EstimatedDurationMinutes = 45,
            Stops = new List<StopRequestDto>
            {
                new() { Name = "Нова зупинка A", Latitude = 49.84, Longitude = 24.03, Order = 1 },
                new() { Name = "Нова зупинка B", Latitude = 49.85, Longitude = 24.04, Order = 2 }
            }
        };

        var result = await _service.UpdateRouteStopsAsync(seeded.Id, updateDto);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Stops.Count);
        Assert.Equal(45, result.EstimatedDurationMinutes);
        Assert.Contains(result.Stops, s => s.Name == "Нова зупинка A");

        // Old route stops should be replaced with 2
        var stopsInDb = await _context.RouteStops.Where(s => s.RouteId == seeded.Id).ToListAsync();
        Assert.Equal(2, stopsInDb.Count);
    }

    [Fact]
    public async Task UpdateRouteStopsAsync_ReturnsNull_WhenRouteNotFound()
    {
        var result = await _service.UpdateRouteStopsAsync(Guid.NewGuid(), new UpdateRouteStopsDto());

        Assert.Null(result);
    }

    // ── DeleteRoute ───────────────────────────────────────

    [Fact]
    public async Task DeleteRouteAsync_RemovesRoute_ReturnsTrue()
    {
        var seeded = await SeedRouteAsync();

        var result = await _service.DeleteRouteAsync(seeded.Id);

        Assert.True(result);
        var fromDb = await _context.Routes.FindAsync(seeded.Id);
        Assert.Null(fromDb);
    }

    [Fact]
    public async Task DeleteRouteAsync_ReturnsFalse_WhenNotFound()
    {
        var result = await _service.DeleteRouteAsync(Guid.NewGuid());

        Assert.False(result);
    }

    // ── FindRoutes (Haversine) ────────────────────────────

    [Fact]
    public async Task FindRoutesAsync_ReturnsMatchingRoutes()
    {
        // Route 10: Центр (order=1) → Вокзал (order=2) in Lviv
        await SeedRouteAsync("10", "Центр - Вокзал", 20, new List<Stop>
        {
            new() { Name = "Центр", Latitude = 49.842, Longitude = 24.031 },
            new() { Name = "Вокзал", Latitude = 49.838, Longitude = 24.025 }
        });

        // Search from near Центр to near Вокзал
        var results = (await _service.FindRoutesAsync(49.843, 24.030, 49.837, 24.026)).ToList();

        // Should find route 10 (from stop order < to stop order)
        Assert.NotEmpty(results);
        var found = results.FirstOrDefault(r => r.RouteNumber == "10");
        Assert.NotNull(found);
    }

    [Fact]
    public async Task FindRoutesAsync_ExcludesRoutes_WhenDirectionWrong()
    {
        // Route with 2 stops
        await SeedRouteAsync("10", "Центр - Вокзал", 20, new List<Stop>
        {
            new() { Name = "Центр", Latitude = 49.842, Longitude = 24.031 },
            new() { Name = "Вокзал", Latitude = 49.838, Longitude = 24.025 }
        });

        // Search in REVERSE direction (from near Вокзал to near Центр)
        // The nearest "from" stop will be Вокзал (order=2) and nearest "to" will be Центр (order=1)
        // 2 >= 1 so route should be excluded
        var results = (await _service.FindRoutesAsync(49.837, 24.026, 49.843, 24.030)).ToList();
        var route10 = results.FirstOrDefault(r => r.RouteNumber == "10");

        Assert.Null(route10);
    }

    [Fact]
    public async Task FindRoutesAsync_ReturnsEmpty_WhenNoRoutes()
    {
        _context.Routes.RemoveRange(_context.Routes);
        await _context.SaveChangesAsync();

        var results = await _service.FindRoutesAsync(49.842, 24.031, 49.838, 24.025);

        Assert.Empty(results);
    }

    // ── DTO mapping correctness ───────────────────────────

    [Fact]
    public async Task CreateRouteAsync_MapsCoordinatesCorrectly()
    {
        var dto = new CreateRouteRequestDto
        {
            Number = "77",
            Name = "Test",
            EstimatedDurationMinutes = 15,
            Stops = new List<StopRequestDto>
            {
                new() { Name = "StopA", Latitude = 49.1234, Longitude = 24.5678, Order = 1 }
            }
        };

        var result = await _service.CreateRouteAsync(dto);

        Assert.Equal(49.1234, result.Stops[0].Latitude, 4);
        Assert.Equal(24.5678, result.Stops[0].Longitude, 4);
        Assert.Equal("StopA", result.Stops[0].Name);
    }
}
