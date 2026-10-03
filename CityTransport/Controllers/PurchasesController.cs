using System.Security.Claims;
using CityTransport.DTOs.Purchases;
using CityTransport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePurchase([FromBody] CreatePurchaseRequestDto request)
    {
        try
        {
            // extract userId from claims (assuming authentication is implemented) 
            // for a time being using default Guid
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = string.IsNullOrEmpty(userIdClaim) 
                ? Guid.Parse("00000000-0000-0000-0000-000000000001") 
                : Guid.Parse(userIdClaim);

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