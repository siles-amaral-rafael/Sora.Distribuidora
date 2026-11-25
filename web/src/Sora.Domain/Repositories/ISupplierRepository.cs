using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public interface ISupplierRepository
{
    Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken);
    Task<Supplier?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken);
    Task<bool> ReplaceProductsAsync(Guid supplierId, IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
