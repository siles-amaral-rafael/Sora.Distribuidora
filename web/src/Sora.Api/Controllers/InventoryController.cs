using Sora.Api.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sora.Application.Inventory;
using Sora.Shared.Inventory;

namespace Sora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryService inventory) : ControllerBase
{
    [Authorize(Policy = PermissionPolicies.InventoryRead)]
    [HttpGet("stock")]
    [ProducesResponseType<IReadOnlyList<InventoryStockLotResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStock([FromQuery] Guid? productId, CancellationToken cancellationToken) =>
        Ok(await inventory.GetStockAsync(productId, cancellationToken));

    [Authorize(Policy = PermissionPolicies.InventoryWrite)]
    [HttpPost("receipts")]
    [ProducesResponseType<PurchaseReceiptResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Receive(CreatePurchaseReceiptRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();

        var result = await inventory.ReceiveAsync(request, actorId, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorCode switch
            {
                "supplier_not_found" or "product_not_found_or_inactive" => NotFound(new { code = result.ErrorCode }),
                "invoice_already_registered" or "duplicate_lot_line" => Conflict(new { code = result.ErrorCode }),
                _ => BadRequest(new { code = result.ErrorCode })
            };
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Policy = PermissionPolicies.InventoryWrite)]
    [HttpPost("adjustments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Adjust(AdjustStockRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();

        var result = await inventory.AdjustStockAsync(request, actorId, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorCode switch
            {
                "lot_not_found" => NotFound(new { code = result.ErrorCode }),
                "adjustment_would_consume_reserved_stock" => Conflict(new { code = result.ErrorCode }),
                _ => BadRequest(new { code = result.ErrorCode })
            };
        }
        return NoContent();
    }

    private bool TryGetActorId(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
}
