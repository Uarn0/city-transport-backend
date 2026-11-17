using CityTransport.Data;
using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CityTransport.Tests;

public class TransactionRepositoryIntegrationTests : IAsyncLifetime
{
    private DbContextOptions<AppDbContext> _options = null!;
    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AppDbContext(_options);
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.Database.CloseConnectionAsync();
            await _context.DisposeAsync();
        }
    }

    #region CRUD Tests

    [Fact]
    public async Task AddTransaction_ShouldPersistToDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        var tariffId = Guid.NewGuid();
        var tariff = new Tariff
        {
            Id = tariffId,
            Name = "Test Tariff",
            Price = 20.00m,
            DurationMinutes = 60,
            IsActive = true
        };

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TariffId = tariffId,
            Quantity = 1,
            TotalAmount = 20.00m,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentProvider = "Monobank",
            ExternalTransactionId = "ext-123",
            Amount = 20.00m,
            Status = "Success",
            ProcessedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.Tariffs.Add(tariff);
        _context.Orders.Add(order);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(transaction.Id, result.Id);
        Assert.Equal("Monobank", result.PaymentProvider);
        Assert.Equal(20.00m, result.Amount);
        Assert.Equal("Success", result.Status);
    }

    [Fact]
    public async Task EditTransaction_ShouldUpdateExistingRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        var tariffId = Guid.NewGuid();
        var tariff = new Tariff
        {
            Id = tariffId,
            Name = "Test Tariff",
            Price = 20.00m,
            DurationMinutes = 60,
            IsActive = true
        };

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TariffId = tariffId,
            Quantity = 1,
            TotalAmount = 20.00m,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentProvider = "Monobank",
            ExternalTransactionId = "ext-123",
            Amount = 20.00m,
            Status = "Pending",
            ProcessedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.Tariffs.Add(tariff);
        _context.Orders.Add(order);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Act
        var transactionToUpdate = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id);

        Assert.NotNull(transactionToUpdate);
        transactionToUpdate.Status = "Success";
        transactionToUpdate.Amount = 25.00m;

        _context.Transactions.Update(transactionToUpdate);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id);

        Assert.NotNull(updated);
        Assert.Equal("Success", updated.Status);
        Assert.Equal(25.00m, updated.Amount);
    }

    [Fact]
    public async Task DeleteTransaction_ShouldRemoveFromDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        var tariffId = Guid.NewGuid();
        var tariff = new Tariff
        {
            Id = tariffId,
            Name = "Test Tariff",
            Price = 20.00m,
            DurationMinutes = 60,
            IsActive = true
        };

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TariffId = tariffId,
            Quantity = 1,
            TotalAmount = 20.00m,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentProvider = "Monobank",
            ExternalTransactionId = "ext-123",
            Amount = 20.00m,
            Status = "Success",
            ProcessedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.Tariffs.Add(tariff);
        _context.Orders.Add(order);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Act
        var transactionToDelete = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id);

        Assert.NotNull(transactionToDelete);
        _context.Transactions.Remove(transactionToDelete);
        await _context.SaveChangesAsync();

        // Assert
        var deleted = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id);

        Assert.Null(deleted);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task QueryTransactionsByPeriod_ShouldFilterAndSortByDate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        var tariffId = Guid.NewGuid();
        var tariff = new Tariff
        {
            Id = tariffId,
            Name = "Test Tariff",
            Price = 20.00m,
            DurationMinutes = 60,
            IsActive = true
        };

        var now = DateTime.UtcNow;
        var pastDate = now.AddDays(-10);
        var futureDate = now.AddDays(10);

        var orders = new List<Order>();
        var transactions = new List<Transaction>();

        for (int i = 0; i < 3; i++)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TariffId = tariffId,
                Quantity = 1,
                TotalAmount = 20.00m * (i + 1),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            orders.Add(order);

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PaymentProvider = "Monobank",
                ExternalTransactionId = $"ext-{i}",
                Amount = 20.00m * (i + 1),
                Status = "Success",
                ProcessedAt = i == 0 ? pastDate : (i == 1 ? now : futureDate)
            };
            transactions.Add(transaction);
        }

        _context.Users.Add(user);
        _context.Tariffs.Add(tariff);
        _context.Orders.AddRange(orders);
        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();

        // Act - Query transactions from 5 days ago to today
        var startDate = now.AddDays(-5);
        var endDate = now.AddHours(1);

        var result = await _context.Transactions
            .Where(t => t.ProcessedAt >= startDate && t.ProcessedAt <= endDate)
            .OrderBy(t => t.ProcessedAt)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(1, result.Count);
        Assert.Equal("ext-1", result[0].ExternalTransactionId);

        // Verify sorting
        Assert.True(result[0].ProcessedAt <= result[result.Count - 1].ProcessedAt);
    }

    [Fact]
    public async Task QueryTransactionsByPeriodWithMultipleRecords_ShouldSortCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        var tariffId = Guid.NewGuid();
        var tariff = new Tariff
        {
            Id = tariffId,
            Name = "Test Tariff",
            Price = 20.00m,
            DurationMinutes = 60,
            IsActive = true
        };

        var now = DateTime.UtcNow;
        var dates = new[]
        {
            now.AddDays(-2),
            now.AddDays(-1),
            now,
            now.AddDays(1)
        };

        var orders = new List<Order>();
        var transactions = new List<Transaction>();

        for (int i = 0; i < dates.Length; i++)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TariffId = tariffId,
                Quantity = 1,
                TotalAmount = 20.00m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            orders.Add(order);

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PaymentProvider = "Monobank",
                ExternalTransactionId = $"ext-{i}",
                Amount = 20.00m,
                Status = "Success",
                ProcessedAt = dates[i]
            };
            transactions.Add(transaction);
        }

        _context.Users.Add(user);
        _context.Tariffs.Add(tariff);
        _context.Orders.AddRange(orders);
        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();

        // Act - Query transactions from 3 days ago to 2 days in future
        var startDate = now.AddDays(-3);
        var endDate = now.AddDays(2);

        var result = await _context.Transactions
            .Where(t => t.ProcessedAt >= startDate && t.ProcessedAt <= endDate)
            .OrderBy(t => t.ProcessedAt)
            .ToListAsync();

        // Assert
        Assert.Equal(4, result.Count);

        // Verify sorting is ascending
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].ProcessedAt <= result[i + 1].ProcessedAt,
                $"Transaction {i} should be before transaction {i + 1}");
        }

        // Verify order of ExternalTransactionIds
        Assert.Equal("ext-0", result[0].ExternalTransactionId);
        Assert.Equal("ext-1", result[1].ExternalTransactionId);
        Assert.Equal("ext-2", result[2].ExternalTransactionId);
        Assert.Equal("ext-3", result[3].ExternalTransactionId);
    }

    #endregion

    #region End-to-End Tests

    [Fact]
    public async Task EndToEnd_CreateOrderAddTransactionAndVerifyBalance()
    {
        // Arrange - Create complete business flow
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "user@transport.com",
            FullName = "John Doe",
            CreatedAt = DateTime.UtcNow
        };

        var tariffId = Guid.NewGuid();
        var tariff = new Tariff
        {
            Id = tariffId,
            Name = "Standard Ticket",
            Price = 15.00m,
            DurationMinutes = 60,
            IsActive = true
        };

        _context.Users.Add(user);
        _context.Tariffs.Add(tariff);
        await _context.SaveChangesAsync();

        // Act - Business Logic: Create Order for User
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            TariffId = tariffId,
            Quantity = 2,
            TotalAmount = 30.00m, // 2 tickets * 15.00
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act - Repository: Add Transaction for the Order
        var transactionId = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = transactionId,
            OrderId = orderId,
            PaymentProvider = "Monobank",
            ExternalTransactionId = "mono-12345",
            Amount = 30.00m,
            Status = "Success",
            ProcessedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        _context.Orders.FirstAsync(o => o.Id == orderId).Result.Status = OrderStatus.Paid;
        await _context.SaveChangesAsync();

        // Assert - Verify complete flow
        var savedOrder = await _context.Orders
            .Include(o => o.Transaction)
            .Include(o => o.User)
            .Include(o => o.Tariff)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        Assert.NotNull(savedOrder);
        Assert.Equal(userId, savedOrder.UserId);
        Assert.Equal(OrderStatus.Paid, savedOrder.Status);
        Assert.Equal(30.00m, savedOrder.TotalAmount);
        Assert.Equal(2, savedOrder.Quantity);

        var savedTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        Assert.NotNull(savedTransaction);
        Assert.Equal(orderId, savedTransaction.OrderId);
        Assert.Equal(30.00m, savedTransaction.Amount);
        Assert.Equal("Success", savedTransaction.Status);

        // Verify the relationship
        Assert.Equal(savedOrder.Id, savedTransaction.OrderId);
        Assert.Equal(savedOrder.TotalAmount, savedTransaction.Amount);
    }

    [Fact]
    public async Task EndToEnd_AddAndRemoveTransactionAffectsOrderBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "user2@transport.com",
            FullName = "Jane Smith",
            CreatedAt = DateTime.UtcNow
        };

        var tariffId = Guid.NewGuid();
        var tariff = new Tariff
        {
            Id = tariffId,
            Name = "Premium Ticket",
            Price = 25.00m,
            DurationMinutes = 120,
            IsActive = true
        };

        _context.Users.Add(user);
        _context.Tariffs.Add(tariff);
        await _context.SaveChangesAsync();

        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            TariffId = tariffId,
            Quantity = 1,
            TotalAmount = 25.00m,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act 1 - Add Transaction
        var transactionId = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = transactionId,
            OrderId = orderId,
            PaymentProvider = "Stripe",
            ExternalTransactionId = "stripe-789",
            Amount = 25.00m,
            Status = "Success",
            ProcessedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        var paidOrder = await _context.Orders.FirstAsync(o => o.Id == orderId);
        paidOrder.Status = OrderStatus.Paid;
        await _context.SaveChangesAsync();

        // Assert 1 - Verify transaction added
        var ordersWithTransaction = await _context.Orders
            .Include(o => o.Transaction)
            .Where(o => o.Id == orderId && o.Transaction != null)
            .ToListAsync();

        Assert.Single(ordersWithTransaction);
        Assert.Equal(25.00m, ordersWithTransaction[0].Transaction!.Amount);

        // Act 2 - Remove Transaction and update order status
        var transToDelete = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        Assert.NotNull(transToDelete);
        _context.Transactions.Remove(transToDelete);

        var cancelledOrder = await _context.Orders.FirstAsync(o => o.Id == orderId);
        cancelledOrder.Status = OrderStatus.Cancelled;
        await _context.SaveChangesAsync();

        // Assert 2 - Verify transaction removed and order status updated
        var finalTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        var finalOrder = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);

        Assert.Null(finalTransaction);
        Assert.NotNull(finalOrder);
        Assert.Equal(OrderStatus.Cancelled, finalOrder.Status);
    }

    #endregion
}
