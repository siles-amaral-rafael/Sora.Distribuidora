using Sora.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sora.Application.Suppliers;
using Sora.Shared.Suppliers;

namespace Sora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers")]
public sealed class SuppliersController(ISupplierService suppliers) : ControllerBase
{
    [Authorize(Policy = PermissionPolicies.SuppliersRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await suppliers.ListAsync(cancellationToken));

    [Authorize(Policy = PermissionPolicies.SuppliersRead)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.FindByIdAsync(id, cancellationToken);
        return supplier is null ? NotFound(new { code = "supplier_not_found" }) : Ok(supplier);
    }

    [Authorize(Policy = PermissionPolicies.SuppliersWrite)]
    [HttpPost]
    public async Task<IActionResult> Create(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
    }

    [Authorize(Policy = PermissionPolicies.SuppliersWrite)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.UpdateAsync(id, request, cancellationToken);
        return supplier is null ? NotFound(new { code = "supplier_not_found" }) : Ok(supplier);
    }

    [Authorize(Policy = PermissionPolicies.SuppliersWrite)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await suppliers.DeactivateAsync(id, cancellationToken)
            ? NoContent()
            : NotFound(new { code = "supplier_not_found" });
}
