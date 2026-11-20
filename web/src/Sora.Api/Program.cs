using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sora.Api.Auth;
using Sora.Application.Auth;
using Sora.Domain.Repositories;
using Sora.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings:Postgres. Configúrala con dotnet user-secrets para desarrollo.");
}

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección de configuración Jwt.");
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 bytes.");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("BlazorClient", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.AddDbContext<SoraDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IStaffAccountRepository, StaffAccountRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("BlazorClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
