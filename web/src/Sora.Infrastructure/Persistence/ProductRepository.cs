using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class ProductRepository(SoraDbContext dbContext) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Products.AsNoTracking().Include(x => x.Presentations).Include(x => x.DiscountRules)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Product?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Products.Include(x => x.Presentations).Include(x => x.DiscountRules)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> InternalCodeExistsAsync(string code, Guid? exceptProductId, CancellationToken cancellationToken) =>
        dbContext.Products.AnyAsync(x => x.InternalCode.ToUpper() == code &&
            (exceptProductId == null || x.Id != exceptProductId), cancellationToken);

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? exceptProductId, CancellationToken cancellationToken) =>
        dbContext.Products.AnyAsync(x => x.Barcode == barcode &&
            (exceptProductId == null || x.Id != exceptProductId), cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddManyAsync(IReadOnlyList<Product> products, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddPresentationAsync(ProductPresentation presentation, CancellationToken cancellationToken)
    {
        dbContext.ProductPresentations.Add(presentation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ReplaceDiscountRulesAsync(Guid productId,
        IReadOnlyList<ProductDiscountRule> rules, Guid actorId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var product = await dbContext.Products.Include(x => x.DiscountRules)
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null) return false;
        var before = product.DiscountRules.OrderBy(x => x.MinimumQuantityUnits)
            .Select(x => new { x.MinimumQuantityUnits, x.DiscountPercent }).ToArray();
        dbContext.ProductDiscountRules.RemoveRange(product.DiscountRules);
        product.DiscountRules.Clear();
        foreach (var rule in rules)
        {
            product.DiscountRules.Add(rule);
            dbContext.ProductDiscountRules.Add(rule);
        }
        dbContext.StaffAuditEntries.Add(new StaffAuditEntry(actorId, "product.discount_rules_changed",
            "product", productId.ToString(), JsonSerializer.Serialize(new
            {
                ProductCode = product.InternalCode,
                Before = before,
                After = rules.OrderBy(x => x.MinimumQuantityUnits)
                    .Select(x => new { x.MinimumQuantityUnits, x.DiscountPercent }).ToArray()
            }), now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
