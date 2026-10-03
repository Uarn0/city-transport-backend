using CityTransport.DTOs.Tickets;
using CityTransport.Services;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<TicketsController> _logger;

    // Hardcoded dev user ID until full JWT Auth middleware is connected
    private static readonly Guid DevUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public TicketsController(ITicketService ticketService, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(IEnumerable<TicketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTickets([FromQuery] bool onlyActive = false)
    {
        _logger.LogInformation("Fetching tickets for User: {UserId}, OnlyActive: {OnlyActive}", DevUserId, onlyActive);
        var tickets = await _ticketService.GetUserTicketsAsync(DevUserId, onlyActive);
        return Ok(tickets);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketById([FromRoute] Guid id)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(id, DevUserId);
        if (ticket == null)
        {
            return NotFound(new { message = $"Ticket with ID {id} not found." });
        }

        return Ok(ticket);
    }
}