using System.Security.Claims;
using CityTransport.DTOs.Auth;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Авторизація або вхід користувача з отриманням JWT-токена
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка авторизації для email: {Email}", request.Email);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Реєстрація нового користувача (Пасажир за замовчуванням)
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return CreatedAtAction(nameof(GetMyProfile), new { }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Отримання тестового JWT-токена для будь-якої з 3-х ролей (Passenger, Controller, Admin)
    /// </summary>
    [HttpGet("test-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public IActionResult GenerateTestToken([FromQuery] string role = UserRoles.Passenger)
    {
        // Нормалізація ролі
        var normalizedRole = role.ToLowerInvariant() switch
        {
            "controller" or "driver" or "inspector" => UserRoles.Controller,
            "admin" or "carrier" => UserRoles.Admin,
            _ => UserRoles.Passenger
        };

        var response = _authService.GenerateTestTokenForRole(normalizedRole);
        return Ok(response);
    }

    /// <summary>
    /// Отримання поточної ідентичності та ролі з активного JWT-токена
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetMyProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var email = User.FindFirstValue(ClaimTypes.Email);
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? UserRoles.Passenger;
        var name = User.FindFirstValue(ClaimTypes.Name);

        return Ok(new
        {
            UserId = userId,
            Email = email,
            Role = role,
            FullName = name
        });
    }
}
