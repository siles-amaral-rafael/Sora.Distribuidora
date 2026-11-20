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
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(ClaimTypes.Name, account.FullName),
            new Claim(ClaimTypes.Role, account.RoleName)
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
