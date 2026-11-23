using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class SupplierRepository(SoraDbContext dbContext) : ISupplierRepository
{
    public async Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Supplier?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
