namespace CityTransport.Services.Transfers;

public interface ITransferWindowValidator
{
    TransferValidationResult ValidateTransfer(TransferValidationRequest request);
}

