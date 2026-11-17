using System.Security.Claims;
using CityTransport.DTOs.Purchases;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.PassengerOrAdmin)]
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;

    public PurchasesController(IPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatePurchaseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePurchase([FromBody] CreatePurchaseRequestDto request)
    {
        try
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Необхідно авторизуватися для оформлення покупки квитка." });
            }

            var result = await _purchaseService.CreatePurchaseAsync(request, userId);

            return CreatedAtAction(nameof(CreatePurchase), new { id = result.OrderId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
