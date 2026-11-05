using CityTransport.Data;
using CityTransport.DTOs.Purchases;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CityTransport.Tests;

public class PurchaseServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly PurchaseService _purchaseService;
    private readonly Guid _testUserId = Guid.NewGuid();

    public PurchaseServiceTests()
    {
        // Set up SQLite In-Memory connection for isolated unit testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Seed test user to satisfy Foreign Key constraints for Order.UserId
        _dbContext.Users.Add(new User
        {
            Id = _testUserId,
            FullName = "Тестовий Користувач",
            Email = "test@example.com"
        });
        _dbContext.SaveChanges();

        _purchaseService = new PurchaseService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(1, 15.00, 15.00)]
    [InlineData(2, 15.00, 30.00)]
    [InlineData(3, 7.50, 22.50)]
    [InlineData(5, 7.50, 37.50)]
    [InlineData(10, 15.00, 150.00)]
    public async Task CreatePurchaseAsync_WithVariousQuantitiesAndPrices_CalculatesTotalAmountCorrectly(
        int quantity, 
        decimal price, 
        decimal expectedTotal)
    {
        // Arrange
        var tariff = new Tariff
        {
            Id = Guid.NewGuid(),
            Name = $"Tariff {price} UAH",
            Price = price,
            DurationMinutes = 60,
            IsActive = true
        };
        _dbContext.Tariffs.Add(tariff);
        await _dbContext.SaveChangesAsync();

        var request = new CreatePurchaseRequestDto
        {
            TariffId = tariff.Id,
            Quantity = quantity,
            PaymentProvider = "Monobank"
        };

        // Act
        var result = await _purchaseService.CreatePurchaseAsync(request, _testUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedTotal, result.TotalAmount);
        Assert.Equal(OrderStatus.Pending.ToString(), result.Status);
        Assert.Contains(result.OrderId.ToString(), result.PaymentUrl);

        var savedOrder = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == result.OrderId);
        Assert.NotNull(savedOrder);
        Assert.Equal(expectedTotal, savedOrder.TotalAmount);
        Assert.Equal(quantity, savedOrder.Quantity);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenTariffNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentTariffId = Guid.NewGuid();
        var request = new CreatePurchaseRequestDto
        {
            TariffId = nonExistentTariffId,
            Quantity = 1,
            PaymentProvider = "Monobank"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _purchaseService.CreatePurchaseAsync(request, _testUserId));

        Assert.Contains(nonExistentTariffId.ToString(), exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenTariffIsInactive_ThrowsInvalidOperationException()
    {
        // Arrange
        var inactiveTariff = new Tariff
        {
            Id = Guid.NewGuid(),
            Name = "Старий архівний тариф",
            Price = 10.00m,
            DurationMinutes = 60,
            IsActive = false
        };
        _dbContext.Tariffs.Add(inactiveTariff);
        await _dbContext.SaveChangesAsync();

        var request = new CreatePurchaseRequestDto
        {
            TariffId = inactiveTariff.Id,
            Quantity = 1,
            PaymentProvider = "Monobank"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _purchaseService.CreatePurchaseAsync(request, _testUserId));

        Assert.Contains("неактивний", exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenRouteNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var tariff = new Tariff
        {
            Id = Guid.NewGuid(),
            Name = "Загальний разовий",
            Price = 15.00m,
            IsActive = true
        };
        _dbContext.Tariffs.Add(tariff);
        await _dbContext.SaveChangesAsync();

        var nonExistentRouteId = Guid.NewGuid();
        var request = new CreatePurchaseRequestDto
        {
            TariffId = tariff.Id,
            RouteId = nonExistentRouteId,
            Quantity = 1,
            PaymentProvider = "Monobank"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _purchaseService.CreatePurchaseAsync(request, _testUserId));

        Assert.Contains("не знайдено", exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenRouteNotAllowedForTariff_ThrowsInvalidOperationException()
    {
        // Arrange
        var allowedRoute = new Route { Id = Guid.NewGuid(), Number = "10", Name = "Центр - Вокзал" };
        var prohibitedRoute = new Route { Id = Guid.NewGuid(), Number = "99", Name = "Аеропорт - Центр" };

        var restrictedTariff = new Tariff
        {
            Id = Guid.NewGuid(),
            Name = "Маршрутний експрес",
            Price = 20.00m,
            IsActive = true,
            Routes = new List<Route> { allowedRoute }
        };

        _dbContext.Routes.AddRange(allowedRoute, prohibitedRoute);
        _dbContext.Tariffs.Add(restrictedTariff);
        await _dbContext.SaveChangesAsync();

        var request = new CreatePurchaseRequestDto
        {
            TariffId = restrictedTariff.Id,
            RouteId = prohibitedRoute.Id,
            Quantity = 1,
            PaymentProvider = "Monobank"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _purchaseService.CreatePurchaseAsync(request, _testUserId));

        Assert.Contains("не діє на обраний маршрут", exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenRouteIsAllowedForTariff_CreatesOrderSuccessfully()
    {
        // Arrange
        var validRoute = new Route { Id = Guid.NewGuid(), Number = "15", Name = "Лісовий - Центр" };
        var tariff = new Tariff
        {
            Id = Guid.NewGuid(),
            Name = "Спеціальний міський",
            Price = 12.00m,
            IsActive = true,
            Routes = new List<Route> { validRoute }
        };

        _dbContext.Routes.Add(validRoute);
        _dbContext.Tariffs.Add(tariff);
        await _dbContext.SaveChangesAsync();

        var request = new CreatePurchaseRequestDto
        {
            TariffId = tariff.Id,
            RouteId = validRoute.Id,
            Quantity = 2,
            PaymentProvider = "Monobank"
        };

        // Act
        var result = await _purchaseService.CreatePurchaseAsync(request, _testUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(24.00m, result.TotalAmount);
        Assert.Equal(OrderStatus.Pending.ToString(), result.Status);

        var savedOrder = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == result.OrderId);
        Assert.NotNull(savedOrder);
        Assert.Equal(validRoute.Id, savedOrder.RouteId);
    }
}
