using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sora.Api.Auth;
using Sora.Infrastructure.Persistence;
using Sora.Shared.Notifications;

namespace Sora.Api.Controllers;

[ApiController, Authorize(Policy = PermissionPolicies.NotificationsManage)]
[Route("api/notifications/outbox")]
public sealed class NotificationsController(SoraDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<NotificationOutboxResponse>> List(CancellationToken ct) =>
        await db.NotificationOutbox.AsNoTracking().Include(x => x.Order).Include(x => x.Customer)
            .OrderByDescending(x => x.CreatedAtUtc).Take(300)
            .Select(x => new NotificationOutboxResponse(x.Id, x.OrderId, x.Order.OrderNumber,
                x.Customer.FullName, x.RecipientPhone, x.EventType, x.Message, x.State, x.Mode,
                x.Attempts, x.RetryCount, x.LastError, x.CreatedAtUtc, x.ProcessedAtUtc,
                x.LastRetryAtUtc)).ToListAsync(ct);

    [HttpPost("{id:guid}/simulate-send")]
    public async Task<IActionResult> SimulateSend(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var entry = await db.NotificationOutbox.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null) return NotFound(new { code = "notification_not_found" });
        if (entry.State != "PENDING") return Conflict(new { code = "notification_already_processed" });
        entry.MarkSimulated(actorId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/simulate-failure")]
    public async Task<IActionResult> SimulateFailure(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var entry = await db.NotificationOutbox.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null) return NotFound(new { code = "notification_not_found" });
        if (entry.State != "PENDING") return Conflict(new { code = "notification_not_pending" });
        entry.MarkSimulatedFailure(actorId, clock.GetUtcNow(), "Fallo de procesamiento interno simulado; no se intentó contactar un servicio externo.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var entry = await db.NotificationOutbox.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null) return NotFound(new { code = "notification_not_found" });
        if (!entry.RequeueForRetry(actorId, clock.GetUtcNow())) return Conflict(new { code = "notification_not_failed" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
