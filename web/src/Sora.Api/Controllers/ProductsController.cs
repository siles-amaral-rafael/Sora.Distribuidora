using Sora.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sora.Application.Products;
using Sora.Shared.Products;
using System.Security.Claims;

namespace Sora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await products.ListAsync(cancellationToken));

    [Authorize(Policy = PermissionPolicies.ProductsRead)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        return product is null ? NotFound(new { code = "product_not_found" }) : Ok(product);
    }

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var result = await products.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Conflict(new { code = result.ErrorCode });
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpPost("bulk")]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMany(CreateProductsRequest request, CancellationToken cancellationToken)
    {
        var result = await products.CreateManyAsync(request, cancellationToken);
        if (!result.Succeeded)
            return result.ErrorCode == "product_barcode_already_exists"
                ? Conflict(new { code = result.ErrorCode })
                : BadRequest(new { code = result.ErrorCode });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, SaveProductRequest request, CancellationToken cancellationToken)
    {
        var result = await products.UpdateAsync(id, request, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorCode == "product_not_found"
                ? NotFound(new { code = result.ErrorCode })
                : result.ErrorCode!.EndsWith("already_exists", StringComparison.Ordinal)
                    ? Conflict(new { code = result.ErrorCode })
                    : BadRequest(new { code = result.ErrorCode });
        }
        return Ok(result.Value);
    }

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await products.DeactivateAsync(id, cancellationToken)
            ? NoContent()
            : NotFound(new { code = "product_not_found" });

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        await products.ActivateAsync(id, cancellationToken)
            ? NoContent()
            : NotFound(new { code = "product_not_found" });

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpPost("{productId:guid}/presentations")]
    [ProducesResponseType<ProductPresentationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddPresentation(Guid productId, SaveProductPresentationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await products.AddPresentationAsync(productId, request, cancellationToken);
        if (!result.Succeeded)
        {
            return PresentationError(result.ErrorCode!);
        }
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpPut("{productId:guid}/presentations/{presentationId:guid}")]
    [ProducesResponseType<ProductPresentationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePresentation(Guid productId, Guid presentationId,
        SaveProductPresentationRequest request, CancellationToken cancellationToken)
    {
        var result = await products.UpdatePresentationAsync(productId, presentationId, request, cancellationToken);
        if (!result.Succeeded)
        {
            return PresentationError(result.ErrorCode!);
        }
        return Ok(result.Value);
    }

    [Authorize(Policy = PermissionPolicies.ProductsWrite)]
    [HttpDelete("{productId:guid}/presentations/{presentationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePresentation(Guid productId, Guid presentationId,
        CancellationToken cancellationToken) =>
        await products.DeactivatePresentationAsync(productId, presentationId, cancellationToken)
            ? NoContent()
            : NotFound(new { code = "presentation_not_found" });

    [Authorize(Policy = PermissionPolicies.DiscountsManage)]
    [HttpPut("{productId:guid}/discount-rules")]
    public async Task<IActionResult> ReplaceDiscountRules(Guid productId,
        ReplaceProductDiscountRulesRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await products.ReplaceDiscountRulesAsync(productId, request, actorId, cancellationToken);
        if (!result.Succeeded)
            return result.ErrorCode == "product_not_found"
                ? NotFound(new { code = result.ErrorCode })
                : BadRequest(new { code = result.ErrorCode });
        return Ok(result.Value);
    }

    private IActionResult PresentationError(string errorCode) => errorCode switch
    {
        "product_not_found" or "presentation_not_found" => NotFound(new { code = errorCode }),
        "presentation_name_already_exists" => Conflict(new { code = errorCode }),
        _ => BadRequest(new { code = errorCode })
    };
}
