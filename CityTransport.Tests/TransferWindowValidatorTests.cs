using CityTransport.Entities;
using CityTransport.Services.Transfers;

namespace CityTransport.Tests;

public class TransferWindowValidatorTests
{
    private readonly ITransferWindowValidator _validator;

    public TransferWindowValidatorTests()
    {
        _validator = new TransferWindowValidator();
    }

    [Fact]
    public void ValidateTransfer_WithinTimeWindowAndDifferentRoute_ReturnsValidFreeTransfer()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = new TransferValidationRequest
        {
            TicketId = Guid.NewGuid(),
            PurchasedAt = now.AddMinutes(-25),
            ValidUntil = now.AddMinutes(35), // 60 min total window, 35 min remaining
            Status = TicketStatus.Active,
            FirstRouteId = Guid.NewGuid(),
            TargetRouteId = Guid.NewGuid(),
            ValidationTime = now
        };

        // Act
        var result = _validator.ValidateTransfer(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.True(result.IsFreeTransfer);
        Assert.True(result.RemainingMinutes >= 34 && result.RemainingMinutes <= 36);
        Assert.Contains("успішн", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTransfer_WhenValidationTimeExceedsValidUntil_ReturnsInvalidExpired()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = new TransferValidationRequest
        {
            TicketId = Guid.NewGuid(),
            PurchasedAt = now.AddMinutes(-65),
            ValidUntil = now.AddMinutes(-5), // Expired 5 minutes ago
            Status = TicketStatus.Active,
            FirstRouteId = Guid.NewGuid(),
            TargetRouteId = Guid.NewGuid(),
            ValidationTime = now
        };

        // Act
        var result = _validator.ValidateTransfer(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.False(result.IsFreeTransfer);
        Assert.Equal(0, result.RemainingMinutes);
        Assert.Contains("вичерпано", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTransfer_WhenTicketStatusIsExpired_ReturnsInvalid()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = new TransferValidationRequest
        {
            TicketId = Guid.NewGuid(),
            PurchasedAt = now.AddMinutes(-70),
            ValidUntil = now.AddMinutes(-10),
            Status = TicketStatus.Expired,
            FirstRouteId = Guid.NewGuid(),
            TargetRouteId = Guid.NewGuid(),
            ValidationTime = now
        };

        // Act
        var result = _validator.ValidateTransfer(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("прострочен", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTransfer_WhenValidationTimeIsBeforePurchasedAt_ReturnsInvalidClockDrift()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = new TransferValidationRequest
        {
            TicketId = Guid.NewGuid(),
            PurchasedAt = now.AddMinutes(5), // Future purchase timestamp (clock anomaly)
            ValidUntil = now.AddMinutes(65),
            Status = TicketStatus.Active,
            FirstRouteId = Guid.NewGuid(),
            TargetRouteId = Guid.NewGuid(),
            ValidationTime = now
        };

        // Act
        var result = _validator.ValidateTransfer(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("некоректн", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTransfer_DoubleTapOnSameRouteWithinTwoMinutes_ReturnsDoubleTapDetected()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sameRouteId = Guid.NewGuid();
        var request = new TransferValidationRequest
        {
            TicketId = Guid.NewGuid(),
            PurchasedAt = now.AddMinutes(-10),
            ValidUntil = now.AddMinutes(50),
            Status = TicketStatus.Used,
            FirstRouteId = sameRouteId,
            TargetRouteId = sameRouteId,
            LastValidationTime = now.AddSeconds(-45), // 45 seconds ago on same route
            ValidationTime = now
        };

        // Act
        var result = _validator.ValidateTransfer(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.False(result.IsFreeTransfer);
        Assert.Contains("повторн", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}

