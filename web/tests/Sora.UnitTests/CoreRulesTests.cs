using Moq;
using Sora.Application.Auth;
using Sora.Application.Customers;
using Sora.Application.Dispatches;
using Sora.Application.Inventory;
using Sora.Application.Orders;
using Sora.Application.Products;
using Sora.Application.Suppliers;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;
using Sora.Domain.Security;
using Sora.Shared.Auth;
using Sora.Shared.Customers;
using Sora.Shared.Dispatches;
using Sora.Shared.Inventory;
using Sora.Shared.Orders;
using Sora.Shared.Products;
using Sora.Shared.Suppliers;
using Xunit;

namespace Sora.UnitTests;

public sealed class CoreRulesTests
{
    [Fact(DisplayName = "UT-FEFO-01 · asignar antes el lote con vencimiento próximo")]
    public void Fefo_uses_earliest_expiry_and_spills_to_next_lot()
    {
        var early = Guid.NewGuid();
        var late = Guid.NewGuid();
        var plan = OrderPricingPolicy.PlanFefo(12,
        [new(late, new DateOnly(2027, 4, 1), 20, 8m), new(early, new DateOnly(2027, 1, 1), 8, 8m)]);
        Assert.Equal(new PlannedLotAllocation(early, 8), plan.Allocations[0]);
        Assert.Equal(new PlannedLotAllocation(late, 4), plan.Allocations[1]);
        Assert.Equal(0, plan.MissingUnits);
    }

    [Theory(DisplayName = "UT-STK-01 · límites de disponibilidad exacta, menos y más")]
    [InlineData(10, 10, 0)]
    [InlineData(11, 10, 1)]
    [InlineData(9, 10, 0)]
    public void Fefo_reports_missing_units_at_stock_boundary(int requested, int available, int expectedMissing)
    {
        var plan = OrderPricingPolicy.PlanFefo(requested, [new(Guid.NewGuid(), null, available, 4m)]);
        Assert.Equal(expectedMissing, plan.MissingUnits);
    }

    [Theory(DisplayName = "UT-DIS-01 · respetar límite de descuento por margen")]
    [InlineData(10, 8, 80, 0.40)]
    [InlineData(10, 8, 10, 0.40)]
    [InlineData(10, 10, 80, 0)]
    [InlineData(8, 10, 80, 0)]
    public void Discount_never_exceeds_twenty_percent_of_positive_margin(
        decimal subtotal, decimal cost, decimal requestedPercent, decimal expected)
    {
        Assert.Equal(expected, OrderPricingPolicy.CalculateDiscount(subtotal, cost, requestedPercent));
    }

    [Fact(DisplayName = "UT-STK-02 · impedir saldo negativo y conservar reservado")]
    public void Inventory_balance_allows_exact_available_stock_and_rejects_one_more()
    {
        var balance = new InventoryLotBalance(Guid.NewGuid(), 10, 4);
        Assert.True(balance.TryChangeReserved(6));
        Assert.False(balance.TryChangeReserved(1));
        Assert.Equal(10, balance.ReservedUnits);
        var adjusted = new InventoryLotBalance(Guid.NewGuid(), 10, 4);
        Assert.False(adjusted.TryAdjustOnHand(-7));
        Assert.True(adjusted.TryAdjustOnHand(-6));
    }

    [Fact(DisplayName = "UT-ACL-01 · catálogo de permisos y rol de repartidor")]
    public void Permission_catalog_has_unique_codes_and_limits_driver_access()
    {
        Assert.Equal(PermissionCatalog.All.Length,
            PermissionCatalog.All.Select(p => p.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("dispatches.result", PermissionCatalog.DefaultsByRole["DRIVER"]);
        Assert.DoesNotContain("orders.write", PermissionCatalog.DefaultsByRole["DRIVER"]);
        Assert.Contains("staff.manage", PermissionCatalog.DefaultsByRole["ADMIN"]);
    }

    [Fact(DisplayName = "UT-AUTH-01 · normalizar correo y rechazar cuenta inactiva")]
    public async Task Login_normalizes_email_and_rejects_inactive_accounts()
    {
        var repo = new Mock<IStaffAccountRepository>(MockBehavior.Strict);
        var tokenIssuer = new Mock<ITokenIssuer>(MockBehavior.Strict);
        var account = new StaffAccount("QA User", "USER@SORA.TEST", BCrypt.Net.BCrypt.HashPassword("valid-password"),
            DateTimeOffset.UtcNow);
        repo.Setup(x => x.FindByEmailAsync("USER@SORA.TEST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        tokenIssuer.Setup(x => x.Issue(account)).Returns(new IssuedToken("test-jwt", DateTimeOffset.UtcNow.AddMinutes(5)));
        var service = new AuthService(repo.Object, tokenIssuer.Object, TimeProvider.System);

        var success = await service.LoginAsync(new LoginRequest { Email = " user@sora.test ", Password = "valid-password" }, default);
        Assert.True(success.Succeeded);
        Assert.Equal("test-jwt", success.Token?.Value);

        account.SetActive(false);
        var rejected = await service.LoginAsync(new LoginRequest { Email = "USER@sora.test", Password = "valid-password" }, default);
        Assert.False(rejected.Succeeded);
        Assert.Equal("invalid_credentials", rejected.ErrorCode);
    }

    [Fact(DisplayName = "UT-ORD-01 · rechazar cantidades inválidas antes del repositorio")]
    public async Task Order_service_rejects_invalid_quantities_before_repository_call()
    {
        var repo = new Mock<IOrderRepository>(MockBehavior.Strict);
        var service = new OrderService(repo.Object, TimeProvider.System);
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(), Fulfillment = "DELIVERY",
            Lines = [new CreateOrderLineRequest { ProductId = Guid.NewGuid(), Quantity = 0 }]
        };
        var result = await service.CreateAsync(request, Guid.NewGuid(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_order", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-CLI-01 · coordenadas parciales se rechazan")]
    public async Task Customer_service_requires_both_coordinates_or_neither()
    {
        var repo = new Mock<ICustomerRepository>(MockBehavior.Strict);
        var service = new CustomerService(repo.Object, TimeProvider.System);
        var result = await service.CreateAsync(new SaveCustomerRequest
        {
            CustomerType = "FAMILY", FullName = "Cliente QA", Phone = "1234567",
            PrimaryAddress = "Dirección ficticia", Latitude = -17.7m
        }, default);
        Assert.False(result.Succeeded);
        Assert.Equal("coordinates_must_be_provided_together", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-PRD-01 · lote masivo fuera del rango permitido")]
    public async Task Product_service_rejects_empty_bulk_without_repository_call()
    {
        var repo = new Mock<IProductRepository>(MockBehavior.Strict);
        var service = new ProductService(repo.Object, TimeProvider.System);
        var result = await service.CreateManyAsync(new CreateProductsRequest(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_product_batch", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-INV-01 · no aceptar recepción sin proveedor válido")]
    public async Task Inventory_service_rejects_missing_supplier_before_persistence()
    {
        var repo = new Mock<IInventoryRepository>(MockBehavior.Strict);
        var service = new InventoryService(repo.Object, TimeProvider.System);
        var result = await service.ReceiveAsync(new CreatePurchaseReceiptRequest(), Guid.NewGuid(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_reference", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-SUP-01 · no duplicar productos asociados a proveedor")]
    public async Task Supplier_service_rejects_duplicate_product_links()
    {
        var repo = new Mock<ISupplierRepository>(MockBehavior.Strict);
        var service = new SupplierService(repo.Object);
        var product = Guid.NewGuid();
        var result = await service.ReplaceProductsAsync(Guid.NewGuid(),
            new ReplaceSupplierProductsRequest { ProductIds = [product, product] }, default);
        Assert.False(result.Succeeded);
        Assert.Equal("duplicate_supplier_product", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-DSP-01 · no repetir pedidos en la misma salida")]
    public async Task Dispatch_service_rejects_duplicate_orders_before_repository_call()
    {
        var repo = new Mock<IDispatchRepository>(MockBehavior.Strict);
        var service = new DispatchService(repo.Object, TimeProvider.System);
        var order = Guid.NewGuid();
        var result = await service.CreateAsync(new CreateDispatchRequest
        {
            VehicleIdentifier = "QA-01", DriverStaffId = Guid.NewGuid(), OrderIds = [order, order]
        }, Guid.NewGuid(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_dispatch", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-STAFF-01 · cuenta administrativa inválida no se persiste")]
    public async Task Staff_service_rejects_invalid_account_before_repository_call()
    {
        var repo = new Mock<IStaffAccountRepository>(MockBehavior.Strict);
        var service = new StaffAdministrationService(repo.Object, TimeProvider.System);
        var result = await service.CreateAsync(new CreateStaffAccountRequest
        {
            FullName = "A", Email = "bad", TemporaryPassword = "too-short", RoleCodes = []
        }, Guid.NewGuid(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_staff_account", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-PRD-02 · bloquear código interno duplicado")]
    public async Task Product_service_rejects_duplicate_internal_code()
    {
        var repo = new Mock<IProductRepository>(MockBehavior.Strict);
        repo.Setup(x => x.InternalCodeExistsAsync("SORA-DUP-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ProductService(repo.Object, TimeProvider.System);
        var result = await service.CreateAsync(new SaveProductRequest
        {
            InternalCode = "sora-dup-1", Name = "Producto QA", BaseUnit = "unidad", UnitSalePrice = 10
        }, default);
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorCode));
        repo.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UT-INV-02 · rechazar lote vencido")]
    public async Task Inventory_service_rejects_expired_lot_before_receipt_number()
    {
        var repo = new Mock<IInventoryRepository>(MockBehavior.Strict);
        var supplierId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        repo.Setup(x => x.SupplierIsActiveAsync(supplierId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(x => x.GetActiveProductIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { productId });
        var service = new InventoryService(repo.Object, TimeProvider.System);
        var result = await service.ReceiveAsync(new CreatePurchaseReceiptRequest
        {
            SupplierId = supplierId,
            Lines = [new PurchaseReceiptLineRequest { ProductId = productId, LotCode = "QA-EXPIRED", ExpiryDate = DateOnly.MinValue, Quantity = 1 }]
        }, Guid.NewGuid(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("received_lot_is_expired", result.ErrorCode);
        repo.Verify(x => x.GetNextReceiptNumberAsync(It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(x => x.ReceiveAsync(It.IsAny<PurchaseReceipt>(), It.IsAny<IReadOnlyList<InventoryLot>>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UT-DSP-02 · firma base64 inválida se rechaza")]
    public async Task Dispatch_service_rejects_malformed_signature_before_repository_call()
    {
        var repo = new Mock<IDispatchRepository>(MockBehavior.Strict);
        var service = new DispatchService(repo.Object, TimeProvider.System);
        var result = await service.RecordResultAsync(Guid.NewGuid(), new RecordDeliveryResultRequest
        {
            Result = "DELIVERED", RecipientName = "Receptor QA", SignatureBase64 = "%%%",
            SignatureMimeType = "image/png"
        }, Guid.NewGuid(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_signature", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UT-STAFF-02 · impedir quitar permiso crítico de ADMIN")]
    public async Task Staff_service_protects_administrator_management_permission()
    {
        var repo = new Mock<IStaffAccountRepository>(MockBehavior.Strict);
        var service = new StaffAdministrationService(repo.Object, TimeProvider.System);
        var result = await service.ReplacePermissionsAsync("ADMIN",
            new ReplaceRolePermissionsRequest { PermissionCodes = ["orders.read"] }, Guid.NewGuid(), default);
        Assert.False(result.Succeeded);
        Assert.Equal("administrator_permission_required", result.ErrorCode);
        repo.VerifyNoOtherCalls();
    }
}
