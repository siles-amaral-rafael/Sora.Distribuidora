using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Sora.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Sora.Api.Development;
using Sora.Api.Auth;
using Sora.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Options;

namespace Sora.ApiTests;

[Collection("Isolated PostgreSQL")]
public sealed class ApiAuthenticationTests : IClassFixture<SoraApiFactory>, IAsyncLifetime
{
    private readonly SoraApiFactory _factory;
    private readonly HttpClient _client;
    public ApiAuthenticationTests(SoraApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.VerifyDatabaseIdentityAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "API-AUTH-01 · health público devuelve estado operativo")]
    public async Task Health_is_public_and_reports_ok()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", json?.Status);
    }

    [Fact(DisplayName = "API-AUTH-02 · endpoint protegido rechaza anónimo")]
    public async Task Protected_current_account_rejects_anonymous_request()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "API-AUTH-03 · contraseña incorrecta no emite JWT")]
    public async Task Login_with_invalid_password_does_not_issue_token()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@sora.demo", password = "incorrecta-no-valida"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "API-AUTH-04 · login válido emite JWT y consulta identidad")]
    public async Task Valid_test_account_receives_jwt_and_can_read_current_account()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@sora.demo", password = "SoraDemo2026!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login?.AccessToken));
        _client.DefaultRequestHeaders.Authorization = new("Bearer", login!.AccessToken);
        var meResponse = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal("ADMIN@SORA.DEMO", me?.Email);
    }

    [Fact(DisplayName = "API-ACL-01 · vendedor recibe 403 en administración de personal")]
    public async Task Seller_cannot_read_staff_administration()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "vendedor@sora.demo", password = "SoraDemo2026!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login?.AccessToken));
        _client.DefaultRequestHeaders.Authorization = new("Bearer", login!.AccessToken);

        var response = await _client.GetAsync("/api/staff");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "API-VAL-01 · cliente incompleto devuelve 400")]
    public async Task Invalid_customer_payload_is_rejected()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@sora.demo", password = "SoraDemo2026!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", login!.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/customers", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record HealthResponse(string Status, string Application, DateTimeOffset Utc);
    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
    private sealed record MeResponse(Guid Id, string Email, string FullName, string Role);
}

public sealed class SoraApiFactory : WebApplicationFactory<Program>
{
    private static readonly string IsolatedTestPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(DemoDataSeeder.DemoPassword, 12);

    public async Task VerifyDatabaseIdentityAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SoraDbContext>();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select current_database() || ':' || current_user";
            var identity = Convert.ToString(await command.ExecuteScalarAsync());
            Assert.Equal("sora_it:sora_it", identity);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        // Prepare fixtures explicitly after verifying the real database identity.
        // This avoids relying on WebApplicationFactory configuration ordering.
        await db.Database.MigrateAsync();
        await DemoDataSeeder.SeedAsync(db, CancellationToken.None);

        // DemoDataSeeder intentionally does not overwrite credentials of existing
        // accounts. Integration tests need deterministic, disposable credentials.
        var demoEmails = new[]
        {
            "ADMIN@SORA.DEMO", "VENDEDOR@SORA.DEMO", "ALMACEN@SORA.DEMO",
            "DESPACHO@SORA.DEMO", "AYUDANTE@SORA.DEMO", "REPARTIDOR@SORA.DEMO"
        };
        var accounts = await db.StaffAccounts
            .Where(account => demoEmails.Contains(account.Email))
            .ToListAsync();
        Assert.Equal(demoEmails.Length, accounts.Count);
        foreach (var account in accounts)
            db.Entry(account).Property(x => x.PasswordHash).CurrentValue = IsolatedTestPasswordHash;
        await db.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("SORA_IT_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString) ||
            !connectionString.Contains("Database=sora_it", StringComparison.OrdinalIgnoreCase) ||
            !connectionString.Contains("Username=sora_it", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La API de prueba exige SORA_IT_CONNECTION_STRING para sora_it/sora_it.");
        var parsed = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (!parsed.TryGetValue("Host", out var host) ||
            !string.Equals(Convert.ToString(host), "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            !parsed.TryGetValue("Port", out var port) || Convert.ToString(port) != "55433")
            throw new InvalidOperationException("La API de prueba solo admite PostgreSQL de integración en 127.0.0.1:55433.");

        var signingKey = Environment.GetEnvironmentVariable("SORA_IT_JWT_SIGNING_KEY")
            ?? throw new InvalidOperationException("Falta SORA_IT_JWT_SIGNING_KEY.");
        const string issuer = "Sora.Api.IntegrationTests";
        const string audience = "Sora.Api.IntegrationTests";
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["Database:MigrateOnStartup"] = "false",
                ["DemoData:Enabled"] = "false",
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:SigningKey"] = signingKey,
                ["Jwt:ExpiresMinutes"] = "5"
            }));
        // User Secrets or appsettings may be applied after in-memory configuration.
        // Replace the registered DbContext itself so the test host cannot fall back
        // to a developer database such as Database=sora; only the guarded sora_it
        // connection above is used by API requests.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SoraDbContext>();
            services.RemoveAll<DbContextOptions<SoraDbContext>>();
            services.AddDbContext<SoraDbContext>(options => options.UseNpgsql(connectionString));
            services.RemoveAll<ITokenIssuer>();
            services.AddSingleton<ITokenIssuer>(_ => new JwtTokenIssuer(
                Options.Create(new JwtOptions
                {
                    Issuer = issuer,
                    Audience = audience,
                    SigningKey = signingKey,
                    ExpiresMinutes = 5
                }), TimeProvider.System));
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.ValidIssuer = issuer;
                options.TokenValidationParameters.ValidAudience = audience;
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            });
        });
    }
}

[CollectionDefinition("Isolated PostgreSQL", DisableParallelization = true)]
public sealed class ApiTestCollection { }
