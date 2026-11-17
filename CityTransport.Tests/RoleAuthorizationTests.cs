using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CityTransport.Controllers;
using CityTransport.Data;
using CityTransport.DTOs.Auth;
using CityTransport.DTOs.Purchases;
using CityTransport.DTOs.Tickets;
using CityTransport.Entities;
using CityTransport.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CityTransport.Tests;

public class RoleAuthorizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly AuthService _authService;

    public RoleAuthorizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Key", "SuperSecretKeyForJwtAuthenticationThatIsLongEnough123!"},
            {"Jwt:Issuer", "CityTransportApi"},
            {"Jwt:Audience", "CityTransportUser"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _authService = new AuthService(_dbContext, _configuration);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(UserRoles.Passenger)]
    [InlineData(UserRoles.Controller)]
    [InlineData(UserRoles.Admin)]
    public void GenerateTestTokenForRole_ForAllThreeRoles_IncludesRoleAndIdentityClaims(string roleName)
    {
        // Arrange & Act
        var result = _authService.GenerateTestTokenForRole(roleName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal(roleName, result.Role);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Token);

        Assert.Equal("CityTransportApi", jwtToken.Issuer);
        Assert.Contains("CityTransportUser", jwtToken.Audiences);

        // Verify that token contains the designated role claims
        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains(roleName, roleClaims);
    }

    [Fact]
    public async Task LoginAsync_WhenNewUser_CreatesPassengerUserAndReturnsToken()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "newpassenger@example.com",
            Role = UserRoles.Passenger
        };

        // Act
        var response = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(UserRoles.Passenger, response.Role);
        Assert.Equal("newpassenger@example.com", response.Email);

        var savedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        Assert.NotNull(savedUser);
        Assert.Equal(UserRoles.Passenger, savedUser.Role);
    }

    [Fact]
    public async Task RegisterAsync_WhenDuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        _dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            FullName = "Існуючий користувач",
            Role = UserRoles.Passenger
        });
        await _dbContext.SaveChangesAsync();

        var registerDto = new RegisterRequestDto
        {
            Email = "existing@example.com",
            FullName = "Новий клон",
            Role = UserRoles.Passenger
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _authService.RegisterAsync(registerDto));
    }

    [Fact]
    public async Task PurchasesController_WhenUserIsAuthenticatedPassenger_ExtractsUserIdAndCreatesPurchase()
    {
        // Arrange
        var testUserId = Guid.NewGuid();
        var mockPurchaseService = new Mock<IPurchaseService>();
        mockPurchaseService
            .Setup(p => p.CreatePurchaseAsync(It.IsAny<CreatePurchaseRequestDto>(), testUserId))
            .ReturnsAsync(new CreatePurchaseResponseDto
            {
                OrderId = Guid.NewGuid(),
                PaymentUrl = "https://pay.example.com",
                TotalAmount = 30.00m,
                Status = OrderStatus.Pending.ToString(),
                CreatedAt = DateTime.UtcNow
            });

        var controller = new PurchasesController(mockPurchaseService.Object);

        // Set HttpContext with Passenger claims
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, testUserId.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Passenger)
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        var requestDto = new CreatePurchaseRequestDto
        {
            TariffId = Guid.NewGuid(),
            Quantity = 2,
            PaymentProvider = "Monobank"
        };

        // Act
        var result = await controller.CreatePurchase(requestDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var responseDto = Assert.IsType<CreatePurchaseResponseDto>(createdResult.Value);
        Assert.Equal(30.00m, responseDto.TotalAmount);

        mockPurchaseService.Verify(p => p.CreatePurchaseAsync(requestDto, testUserId), Times.Once());
    }

    [Fact]
    public async Task PurchasesController_WhenUserUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var mockPurchaseService = new Mock<IPurchaseService>();
        var controller = new PurchasesController(mockPurchaseService.Object);

        // Empty user context (no NameIdentifier claim)
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var requestDto = new CreatePurchaseRequestDto
        {
            TariffId = Guid.NewGuid(),
            Quantity = 1,
            PaymentProvider = "Monobank"
        };

        // Act
        var result = await controller.CreatePurchase(requestDto);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task TicketsController_GetMyTickets_WhenAuthenticatedWithPassengerRole_ReturnsUserTickets()
    {
        // Arrange
        var testUserId = Guid.NewGuid();
        var mockTicketService = new Mock<ITicketService>();
        var mockLogger = new Mock<ILogger<TicketsController>>();

        mockTicketService
            .Setup(s => s.GetUserTicketsAsync(testUserId, false))
            .ReturnsAsync(new List<TicketDto>
            {
                new() { Id = Guid.NewGuid(), UserId = testUserId, TariffName = "Стандарт" }
            });

        var controller = new TicketsController(mockTicketService.Object, mockLogger.Object);

        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, testUserId.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Passenger)
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        // Act
        var result = await controller.GetMyTickets();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tickets = Assert.IsAssignableFrom<IEnumerable<TicketDto>>(okResult.Value);
        Assert.Single(tickets);
    }

    [Fact]
    public void AuthController_GenerateTestToken_ForController_ReturnsTokenWithControllerRole()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthService>();
        var mockLogger = new Mock<ILogger<AuthController>>();

        mockAuthService
            .Setup(s => s.GenerateTestTokenForRole(UserRoles.Controller, null, null))
            .Returns(new AuthResponseDto
            {
                Token = "JWT_CONTROLLER_TOKEN_ABC",
                Role = UserRoles.Controller,
                Email = "controller@citytransport.local"
            });

        var controller = new AuthController(mockAuthService.Object, mockLogger.Object);

        // Act
        var result = controller.GenerateTestToken("Controller");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.Equal(UserRoles.Controller, response.Role);
        Assert.Equal("JWT_CONTROLLER_TOKEN_ABC", response.Token);
    }
}
