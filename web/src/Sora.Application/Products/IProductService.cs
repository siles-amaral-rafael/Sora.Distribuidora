using Sora.Shared.Products;

namespace Sora.Application.Products;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ProductResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<ProductResponse>> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<IReadOnlyList<ProductResponse>>> CreateManyAsync(CreateProductsRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<ProductResponse>> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<ProductPresentationResponse>> AddPresentationAsync(Guid productId, SaveProductPresentationRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<ProductPresentationResponse>> UpdatePresentationAsync(Guid productId, Guid presentationId, SaveProductPresentationRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivatePresentationAsync(Guid productId, Guid presentationId, CancellationToken cancellationToken);
    Task<ServiceResult<ProductResponse>> ReplaceDiscountRulesAsync(Guid productId,
        ReplaceProductDiscountRulesRequest request, Guid actorId, CancellationToken cancellationToken);
}
