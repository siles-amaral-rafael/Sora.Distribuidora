using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);
    Task<Product?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> InternalCodeExistsAsync(string code, Guid? exceptProductId, CancellationToken cancellationToken);
    Task<bool> BarcodeExistsAsync(string barcode, Guid? exceptProductId, CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task AddManyAsync(IReadOnlyList<Product> products, CancellationToken cancellationToken);
    Task AddPresentationAsync(ProductPresentation presentation, CancellationToken cancellationToken);
    Task<bool> ReplaceDiscountRulesAsync(Guid productId, IReadOnlyList<ProductDiscountRule> rules,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
