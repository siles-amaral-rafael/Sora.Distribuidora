using Sora.Api.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sora.Application.Dispatches;
using Sora.Infrastructure.Persistence;
using Sora.Shared.Dispatches;

namespace Sora.Api.Controllers;

[ApiController, Authorize]
[Route("api/dispatches")]
public sealed class DispatchesController(IDispatchService dispatches, SoraDbContext db) : ControllerBase
{
    [Authorize(Policy = PermissionPolicies.DispatchesCreate)]
    [HttpGet("drivers")]
    public async Task<IActionResult> Drivers([FromQuery] string? query, CancellationToken ct)
    {
        var search = query?.Trim();
        var drivers = db.StaffAccounts.AsNoTracking().Where(x => x.IsActive &&
            x.RoleAssignments.Any(r => r.RoleCode == "DRIVER"));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var name = search.ToLowerInvariant();
            var id = Guid.TryParse(search, out var parsedId) ? parsedId : (Guid?)null;
            drivers = drivers.Where(x => x.FullName.ToLower().Contains(name) || x.Email.ToLower().Contains(name) ||
                (id.HasValue && x.Id == id.Value));
        }
        var result = await drivers.OrderBy(x => x.FullName).Take(15)
            .Select(x => new DispatchDriverResponse(x.Id, x.FullName, x.Email)).ToListAsync(ct);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicies.DispatchesRead)]
    [HttpGet("assigned")]
    public async Task<IActionResult> Assigned(CancellationToken ct)
    {
        Guid? driverId = null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentStaffId)) return Unauthorized();
        var driverScoped = await db.StaffAccounts.AsNoTracking().AnyAsync(x => x.Id == currentStaffId && x.IsActive &&
            x.RoleAssignments.Any(r => r.RoleCode == "DRIVER") &&
            !x.RoleAssignments.Any(r => r.Role.Permissions.Any(p => p.PermissionCode == PermissionPolicies.DispatchesCreate ||
                p.PermissionCode == PermissionPolicies.DispatchesInspect)), ct);
        if (driverScoped)
        {
            driverId = currentStaffId;
        }
        return Ok(await dispatches.ListAssignedOpenDeliveriesAsync(driverId, ct));
    }

    [Authorize(Policy = PermissionPolicies.DispatchesInspect)]
    [HttpGet("pending-inspections")]
    public async Task<IActionResult> PendingInspections(CancellationToken ct) =>
        Ok(await dispatches.ListPendingInspectionsAsync(ct));

    [Authorize(Policy = PermissionPolicies.DispatchesInspect)]
    [HttpPost("orders/{dispatchOrderId:guid}/inspection")]
    public async Task<IActionResult> Inspect(Guid dispatchOrderId, InspectRejectedDeliveryRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var inspectorId)) return Unauthorized();
        var result = await dispatches.InspectRejectedAsync(dispatchOrderId, request, inspectorId, ct);
        if (!result.Succeeded)
            return result.ErrorCode == "dispatch_order_not_found" ? NotFound(new { code = result.ErrorCode }) :
                Conflict(new { code = result.ErrorCode });
        return NoContent();
    }

    [Authorize(Policy = PermissionPolicies.DispatchesCreate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateDispatchRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var leadId)) return Unauthorized();
        var result = await dispatches.CreateAsync(request, leadId, ct);
        if (!result.Succeeded)
            return result.ErrorCode is "order_not_found" ? NotFound(new { code = result.ErrorCode }) :
                result.ErrorCode is "order_not_ready_or_stock_changed" ? Conflict(new { code = result.ErrorCode }) :
                BadRequest(new { code = result.ErrorCode });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Policy = PermissionPolicies.DispatchesResult)]
    [HttpPost("orders/{dispatchOrderId:guid}/result")]
    public async Task<IActionResult> RecordResult(Guid dispatchOrderId, RecordDeliveryResultRequest request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await dispatches.RecordResultAsync(dispatchOrderId, request, actorId, ct);
        if (!result.Succeeded)
            return result.ErrorCode == "dispatch_order_not_found" ? NotFound(new { code = result.ErrorCode }) :
                result.ErrorCode == "delivery_result_already_recorded" ? Conflict(new { code = result.ErrorCode }) :
                BadRequest(new { code = result.ErrorCode });
        return NoContent();
    }
}
