using CityTransport.Data;
using CityTransport.DTOs.Payments;
using CityTransport.Entities;
using CityTransport.Services;
using CityTransport.Utils.TicketUtils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CityTransport.Tests;

public class PaymentServiceMockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testTariffId = Guid.NewGuid();

    public PaymentServiceMockTests()
    {
        // Isolated SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Seed User and Tariff to satisfy foreign key constraints
        _dbContext.Users.Add(new User
        {
            Id = _testUserId,
            FullName = "Марія Тестова",
            Email = "maria.test@example.com"
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
    public async Task ProcessMonobankWebhookAsync_WhenPaymentSuccess_GeneratesEncryptedPayloadOnceForTicket()
    {
        // Arrange
        // --- MOCK 2: Verifying that GenerateEncryptedPayload is invoked with actual TicketId ---
        var mockQrGenerator = new Mock<IQRCodeGeneratorService>();
        var stubLogger = new Mock<ILogger<PaymentService>>(); // Stub logger (no verify asserted)

        var expectedEncryptedCipher = "CIPHER_AES256_MOCK_PAYLOAD_TOKEN";
        mockQrGenerator
            .Setup(q => q.GenerateEncryptedPayload(It.IsAny<string>()))
            .Returns(expectedEncryptedCipher);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            TariffId = _testTariffId,
            Quantity = 1,
            TotalAmount = 15.00m,
            Status = OrderStatus.Pending
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var service = new PaymentService(_dbContext, stubLogger.Object, mockQrGenerator.Object);

        var webhookDto = new MonobankWebhookDto
        {
            InvoiceId = "inv_123456",
            Status = "success",
            Amount = 1500,
            Reference = order.Id.ToString()
        };

        // Act
        var result = await service.ProcessMonobankWebhookAsync(webhookDto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.TicketId);

        var createdTicket = await _dbContext.Tickets.FirstOrDefaultAsync(t => t.OrderId == order.Id);
        Assert.NotNull(createdTicket);
        Assert.Equal(TicketStatus.Active, createdTicket.Status);
        Assert.Equal(expectedEncryptedCipher, createdTicket.QrCodePayload);

        // Verify Mock invocation: payload generation MUST be called exactly once with the generated Ticket GUID
        mockQrGenerator.Verify(
            q => q.GenerateEncryptedPayload(It.Is<string>(guidStr => guidStr == createdTicket.Id.ToString())),
            Times.Once());
    }

    [Fact]
    public async Task ProcessMonobankWebhookAsync_WhenPaymentFails_CancelsOrderAndNeverGeneratesQrCode()
    {
        // Arrange
        var mockQrGenerator = new Mock<IQRCodeGeneratorService>();
        var stubLogger = new Mock<ILogger<PaymentService>>();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            TariffId = _testTariffId,
            TotalAmount = 15.00m,
            Status = OrderStatus.Pending
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var service = new PaymentService(_dbContext, stubLogger.Object, mockQrGenerator.Object);

        var webhookDto = new MonobankWebhookDto
        {
            InvoiceId = "inv_failed_001",
            Status = "failure",
            Amount = 1500,
            Reference = order.Id.ToString()
        };

        // Act
        var result = await service.ProcessMonobankWebhookAsync(webhookDto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);

        var updatedOrder = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Cancelled, updatedOrder.Status);

        // Verify Mock behavior: GenerateEncryptedPayload MUST NEVER be invoked if payment failed
        mockQrGenerator.Verify(
            q => q.GenerateEncryptedPayload(It.IsAny<string>()),
            Times.Never());
    }

    [Fact]
    public async Task ProcessMonobankWebhookAsync_WhenOrderAlreadyPaid_SkipsDuplicateProcessingAndNeverGeneratesQrCode()
    {
        // Arrange
        var mockQrGenerator = new Mock<IQRCodeGeneratorService>();
        var stubLogger = new Mock<ILogger<PaymentService>>();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            TariffId = _testTariffId,
            TotalAmount = 15.00m,
            Status = OrderStatus.Paid // Already processed
        };
        var existingTicket = new Ticket
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            UserId = _testUserId,
            QrCodePayload = "ALREADY_EXISTING_CIPHER",
            Status = TicketStatus.Active,
            ValidUntil = DateTime.UtcNow.AddMinutes(60)
        };
        _dbContext.Orders.Add(order);
        _dbContext.Tickets.Add(existingTicket);
        await _dbContext.SaveChangesAsync();

        var service = new PaymentService(_dbContext, stubLogger.Object, mockQrGenerator.Object);

        var webhookDto = new MonobankWebhookDto
        {
            InvoiceId = "inv_dup_001",
            Status = "success",
            Amount = 1500,
            Reference = order.Id.ToString()
        };

        // Act
        var result = await service.ProcessMonobankWebhookAsync(webhookDto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Order already processed.", result.Message);
        Assert.Equal(existingTicket.Id, result.TicketId);

        // Verify Mock behavior: Idempotency check prevents duplicate QR code generation
        mockQrGenerator.Verify(
            q => q.GenerateEncryptedPayload(It.IsAny<string>()),
            Times.Never());
    }

    [Fact]
    public async Task ProcessMonobankWebhookAsync_WithStubQrGenerator_ReturnsSuccessAndStubbedPayload()
    {
        // Arrange
        // --- STUB 2: Predefined canned return value to support service flow without call verification ---
        var stubQrGenerator = new Mock<IQRCodeGeneratorService>();
        var cannedPayload = "STUBBED_AES256_FIXED_PAYLOAD_ABC";
        stubQrGenerator
            .Setup(q => q.GenerateEncryptedPayload(It.IsAny<string>()))
            .Returns(cannedPayload);

        var stubLogger = new Mock<ILogger<PaymentService>>();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            TariffId = _testTariffId,
            TotalAmount = 15.00m,
            Status = OrderStatus.Pending
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var service = new PaymentService(_dbContext, stubLogger.Object, stubQrGenerator.Object);

        var webhookDto = new MonobankWebhookDto
        {
            InvoiceId = "inv_stub_001",
            Status = "success",
            Amount = 1500,
            Reference = order.Id.ToString()
        };

        // Act
        var result = await service.ProcessMonobankWebhookAsync(webhookDto);

        // Assert state only (no Verify called on stubQrGenerator)
        Assert.NotNull(result);
        Assert.True(result.Success);

        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(t => t.OrderId == order.Id);
        Assert.NotNull(ticket);
        Assert.Equal(cannedPayload, ticket.QrCodePayload);
    }
}
