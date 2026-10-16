using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using QRCoder;

namespace CityTransport.Utils.TicketUtils
{
    public class QRCodeGeneratorService
    {
        // Для AES-256 довжина ключа повинна бути рівно 32 байти (символи)
        private readonly byte[] _encryptionKey;

        public QRCodeGeneratorService(IOptions<CryptoSettings> options)
        {
            var encryptionKey = options.Value.EncryptionKey;
            if (encryptionKey.Length != 32)
            {
                throw new ArgumentException("Encryption key must be 32 characters long for AES-256.");
            }
            _encryptionKey = Encoding.UTF8.GetBytes(encryptionKey);
        }

        /// <summary>
        /// Генерує чистий зашифрований payload для збереження в базу даних (Ticket.QrCodePayload)
        /// </summary>
        public string GenerateEncryptedPayload(string ticketGuid)
        {
            string rawPayload = $"{ticketGuid}|{DateTime.UtcNow:O}";
            return EncryptString(rawPayload, _encryptionKey);
        }

        /// <summary>
        /// Генерує Base64-рядок PNG зображення QR-коду на основі вже зашифрованого пейлоаду
        /// </summary>
        public string GenerateQRCodeImage(string encryptedPayload)
        {
            using (var qrGenerator = new QRCoder.QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(encryptedPayload, QRCoder.QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeBytes = qrCode.GetGraphic(20);

                return Convert.ToBase64String(qrCodeBytes);
            }
        }

        // --- Метод дешифрування залишається без змін ---
        public bool TryDecryptPayload(string encryptedPayload, out string? ticketGuid, out DateTime generatedAt)
        {
            ticketGuid = null;
            generatedAt = default;

            try
            {
                var fullCipher = Convert.FromBase64String(encryptedPayload);
                if (fullCipher.Length < 16) return false;

                using var aes = Aes.Create();
                aes.Key = _encryptionKey;

                // витягуємо IV (перші 16 байт)
                byte[] iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, 16);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var msDecrypt = new MemoryStream(fullCipher, 16, fullCipher.Length - 16);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                string plainText = srDecrypt.ReadToEnd();
                var parts = plainText.Split('|');
                
                if (parts.Length == 2 && Guid.TryParse(parts[0], out var parsedGuid) && 
                    DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTime))
                {
                    ticketGuid = parsedGuid.ToString();
                    generatedAt = parsedTime;
                    return true;
                }
            }
            catch
            {
                // помилка дешифрування
                return false;
            }

            return false;
        }

        private string EncryptString(string plainText, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var msEncrypt = new MemoryStream();

            msEncrypt.Write(aes.IV, 0, aes.IV.Length);

            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }
    }
}