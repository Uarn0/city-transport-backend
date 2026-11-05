using CityTransport.Entities;

namespace CityTransport.Services.Transfers;

public class TransferWindowValidator : ITransferWindowValidator
{
    public TransferValidationResult ValidateTransfer(TransferValidationRequest request)
    {
        // 1. Check for clock anomaly / validation before purchase
        if (request.ValidationTime < request.PurchasedAt)
        {
            return new TransferValidationResult
            {
                IsValid = false,
                IsFreeTransfer = false,
                RemainingMinutes = 0,
                Message = "Некоректний час валідації (раніше моменту придбання)."
            };
        }

        // 2. Check ticket status
        if (request.Status == TicketStatus.Expired)
        {
            return new TransferValidationResult
            {
                IsValid = false,
                IsFreeTransfer = false,
                RemainingMinutes = 0,
                Message = "Квиток прострочений або недійсний."
            };
        }

        // 3. Check if transfer window has expired
        if (request.ValidationTime > request.ValidUntil)
        {
            return new TransferValidationResult
            {
                IsValid = false,
                IsFreeTransfer = false,
                RemainingMinutes = 0,
                Message = "Термін дії вікна пересадки вичерпано."
            };
        }

        // 4. Double-tap detection on the same route within 2 minutes
        if (request.LastValidationTime.HasValue && 
            request.FirstRouteId.HasValue && 
            request.FirstRouteId.Value == request.TargetRouteId)
        {
            var timeSinceLastValidation = request.ValidationTime - request.LastValidationTime.Value;
            if (timeSinceLastValidation.TotalMinutes < 2.0)
            {
                return new TransferValidationResult
                {
                    IsValid = false,
                    IsFreeTransfer = false,
                    RemainingMinutes = (int)Math.Max(0, (request.ValidUntil - request.ValidationTime).TotalMinutes),
                    Message = "Виявлено повторне прикладання квитка на тому ж маршруті."
                };
            }
        }

        // 5. Successful transfer
        var remainingMinutes = (int)Math.Ceiling((request.ValidUntil - request.ValidationTime).TotalMinutes);

        return new TransferValidationResult
        {
            IsValid = true,
            IsFreeTransfer = true,
            RemainingMinutes = Math.Max(0, remainingMinutes),
            Message = "Пересадка успішна (в межах діючого часового вікна)."
        };
    }
}
