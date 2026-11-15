using CityTransport.Utils.TicketUtils;
using Microsoft.Extensions.Options;

namespace CityTransport.Tests;

public class QrCodeGeneratorServiceTests
{
    private readonly IOptions<CryptoSettings> _validCryptoSettings;
    private readonly QRCodeGeneratorService _service;

    public QrCodeGeneratorServiceTests()
    {
        // Common setup in constructor
        _validCryptoSettings = Options.Create(new CryptoSettings
        {
            EncryptionKey = "12345678901234567890123456789012" // Exactly 32 characters
        });

        _service = new QRCodeGeneratorService(_validCryptoSettings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short_key")]
    [InlineData("1234567890123456789012345678901")] // 31 characters
    [InlineData("123456789012345678901234567890123")] // 33 characters
    public void Constructor_WithInvalidKeyLengths_ThrowsArgumentException(string invalidKey)
    {
        // Arrange
        var invalidSettings = Options.Create(new CryptoSettings
        {
            EncryptionKey = invalidKey
        });

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new QRCodeGeneratorService(invalidSettings));
        Assert.Contains("32 characters", exception.Message);
    }

    [Fact]
    public void GenerateEncryptedPayload_WhenDecrypted_RestoresOriginalTicketGuidAndRecentTimestamp()
    {
        // Arrange
        var originalGuid = Guid.NewGuid().ToString();

        // Act
        var encryptedPayload = _service.GenerateEncryptedPayload(originalGuid);
        var decryptSuccess = _service.TryDecryptPayload(encryptedPayload, out var decryptedGuid, out var generatedAt);

        // Assert
        Assert.NotEmpty(encryptedPayload);
        Assert.True(decryptSuccess);
        Assert.Equal(originalGuid, decryptedGuid);
        Assert.True((DateTime.UtcNow - generatedAt).TotalSeconds < 10);
    }

    [Fact]
    public void GenerateQRCodeImage_WithEncryptedPayload_ReturnsValidPngBase64String()
    {
        // Arrange
        var testGuid = Guid.NewGuid().ToString();
        var payload = _service.GenerateEncryptedPayload(testGuid);

        // Act
        var base64Image = _service.GenerateQRCodeImage(payload);

        // Assert
        Assert.False(string.IsNullOrEmpty(base64Image));
        var imageBytes = Convert.FromBase64String(base64Image);
        Assert.NotEmpty(imageBytes);

        // Verify PNG magic header: 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(imageBytes.Length > 8);
        for (int i = 0; i < pngHeader.Length; i++)
        {
            Assert.Equal(pngHeader[i], imageBytes[i]);
        }
    }

    [Fact]
    public void TryDecryptPayload_WithCorruptedPayload_ReturnsFalse()
    {
        // Arrange
        var corruptedPayload = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        // Act
        var result = _service.TryDecryptPayload(corruptedPayload, out var ticketGuid, out var generatedAt);

        // Assert
        Assert.False(result);
        Assert.Null(ticketGuid);
        Assert.Equal(default, generatedAt);
    }
}
