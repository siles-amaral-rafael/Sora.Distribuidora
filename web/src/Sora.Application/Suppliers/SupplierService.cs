using Sora.Domain.Entities;
using Sora.Domain.Repositories;
using Sora.Shared.Suppliers;

namespace Sora.Application.Suppliers;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierResponse>> ListAsync(CancellationToken cancellationToken);
    Task<SupplierResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SupplierResponse> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken);
    Task<SupplierResponse?> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class SupplierService(ISupplierRepository suppliers) : ISupplierService
{
    public async Task<IReadOnlyList<SupplierResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await suppliers.ListAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<SupplierResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.FindByIdAsync(id, cancellationToken);
        return supplier is null ? null : ToResponse(supplier);
    }

    public async Task<SupplierResponse> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = new Supplier(request.Name.Trim(), request.Phone.Trim());
        await suppliers.AddAsync(supplier, cancellationToken);
        return ToResponse(supplier);
    }

    public async Task<SupplierResponse?> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.FindByIdAsync(id, cancellationToken);
        if (supplier is null) return null;
        supplier.Update(request.Name.Trim(), request.Phone.Trim());
        await suppliers.SaveChangesAsync(cancellationToken);
        return ToResponse(supplier);
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.FindByIdAsync(id, cancellationToken);
        if (supplier is null) return false;
        supplier.Deactivate();
        await suppliers.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SupplierResponse ToResponse(Supplier x) => new(x.Id, x.Name, x.Phone, x.IsActive);
}
