using CityTransport.Data;
using CityTransport.DTOs.Purchases;
using CityTransport.Entities;
using CityTransport.Services;
using CityTransport.Services.Payments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CityTransport.Tests;

public class PurchaseServiceMockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly Guid _testUserId = Guid.NewGuid();

    public PurchaseServiceMockTests()
    {
        // Isolated SQLite In-Memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Seed user to satisfy foreign key requirement
        _dbContext.Users.Add(new User
        {
            Id = _testUserId,
            FullName = "Іван Тестовий",
            Email = "ivan.test@example.com"
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenValidRequest_CallsPaymentGatewayWithCorrectOrderDetails()
    {
        // Arrange
        // --- MOCK 1: Behaviour Verification via Verify and Times.Once() ---
        var mockPaymentGateway = new Mock<IPaymentGateway>();
        var expectedCheckoutUrl = "https://checkout.monobank.ua/pay/unique-session-id-123";
        mockPaymentGateway
            .Setup(g => g.CreatePaymentSessionAsync(It.IsAny<Order>()))
            .ReturnsAsync(expectedCheckoutUrl);

        var tariff = new Tariff
        {
            Id = Guid.NewGuid(),
            Name = "Стандартний разовий",
            Price = 15.00m,
            DurationMinutes = 60,
            IsActive = true
        };
        _dbContext.Tariffs.Add(tariff);
        await _dbContext.SaveChangesAsync();

        var service = new PurchaseService(_dbContext, mockPaymentGateway.Object);

        var request = new CreatePurchaseRequestDto
        {
            TariffId = tariff.Id,
            Quantity = 2,
            PaymentProvider = "Monobank"
        };

        // Act
        var result = await service.CreatePurchaseAsync(request, _testUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCheckoutUrl, result.PaymentUrl);
        Assert.Equal(30.00m, result.TotalAmount);

        // Verifying behavior: CreatePaymentSessionAsync MUST be called exactly once with matching Order properties
        mockPaymentGateway.Verify(
            g => g.CreatePaymentSessionAsync(It.Is<Order>(o =>
                o.Id == result.OrderId &&
                o.UserId == _testUserId &&
                o.TotalAmount == 30.00m &&
                o.Quantity == 2 &&
                o.Status == OrderStatus.Pending)),
            Times.Once());
    }

    [Fact]
    public async Task CreatePurchaseAsync_WhenTariffNotFound_NeverCallsPaymentGateway()
    {
        // Arrange
        var mockPaymentGateway = new Mock<IPaymentGateway>();
        var service = new PurchaseService(_dbContext, mockPaymentGateway.Object);

        var request = new CreatePurchaseRequestDto
        {
            TariffId = Guid.NewGuid(), // Non-existent tariff
            Quantity = 1,
            PaymentProvider = "Monobank"
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreatePurchaseAsync(request, _testUserId));

        // Verifying behavior: Payment Gateway should NEVER be called when validation fails
        mockPaymentGateway.Verify(
            g => g.CreatePaymentSessionAsync(It.IsAny<Order>()),
            Times.Never());
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithStubPaymentGateway_ReturnsPredefinedStubUrl()
    {
        // Arrange
        // --- STUB 1: Provides fixed canned data without verifying invocation ---
        var stubPaymentGateway = new Mock<IPaymentGateway>();
        var stubUrl = "https://stub.payment.local/canned-checkout-url";
        stubPaymentGateway
            .Setup(g => g.CreatePaymentSessionAsync(It.IsAny<Order>()))
            .ReturnsAsync(stubUrl);

        var tariff = new Tariff
        {
            Id = Guid.NewGuid(),
            Name = "Пільговий квиток",
            Price = 7.50m,
            DurationMinutes = 60,
            IsActive = true
        };
        _dbContext.Tariffs.Add(tariff);
        await _dbContext.SaveChangesAsync();

        var service = new PurchaseService(_dbContext, stubPaymentGateway.Object);

        var request = new CreatePurchaseRequestDto
        {
            TariffId = tariff.Id,
            Quantity = 1,
            PaymentProvider = "Monobank"
        };

        // Act
        var result = await service.CreatePurchaseAsync(request, _testUserId);

        // Assert - purely state assertion on result and database (no mock verification)
        Assert.NotNull(result);
        Assert.Equal(stubUrl, result.PaymentUrl);
        Assert.Equal(7.50m, result.TotalAmount);
        Assert.Equal(OrderStatus.Pending.ToString(), result.Status);
    }
}
