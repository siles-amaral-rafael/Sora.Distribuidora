using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class SupplierRepository(SoraDbContext dbContext) : ISupplierRepository
{
    public async Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Suppliers.AsNoTracking().Include(x => x.ProductLinks).ThenInclude(x => x.Product)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Supplier?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Suppliers.Include(x => x.ProductLinks).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ReplaceProductsAsync(Guid supplierId, IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var supplier = await dbContext.Suppliers.Include(x => x.ProductLinks)
            .SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
        if (supplier is null) return false;
        if (productIds.Count > 0)
        {
            var activeCount = await dbContext.Products.CountAsync(x => productIds.Contains(x.Id) && x.IsActive,
                cancellationToken);
            if (activeCount != productIds.Count) return false;
        }
        var requestedIds = productIds.ToHashSet();
        var existingIds = supplier.ProductLinks.Select(x => x.ProductId).ToHashSet();
        dbContext.SupplierProducts.RemoveRange(supplier.ProductLinks
            .Where(link => !requestedIds.Contains(link.ProductId)));
        foreach (var productId in requestedIds.Except(existingIds))
            dbContext.SupplierProducts.Add(new SupplierProduct(supplierId, productId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
