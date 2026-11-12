using CityTransport.Utils.TicketUtils;
using System.Security.Cryptography;
using System.Security.Claims;
using CityTransport.Data;
using CityTransport.Entities;
using CityTransport.Services;
using CityTransport.Services.Payments;
using CityTransport.Services.Transfers;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CityTransportApi",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CityTransportUser",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForJwtAuthenticationThatIsLongEnough123!")),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    };
});

builder.Services.AddAuthorization(options =>
{
    // 1. Політика для Пасажирів
    options.AddPolicy("PassengerPolicy", policy => policy.RequireRole(UserRoles.Passenger, UserRoles.User, UserRoles.Admin, UserRoles.Carrier));
    // 2. Політика для Контролерів / Водіїв
    options.AddPolicy("ControllerPolicy", policy => policy.RequireRole(UserRoles.Controller, UserRoles.Driver, UserRoles.Inspector, UserRoles.Admin, UserRoles.Carrier));
    // 3. Політика для Адміністраторів / Перевізників
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole(UserRoles.Admin, UserRoles.Carrier));
    // Зворотна сумісність
    options.AddPolicy("UserPolicy", policy => policy.RequireRole(UserRoles.Passenger, UserRoles.User, UserRoles.Admin, UserRoles.Carrier));
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ITariffService, TariffService>();
builder.Services.AddScoped<ITransferWindowValidator, TransferWindowValidator>();
builder.Services.AddScoped<IPaymentGateway, MonobankPaymentGateway>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.Configure<CryptoSettings>(builder.Configuration.GetSection("CryptoSettings"));
builder.Services.AddTransient<IQRCodeGeneratorService, QRCodeGeneratorService>();
builder.Services.AddTransient<QRCodeGeneratorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("CityTransport API Reference")
               .WithTheme(ScalarTheme.Purple)
               .WithDefaultHttpClient(ScalarTarget.Http, ScalarClient.Http11);
    });
}


app.UseHttpsRedirection();

app.UseRouting();


app.UseAuthentication(); 
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllers();

app.Run();
