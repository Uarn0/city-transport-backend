using CityTransport.DTOs.Auth;
using CityTransport.Entities;

namespace CityTransport.Services;

public interface IAuthService
{
    string GenerateToken(Guid userId, string email, string role, string fullName = "");
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    AuthResponseDto GenerateTestTokenForRole(string role, Guid? userId = null, string? email = null);
}
