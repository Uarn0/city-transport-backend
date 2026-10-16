using CityTransport.DTOs.Tickets;
using CityTransport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<TicketsController> _logger;

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    public TicketsController(ITicketService ticketService, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(IEnumerable<TicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTickets([FromQuery] bool onlyActive = false)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in session token." });
        }

        _logger.LogInformation("Fetching tickets for User: {UserId}, OnlyActive: {OnlyActive}", userId, onlyActive);
        var tickets = await _ticketService.GetUserTicketsAsync(userId.Value, onlyActive);
        return Ok(tickets);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTicketById([FromRoute] Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in session token." });
        }

        var ticket = await _ticketService.GetTicketByIdAsync(id, userId.Value);
        if (ticket == null)
        {
            return NotFound(new { message = $"Ticket with ID {id} not found." });
        }

        return Ok(ticket);
    }

    [HttpGet("types")]
    [ProducesResponseType(typeof(IEnumerable<TicketDto>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<string>>> GetTicketTypes()
    {
        var types = await _ticketService.GetAvailableTicketTypesAsync();
        return Ok(types);
    }
    /// <summary>
    /// Валідація та сканування квитка за його зашифрованим QR payload (для контролерів/водіїв)
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(TicketVerificationResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyTicket([FromBody] VerifyTicketRequestDto request)
    {
        var result = await _ticketService.VerifyAndUseTicketAsync(request.EncryptedPayload);
        return Ok(result);
    }
    [HttpGet("generate-test-token")]
[AllowAnonymous]
public IActionResult GenerateTestToken([FromServices] IConfiguration configuration)
{
    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    var key = System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "super_secret_key_that_is_long_enough_12345");
    
    var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
    {
        Subject = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "default@example.com")
        }),
        Expires = DateTime.UtcNow.AddHours(1),
        Issuer = configuration["Jwt:Issuer"],
        Audience = configuration["Jwt:Audience"],
        SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
    };
    
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return Ok(new { Token = tokenHandler.WriteToken(token) });
}
}