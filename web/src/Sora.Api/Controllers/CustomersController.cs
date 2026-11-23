using Sora.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sora.Application.Customers;
using Sora.Shared.Customers;

namespace Sora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [Authorize(Policy = PermissionPolicies.CustomersRead)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await customers.ListAsync(cancellationToken));

    [Authorize(Policy = PermissionPolicies.CustomersRead)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customers.FindByIdAsync(id, cancellationToken);
        return customer is null ? NotFound(new { code = "customer_not_found" }) : Ok(customer);
    }

    [Authorize(Policy = PermissionPolicies.CustomersWrite)]
    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(SaveCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await customers.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { code = result.ErrorCode });
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = PermissionPolicies.CustomersWrite)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await customers.UpdateAsync(id, request, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorCode == "customer_not_found"
                ? NotFound(new { code = result.ErrorCode })
                : BadRequest(new { code = result.ErrorCode });
        }
        return Ok(result.Value);
    }

    [Authorize(Policy = PermissionPolicies.CustomersWrite)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await customers.DeactivateAsync(id, cancellationToken)
            ? NoContent()
            : NotFound(new { code = "customer_not_found" });
}
