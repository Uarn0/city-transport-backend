using CityTransport.DTOs.Routes;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class StopsController : ControllerBase
{
    private readonly IStopService _stopService;
    private readonly ILogger<StopsController> _logger;

    public StopsController(IStopService stopService, ILogger<StopsController> logger)
    {
        _stopService = stopService;
        _logger = logger;
    }

    /// <summary>
    /// Get all stops from the database (optional geographic bounding box filter)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<StopDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStops(
        [FromQuery] double? minLat,
        [FromQuery] double? minLng,
        [FromQuery] double? maxLat,
        [FromQuery] double? maxLng)
    {
        var stops = await _stopService.GetAllStopsAsync(minLat, minLng, maxLat, maxLng);
        return Ok(stops);
    }

    /// <summary>
    /// Get a specific stop by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StopDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStopById(Guid id)
    {
        var stop = await _stopService.GetStopByIdAsync(id);
        if (stop == null)
            return NotFound(new { message = $"Stop {id} not found." });

        return Ok(stop);
    }

    /// <summary>
    /// Create a stop manually (with deduplication)
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StopDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStop([FromBody] CreateStopRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var stop = await _stopService.CreateStopAsync(request);
        _logger.LogInformation("Stop '{Name}' created or resolved at ({Lat}, {Lng})", stop.Name, stop.Latitude, stop.Longitude);
        return CreatedAtAction(nameof(GetStopById), new { id = stop.Id }, stop);
    }

    /// <summary>
    /// Batch import stops from Overpass API (with deduplication)
    /// </summary>
    [HttpPost("import")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BatchImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportStops([FromBody] BatchImportStopsRequestDto request)
    {
        if (request == null || request.Stops == null)
            return BadRequest(new { message = "Request body must contain 'stops' array." });

        var result = await _stopService.BatchImportStopsAsync(request);
        _logger.LogInformation("Imported {ImportedCount} stops, skipped {SkippedCount} duplicates",
            result.ImportedCount, result.SkippedDuplicatesCount);

        return Ok(result);
    }

    /// <summary>
    /// Delete a stop by ID
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStop(Guid id)
    {
        var deleted = await _stopService.DeleteStopAsync(id);
        if (!deleted)
            return NotFound(new { message = $"Stop {id} not found." });

        return NoContent();
    }
}
