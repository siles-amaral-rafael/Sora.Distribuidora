using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sora.Application.Auth;
using Sora.Domain.Entities;

namespace Sora.Api.Auth;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenIssuer
{
    public IssuedToken Issue(StaffAccount account)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.ExpiresMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(ClaimTypes.Name, account.FullName)
        };
        foreach (var role in account.RoleAssignments)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.RoleCode));
            claims.Add(new Claim("role_name", DisplayName(role.RoleCode)));
        }
        foreach (var permission in account.RoleAssignments
                     .SelectMany(role => role.Role?.Permissions ?? [])
                     .Select(permission => permission.PermissionCode)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim("permission", permission));

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static string DisplayName(string roleCode) => roleCode switch
    {
        "ADMIN" => "Administrador",
        "SELLER" => "Vendedor",
        "WAREHOUSE" => "Encargado de almacén",
        "DISPATCH_LEAD" => "Jefe de despacho",
        "DISPATCH_ASSISTANT" => "Ayudante",
        "DRIVER" => "Repartidor",
        _ => roleCode
    };
}
