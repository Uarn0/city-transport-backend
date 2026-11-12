using CityTransport.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class QRCodeController : ControllerBase
{
    /// <summary>
    /// Генерація зображення QR-коду (доступно для пасажира або адміністратора)
    /// </summary>
    [HttpGet("generate")]
    [Authorize(Roles = UserRoles.PassengerOrAdmin)]
    public IActionResult GenerateQRCode([FromQuery] string data)
    {
        throw new NotImplementedException("QR code generation is not implemented yet.");
    }

    /// <summary>
    /// Сканування та розпізнавання QR-коду мобільним пристроєм контролера/водія
    /// </summary>
    [HttpPost("scan")]
    [Authorize(Roles = UserRoles.ControllerOrAdmin)]
    public IActionResult ScanQRCode([FromBody] string qrCodeData)
    {
        throw new NotImplementedException("QR code scanning is not implemented yet.");
    }
}
