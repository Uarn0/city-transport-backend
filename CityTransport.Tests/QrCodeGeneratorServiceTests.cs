using CityTransport.Utils.TicketUtils;
using Microsoft.Extensions.Options;

namespace CityTransport.Tests;

public class QrCodeGeneratorServiceTests
{
    [Fact]
    public void GenerateEncryptedQRCode_WithValidKey_ReturnsBase64ImageString()
    {
        var cryptoSettings = Options.Create(new CryptoSettings
        {
            EncryptionKey = "12345678901234567890123456789012" 
        });

        var service = new QRCodeGeneratorService(cryptoSettings);
        string testTicketGuid = Guid.NewGuid().ToString();

        string result = service.GenerateEncryptedPayload(testTicketGuid);

        Assert.False(string.IsNullOrEmpty(result));

        var imageBytes = Convert.FromBase64String(result);
        Assert.NotEmpty(imageBytes);
    }

    [Fact]
    public void Constructor_WithInvalidKeyLength_ThrowsArgumentException()
    {
        var cryptoSettings = Options.Create(new CryptoSettings
        {
            EncryptionKey = "short_key" 
        });

        Assert.Throws<ArgumentException>(() => new QRCodeGeneratorService(cryptoSettings));
    }
}
