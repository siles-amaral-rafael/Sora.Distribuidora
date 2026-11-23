using Sora.Api.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sora.Application.Orders;
using Sora.Shared.Orders;

namespace Sora.Api.Controllers;

[ApiController, Authorize]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    [Authorize(Policy = PermissionPolicies.OrdersRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await orders.ListAsync(ct));

    [Authorize(Policy = PermissionPolicies.OrdersRead)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        (await orders.GetAsync(id, ct)) is { } order ? Ok(order) : NotFound();

    [Authorize(Policy = PermissionPolicies.OrdersWrite)]
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await orders.CreateAsync(request, actorId, ct);
        if (!result.Succeeded)
        {
            return result.ErrorCode switch
            {
                "customer_not_found" or "product_not_found_or_inactive" => NotFound(new { code = result.ErrorCode }),
                "insufficient_stock" or "sale_below_lot_cost" or "customer_outstanding_balance" => Conflict(new { code = result.ErrorCode }),
                _ => BadRequest(new { code = result.ErrorCode })
            };
        }
        if (result.Value!.Status == "PENDING_CONFIRMATION")
            return AcceptedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [Authorize(Policy = PermissionPolicies.PaymentsRecord)]
    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(Guid id, RecordPaymentRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await orders.RecordPaymentAsync(id, request, actorId, ct);
        if (!result.Succeeded) return result.ErrorCode == "order_not_found" ? NotFound(new { code = result.ErrorCode }) : Conflict(new { code = result.ErrorCode });
        return Ok(result.Value);
    }

    [Authorize(Policy = PermissionPolicies.OrdersPrepare)]
    [HttpPut("{id:guid}/prepare")]
    public async Task<IActionResult> MarkPrepared(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await orders.MarkPreparedAsync(id, actorId, ct);
        if (!result.Succeeded)
            return result.ErrorCode == "order_not_found" ? NotFound(new { code = result.ErrorCode }) :
                Conflict(new { code = result.ErrorCode });
        return NoContent();
    }

    [Authorize(Policy = PermissionPolicies.OrdersWrite)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOrderRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await orders.UpdateAsync(id, request, actorId, ct);
        if (!result.Succeeded) return MapOrderError(result.ErrorCode);
        return Ok(result.Value);
    }

    [Authorize(Policy = PermissionPolicies.OrdersWrite)]
    [HttpPut("{id:guid}/availability")]
    public async Task<IActionResult> ConfirmAvailability(Guid id, ConfirmOrderAvailabilityRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await orders.ConfirmAvailabilityAsync(id, request, actorId, ct);
        if (!result.Succeeded) return MapOrderError(result.ErrorCode);
        return Ok(result.Value);
    }

    private IActionResult MapOrderError(string? code) => code switch
    {
        "order_not_found" or "customer_not_found" or "product_not_found_or_inactive" => NotFound(new { code }),
        "insufficient_stock" or "sale_below_lot_cost" or "order_transition_not_allowed" => Conflict(new { code }),
        _ => BadRequest(new { code })
    };

    [Authorize(Policy = PermissionPolicies.OrdersPickup)]
    [HttpPut("{id:guid}/pickup")]
    public async Task<IActionResult> CompletePickup(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await orders.CompletePickupAsync(id, actorId, ct);
        if (!result.Succeeded)
            return result.ErrorCode == "order_not_found" ? NotFound(new { code = result.ErrorCode }) :
                Conflict(new { code = result.ErrorCode });
        return NoContent();
    }

    [Authorize(Policy = PermissionPolicies.OrdersCancel)]
    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelOrderRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await orders.CancelAsync(id, request, actorId, ct);
        if (!result.Succeeded)
            return result.ErrorCode == "order_not_found" ? NotFound(new { code = result.ErrorCode }) :
                Conflict(new { code = result.ErrorCode });
        return NoContent();
    }
}
