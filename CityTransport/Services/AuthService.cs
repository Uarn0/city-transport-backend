using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CityTransport.Data;
using CityTransport.DTOs.Auth;
using CityTransport.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CityTransport.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public string GenerateToken(Guid userId, string email, string role, string fullName = "")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var secretKey = _configuration["Jwt:Key"] ?? "SuperSecretKeyForJwtAuthenticationThatIsLongEnough123!";
        var key = Encoding.UTF8.GetBytes(secretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(fullName) ? email : fullName),
            // Standard role claim for [Authorize(Roles = "...")]
            new(ClaimTypes.Role, role),
            // Custom role claim for policy-based checks (RequireClaim("role", ...))
            new("role", role)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(2),
            Issuer = _configuration["Jwt:Issuer"] ?? "CityTransportApi",
            Audience = _configuration["Jwt:Audience"] ?? "CityTransportUser",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            // Якщо користувача немає — створимо його для зручності розробки/тестування
            var defaultRole = !string.IsNullOrWhiteSpace(request.Role) ? request.Role : UserRoles.Passenger;
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FullName = request.Email.Split('@')[0],
                Role = defaultRole,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }
        else if (!string.IsNullOrWhiteSpace(request.Role) && user.Role != request.Role)
        {
            // Можливість тимчасово перемкнути роль для тестів
            user.Role = request.Role;
            await _dbContext.SaveChangesAsync();
        }

        var token = GenerateToken(user.Id, user.Email, user.Role, user.FullName);

        return new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        };
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException($"Користувач з email '{request.Email}' вже зареєстрований.");
        }

        var normalizedRole = string.IsNullOrWhiteSpace(request.Role) ? UserRoles.Passenger : request.Role;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FullName = request.FullName,
            Role = normalizedRole,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = GenerateToken(user.Id, user.Email, user.Role, user.FullName);

        return new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        };
    }

    public AuthResponseDto GenerateTestTokenForRole(string role, Guid? userId = null, string? email = null)
    {
        var targetUserId = userId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
        var targetEmail = email ?? $"{role.ToLowerInvariant()}@citytransport.local";
        var fullName = $"Тестовий {role}";

        var token = GenerateToken(targetUserId, targetEmail, role, fullName);

        return new AuthResponseDto
        {
            Token = token,
            UserId = targetUserId,
            Email = targetEmail,
            FullName = fullName,
            Role = role,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        };
    }
}
