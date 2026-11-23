using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sora.Application.Auth;
using Sora.Shared.Auth;
using Sora.Api.Auth;

namespace Sora.Api.Controllers;

[ApiController, Authorize(Policy = PermissionPolicies.StaffManage)]
[Route("api/staff")]
public sealed class StaffAdministrationController(IStaffAdministrationService staff) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await staff.ListAccountsAsync(ct));

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await staff.ListRolesAsync(ct));

    [HttpGet("permissions")]
    public IActionResult Permissions() => Ok(staff.ListPermissions());

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(CancellationToken ct) => Ok(await staff.ListAuditAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffAccountRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await staff.CreateAsync(request, actorId, ct);
        if (!result.Succeeded)
            return result.ErrorCode switch
            {
                "email_already_registered" => Conflict(new { code = result.ErrorCode }),
                "role_not_found" => BadRequest(new { code = result.ErrorCode }),
                _ => BadRequest(new { code = result.ErrorCode })
            };
        return CreatedAtAction(nameof(List), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> ReplaceRoles(Guid id, ReplaceStaffRolesRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actorId)) return Unauthorized();
        var result = await staff.ReplaceRolesAsync(id, request, actorId, ct);
        return ToMutationResult(result);
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, SetStaffActiveRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actorId)) return Unauthorized();
        return ToMutationResult(await staff.SetActiveAsync(id, request, actorId, ct));
    }

    [HttpPut("roles/{roleCode}/permissions")]
    public async Task<IActionResult> ReplacePermissions(string roleCode, ReplaceRolePermissionsRequest request,
        CancellationToken ct)
    {
        if (!TryActor(out var actorId)) return Unauthorized();
        return ToMutationResult(await staff.ReplacePermissionsAsync(roleCode, request, actorId, ct));
    }

    private bool TryActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private IActionResult ToMutationResult(Sora.Application.ServiceResult<bool> result)
    {
        if (result.Succeeded) return NoContent();
        return result.ErrorCode switch
        {
            "staff_or_role_not_found" => NotFound(new { code = result.ErrorCode }),
            "last_administrator_protected" or "administrator_permission_required" =>
                Conflict(new { code = result.ErrorCode }),
            _ => BadRequest(new { code = result.ErrorCode })
        };
    }
}
