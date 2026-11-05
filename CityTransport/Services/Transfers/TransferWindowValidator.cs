using CityTransport.Entities;

namespace CityTransport.Services.Transfers;

public class TransferWindowValidator : ITransferWindowValidator
{
    public const double DoubleTapThresholdMinutes = 2.0;

    public TransferValidationResult ValidateTransfer(TransferValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsClockAnomaly(request))
        {
            return TransferValidationResult.Failure("Некоректний час валідації (раніше моменту придбання).");
        }

        if (IsTicketInactiveOrExpired(request))
        {
            return TransferValidationResult.Failure("Квиток прострочений або недійсний.");
        }

        if (IsTransferWindowExpired(request))
        {
            return TransferValidationResult.Failure("Термін дії вікна пересадки вичерпано.");
        }

        if (IsDoubleTapOnSameRoute(request))
        {
            var remaining = CalculateRemainingMinutes(request);
            return TransferValidationResult.Failure("Виявлено повторне прикладання квитка на тому ж маршруті.", remaining);
        }

        var remainingMinutes = CalculateRemainingMinutes(request);
        return TransferValidationResult.Success(remainingMinutes);
    }

    private static bool IsClockAnomaly(TransferValidationRequest request) =>
        request.ValidationTime < request.PurchasedAt;

    private static bool IsTicketInactiveOrExpired(TransferValidationRequest request) =>
        request.Status == TicketStatus.Expired;

    private static bool IsTransferWindowExpired(TransferValidationRequest request) =>
        request.ValidationTime > request.ValidUntil;

    private static bool IsDoubleTapOnSameRoute(TransferValidationRequest request)
    {
        if (!request.LastValidationTime.HasValue || !request.FirstRouteId.HasValue)
        {
            return false;
        }

        var isSameRoute = request.FirstRouteId.Value == request.TargetRouteId;
        var isWithinThreshold = (request.ValidationTime - request.LastValidationTime.Value).TotalMinutes < DoubleTapThresholdMinutes;

        return isSameRoute && isWithinThreshold;
    }

    private static int CalculateRemainingMinutes(TransferValidationRequest request)
    {
        var diff = (request.ValidUntil - request.ValidationTime).TotalMinutes;
        return (int)Math.Max(0, Math.Ceiling(diff));
    }
}
