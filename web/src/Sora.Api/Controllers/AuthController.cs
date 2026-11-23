using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Sora.Application.Auth;
using Sora.Shared.Auth;

namespace Sora.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IWebHostEnvironment environment) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email"),
        fullName = User.Identity?.Name,
        role = User.FindFirstValue("role_name"),
        roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value),
        permissions = User.FindAll("permission").Select(x => x.Value)
    });

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await authService.RegisterAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Conflict(new { code = result.ErrorCode });
        }

        return StatusCode(StatusCodes.Status201Created, ToResponse(result.Token!));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Unauthorized(new { code = result.ErrorCode });
        }

        return Ok(ToResponse(result.Token!));
    }

    private static AuthResponse ToResponse(IssuedToken token) =>
        new(token.Value, token.ExpiresAtUtc);
}
