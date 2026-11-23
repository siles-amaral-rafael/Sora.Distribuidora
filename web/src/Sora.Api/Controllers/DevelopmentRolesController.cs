using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sora.Application.Auth;

namespace Sora.Api.Controllers;

public sealed class DevelopmentRoleRequest
{
    [Required, StringLength(40)]
    public string RoleCode { get; init; } = string.Empty;
}

[ApiController]
[Authorize]
[Route("api/auth/development/roles")]
public sealed class DevelopmentRolesController(
    IDevelopmentRoleAssignmentService roleAssignment,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignToSelf(DevelopmentRoleRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawId, out var staffAccountId))
        {
            return Unauthorized();
        }

        var result = await roleAssignment.AssignToCurrentUserAsync(staffAccountId, request.RoleCode, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorCode == "staff_account_not_found"
                ? NotFound(new { code = result.ErrorCode })
                : Conflict(new { code = result.ErrorCode });
        }

        return Ok(new { roles = result.Roles, message = "Inicia sesión de nuevo para recibir un token con los roles actualizados." });
    }
}
