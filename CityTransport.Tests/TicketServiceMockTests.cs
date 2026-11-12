using CityTransport.Data;
using CityTransport.Entities;
using CityTransport.Services;
using CityTransport.Utils.TicketUtils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CityTransport.Tests;

public class TicketServiceMockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testTariffId = Guid.NewGuid();

    public TicketServiceMockTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _dbContext.Users.Add(new User
        {
            Id = _testUserId,
            FullName = "Олександр Пасажир",
            Email = "alex.passenger@example.com"
        });

        _dbContext.Tariffs.Add(new Tariff
        {
            Id = _testTariffId,
            Name = "Стандартний разовий",
            Price = 15.00m,
            DurationMinutes = 60,
            IsActive = true
        });

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task VerifyAndUseTicketAsync_WhenEncryptedPayloadIsValid_CallsTryDecryptPayloadOnceAndMarksTicketUsed()
    {
        // Arrange
        // --- MOCK 3: Mocking IQRCodeGeneratorService.TryDecryptPayload with out parameters ---
        var mockQrGenerator = new Mock<IQRCodeGeneratorService>();
        var stubLogger = new Mock<ILogger<TicketService>>(); // Stub logger

        var ticketId = Guid.NewGuid();
        var encryptedInput = "AES256_VALID_ENCRYPTED_STRING";
        var outTicketGuid = ticketId.ToString();
        var outGeneratedAt = DateTime.UtcNow.AddMinutes(-5);

        mockQrGenerator
            .Setup(q => q.TryDecryptPayload(encryptedInput, out outTicketGuid, out outGeneratedAt))
            .Returns(true);

        var route = new Route
        {
            Id = Guid.NewGuid(),
            Number = "3A",
            Name = "Центр - Сихів"
        };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            TariffId = _testTariffId,
            RouteId = route.Id,
            TotalAmount = 15.00m,
            Status = OrderStatus.Paid
        };
        var ticket = new Ticket
        {
            Id = ticketId,
            UserId = _testUserId,
            OrderId = order.Id,
            QrCodePayload = encryptedInput,
            Status = TicketStatus.Active,
            PurchasedAt = DateTime.UtcNow.AddMinutes(-5),
            ValidUntil = DateTime.UtcNow.AddMinutes(55)
        };

        _dbContext.Routes.Add(route);
        _dbContext.Orders.Add(order);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var service = new TicketService(_dbContext, mockQrGenerator.Object, stubLogger.Object);

        // Act
        var result = await service.VerifyAndUseTicketAsync(encryptedInput);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal("Квиток успішно перевірено та погашено!", result.Message);
        Assert.Equal(ticketId, result.TicketId);
        Assert.Equal("Used", result.Status);

        var updatedTicket = await _dbContext.Tickets.FindAsync(ticketId);
        Assert.NotNull(updatedTicket);
        Assert.Equal(TicketStatus.Used, updatedTicket.Status);

        // Verify Mock invocation: TryDecryptPayload was invoked exactly once with the encrypted payload
        mockQrGenerator.Verify(
            q => q.TryDecryptPayload(encryptedInput, out It.Ref<string?>.IsAny, out It.Ref<DateTime>.IsAny),
            Times.Once());
    }

    [Fact]
    public async Task VerifyAndUseTicketAsync_WhenTryDecryptPayloadFails_ReturnsInvalidWithoutModifyingDatabase()
    {
        // Arrange
        var mockQrGenerator = new Mock<IQRCodeGeneratorService>();
        var stubLogger = new Mock<ILogger<TicketService>>();

        string? failedGuid = null;
        DateTime failedDate = default;
        var fakeCipher = "INVALID_CORRUPTED_CIPHERTEXT";

        mockQrGenerator
            .Setup(q => q.TryDecryptPayload(fakeCipher, out failedGuid, out failedDate))
            .Returns(false);

        var service = new TicketService(_dbContext, mockQrGenerator.Object, stubLogger.Object);

        // Act
        var result = await service.VerifyAndUseTicketAsync(fakeCipher);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("помилка криптографічної перевірки", result.Message);

        // Verify Mock invocation
        mockQrGenerator.Verify(
            q => q.TryDecryptPayload(fakeCipher, out It.Ref<string?>.IsAny, out It.Ref<DateTime>.IsAny),
            Times.Once());
    }

    [Fact]
    public async Task VerifyAndUseTicketAsync_WhenTicketIsAlreadyUsed_ReturnsInvalidWithUsedStatus()
    {
        // Arrange
        var mockQrGenerator = new Mock<IQRCodeGeneratorService>();
        var stubLogger = new Mock<ILogger<TicketService>>();

        var ticketId = Guid.NewGuid();
        var cipher = "USED_TICKET_CIPHER";
        var outTicketGuid = ticketId.ToString();
        var outGeneratedAt = DateTime.UtcNow.AddMinutes(-30);

        mockQrGenerator
            .Setup(q => q.TryDecryptPayload(cipher, out outTicketGuid, out outGeneratedAt))
            .Returns(true);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            TariffId = _testTariffId,
            TotalAmount = 15.00m,
            Status = OrderStatus.Paid
        };
        var ticket = new Ticket
        {
            Id = ticketId,
            UserId = _testUserId,
            OrderId = order.Id,
            QrCodePayload = cipher,
            Status = TicketStatus.Used, // Already used!
            PurchasedAt = DateTime.UtcNow.AddMinutes(-30),
            ValidUntil = DateTime.UtcNow.AddMinutes(30)
        };

        _dbContext.Orders.Add(order);
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var service = new TicketService(_dbContext, mockQrGenerator.Object, stubLogger.Object);

        // Act
        var result = await service.VerifyAndUseTicketAsync(cipher);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal("Цей квиток вже був використаний раніше!", result.Message);
        Assert.Equal("Used", result.Status);

        mockQrGenerator.Verify(
            q => q.TryDecryptPayload(cipher, out It.Ref<string?>.IsAny, out It.Ref<DateTime>.IsAny),
            Times.Once());
    }
}
