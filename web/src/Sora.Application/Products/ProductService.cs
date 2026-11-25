using Sora.Domain.Entities;
using Sora.Domain.Repositories;
using Sora.Shared.Products;
using System.Security.Cryptography;

namespace Sora.Application.Products;

public sealed class ProductService(IProductRepository products, TimeProvider timeProvider) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await products.ListAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<ProductResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        return product is null ? null : ToResponse(product);
    }

    public async Task<ServiceResult<ProductResponse>> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var code = await ResolveCodeAsync(request.InternalCode, cancellationToken);
        if (code is null) return ServiceResult<ProductResponse>.Failure("product_code_generation_failed");
        var barcode = NormalizeOptional(request.Barcode);
        if (await products.InternalCodeExistsAsync(code, null, cancellationToken))
        {
            return ServiceResult<ProductResponse>.Failure("product_code_already_exists");
        }
        if (barcode is not null && await products.BarcodeExistsAsync(barcode, null, cancellationToken))
        {
            return ServiceResult<ProductResponse>.Failure("product_barcode_already_exists");
        }

        var product = new Product(code, barcode, request.Name.Trim(), NormalizeOptional(request.Brand),
            NormalizeOptional(request.Category), request.NetContent, NormalizeOptional(request.ContentUnit),
            request.BaseUnit.Trim(), request.UnitSalePrice, timeProvider.GetUtcNow());
        await products.AddAsync(product, cancellationToken);
        return ServiceResult<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<ServiceResult<IReadOnlyList<ProductResponse>>> CreateManyAsync(CreateProductsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Products.Count is < 1 or > 50)
            return ServiceResult<IReadOnlyList<ProductResponse>>.Failure("invalid_product_batch");

        var newProducts = new List<Product>(request.Products.Count);
        var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var internalCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Products)
        {
            string? code = null;
            for (var attempt = 0; attempt < 12 && code is null; attempt++)
            {
                var candidate = await ResolveCodeAsync(item.InternalCode, cancellationToken);
                if (candidate is null) break;
                if (internalCodes.Add(candidate)) code = candidate;
                else if (!string.IsNullOrWhiteSpace(item.InternalCode))
                    return ServiceResult<IReadOnlyList<ProductResponse>>.Failure("product_code_already_exists");
            }
            if (code is null)
                return ServiceResult<IReadOnlyList<ProductResponse>>.Failure("product_code_generation_failed");
            var barcode = NormalizeOptional(item.Barcode);
            if (barcode is not null && (!barcodes.Add(barcode) ||
                await products.BarcodeExistsAsync(barcode, null, cancellationToken)))
                return ServiceResult<IReadOnlyList<ProductResponse>>.Failure("product_barcode_already_exists");
            newProducts.Add(new Product(code, barcode, item.Name.Trim(), NormalizeOptional(item.Brand),
                NormalizeOptional(item.Category), item.NetContent, NormalizeOptional(item.ContentUnit),
                item.BaseUnit.Trim(), item.UnitSalePrice, timeProvider.GetUtcNow()));
        }

        await products.AddManyAsync(newProducts, cancellationToken);
        return ServiceResult<IReadOnlyList<ProductResponse>>.Success(newProducts.Select(ToResponse).ToList());
    }

    public async Task<ServiceResult<ProductResponse>> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return ServiceResult<ProductResponse>.Failure("product_not_found");
        }

        var code = string.IsNullOrWhiteSpace(request.InternalCode)
            ? product.InternalCode
            : NormalizeCode(request.InternalCode);
        var barcode = NormalizeOptional(request.Barcode);
        if (await products.InternalCodeExistsAsync(code, id, cancellationToken))
        {
            return ServiceResult<ProductResponse>.Failure("product_code_already_exists");
        }
        if (barcode is not null && await products.BarcodeExistsAsync(barcode, id, cancellationToken))
        {
            return ServiceResult<ProductResponse>.Failure("product_barcode_already_exists");
        }

        product.Update(code, barcode, request.Name.Trim(), NormalizeOptional(request.Brand),
            NormalizeOptional(request.Category), request.NetContent, NormalizeOptional(request.ContentUnit),
            request.BaseUnit.Trim(), request.UnitSalePrice);
        await products.SaveChangesAsync(cancellationToken);
        return ServiceResult<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        product.Deactivate();
        await products.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null) return false;
        product.Activate();
        await products.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ServiceResult<ProductPresentationResponse>> AddPresentationAsync(Guid productId,
        SaveProductPresentationRequest request, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResult<ProductPresentationResponse>.Failure("product_not_found");
        }
        if (product.Presentations.Any(x => string.Equals(x.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult<ProductPresentationResponse>.Failure("presentation_name_already_exists");
        }

        var presentation = product.AddPresentation(request.Name.Trim(), request.UnitsPerPresentation);
        await products.AddPresentationAsync(presentation, cancellationToken);
        return ServiceResult<ProductPresentationResponse>.Success(ToResponse(presentation));
    }

    public async Task<ServiceResult<ProductPresentationResponse>> UpdatePresentationAsync(Guid productId,
        Guid presentationId, SaveProductPresentationRequest request, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResult<ProductPresentationResponse>.Failure("product_not_found");
        }
        var presentation = product.Presentations.SingleOrDefault(x => x.Id == presentationId);
        if (presentation is null)
        {
            return ServiceResult<ProductPresentationResponse>.Failure("presentation_not_found");
        }
        if (product.Presentations.Any(x => x.Id != presentationId &&
            string.Equals(x.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult<ProductPresentationResponse>.Failure("presentation_name_already_exists");
        }

        presentation.Update(request.Name.Trim(), request.UnitsPerPresentation);
        await products.SaveChangesAsync(cancellationToken);
        return ServiceResult<ProductPresentationResponse>.Success(ToResponse(presentation));
    }

    public async Task<bool> DeactivatePresentationAsync(Guid productId, Guid presentationId, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(productId, cancellationToken);
        var presentation = product?.Presentations.SingleOrDefault(x => x.Id == presentationId);
        if (presentation is null)
        {
            return false;
        }

        presentation.Deactivate();
        await products.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ServiceResult<ProductResponse>> ReplaceDiscountRulesAsync(Guid productId,
        ReplaceProductDiscountRulesRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        if (request.Rules.Any(x => x.MinimumQuantityUnits <= 0 ||
                x.DiscountPercent is <= 0 or > 100) ||
            request.Rules.Select(x => x.MinimumQuantityUnits).Distinct().Count() != request.Rules.Count)
            return ServiceResult<ProductResponse>.Failure("invalid_discount_rules");
        var current = await products.FindByIdAsync(productId, cancellationToken);
        if (current is null) return ServiceResult<ProductResponse>.Failure("product_not_found");
        var rules = request.Rules.Select(x => new ProductDiscountRule(productId,
            x.MinimumQuantityUnits, x.DiscountPercent)).ToArray();
        if (!await products.ReplaceDiscountRulesAsync(productId, rules, actorId,
                timeProvider.GetUtcNow(), cancellationToken))
            return ServiceResult<ProductResponse>.Failure("product_not_found");
        var saved = await products.FindByIdAsync(productId, cancellationToken);
        return saved is null ? ServiceResult<ProductResponse>.Failure("product_not_found")
            : ServiceResult<ProductResponse>.Success(ToResponse(saved));
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<string?> ResolveCodeAsync(string? requested, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var supplied = NormalizeCode(requested);
            return await products.InternalCodeExistsAsync(supplied, null, cancellationToken) ? null : supplied;
        }

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var generated = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000).ToString();
            if (!await products.InternalCodeExistsAsync(generated, null, cancellationToken)) return generated;
        }
        return null;
    }

    private static ProductResponse ToResponse(Product product) => new(
        product.Id, product.InternalCode, product.Barcode, product.Name, product.Brand,
        product.Category, product.NetContent, product.ContentUnit, product.BaseUnit,
        product.UnitSalePrice, product.IsActive, product.CreatedAtUtc,
        product.Presentations.OrderBy(x => x.Name).Select(ToResponse).ToList(),
        product.DiscountRules.OrderBy(x => x.MinimumQuantityUnits)
            .Select(x => new ProductDiscountRuleResponse(x.MinimumQuantityUnits, x.DiscountPercent)).ToArray());

    private static ProductPresentationResponse ToResponse(ProductPresentation presentation) => new(
        presentation.Id, presentation.Name, presentation.UnitsPerPresentation, presentation.IsActive);
}
