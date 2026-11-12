namespace CityTransport.Utils.TicketUtils;

public interface IQRCodeGeneratorService
{
    /// <summary>
    /// Генерує зашифрований payload квитка за його GUID (для Ticket.QrCodePayload)
    /// </summary>
    string GenerateEncryptedPayload(string ticketGuid);

    /// <summary>
    /// Генерує Base64-рядок PNG зображення QR-коду на основі зашифрованого пейлоаду
    /// </summary>
    string GenerateQRCodeImage(string encryptedPayload);

    /// <summary>
    /// Спроба розшифрувати payload і витягти GUID квитка та час його генерації
    /// </summary>
    bool TryDecryptPayload(string encryptedPayload, out string? ticketGuid, out DateTime generatedAt);
}
