using CityTransport.DTOs.Routes;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class RoutesController : ControllerBase
{
    private readonly IRouteService _routeService;
    private readonly ILogger<RoutesController> _logger;

    public RoutesController(IRouteService routeService, ILogger<RoutesController> logger)
    {
        _routeService = routeService;
        _logger = logger;
    }

    /// <summary>
    /// Get all routes with their stops for map display
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<RouteWithStopsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRoutes()
    {
        var routes = await _routeService.GetAllRoutesAsync();
        return Ok(routes);
    }

    /// <summary>
    /// Get a specific route with stops by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RouteWithStopsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRouteById(Guid id)
    {
        var route = await _routeService.GetRouteByIdAsync(id);
        if (route == null)
            return NotFound(new { message = $"Route {id} not found." });
        return Ok(route);
    }

    /// <summary>
    /// Create a new route with stops (Admin/Carrier only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.AdminOnly)]
    [ProducesResponseType(typeof(RouteWithStopsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRoute([FromBody] CreateRouteRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var route = await _routeService.CreateRouteAsync(request);
        _logger.LogInformation("Route {RouteNumber} created with {StopCount} stops", route.Number, route.Stops.Count);
        return CreatedAtAction(nameof(GetRouteById), new { id = route.Id }, route);
    }

    /// <summary>
    /// Update stops of an existing route (Admin/Carrier only)
    /// </summary>
    [HttpPut("{id:guid}/stops")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    [ProducesResponseType(typeof(RouteWithStopsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRouteStops(Guid id, [FromBody] UpdateRouteStopsDto request)
    {
        var route = await _routeService.UpdateRouteStopsAsync(id, request);
        if (route == null)
            return NotFound(new { message = $"Route {id} not found." });

        _logger.LogInformation("Route {Id} stops updated", id);
        return Ok(route);
    }

    /// <summary>
    /// Delete a route (Admin/Carrier only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoute(Guid id)
    {
        var deleted = await _routeService.DeleteRouteAsync(id);
        if (!deleted)
            return NotFound(new { message = $"Route {id} not found." });
        return NoContent();
    }

    /// <summary>
    /// Find routes between two geo-coordinates (from/to lat/lng)
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<RouteSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchRoutes(
        [FromQuery] double fromLat,
        [FromQuery] double fromLng,
        [FromQuery] double toLat,
        [FromQuery] double toLng)
    {
        var results = await _routeService.FindRoutesAsync(fromLat, fromLng, toLat, toLng);
        return Ok(results);
    }
}
