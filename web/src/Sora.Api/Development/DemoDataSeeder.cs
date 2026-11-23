using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Infrastructure.Persistence;

namespace Sora.Api.Development;

public static class DemoDataSeeder
{
    public const string DemoPassword = "SoraDemo2026!";

    private sealed record StaffFixture(string Name, string Email, string Role);
    private sealed record CustomerFixture(string Type, string Name, string Phone, string Address,
        decimal Latitude, decimal Longitude);
    private sealed record ProductFixture(string Code, string Name, string Brand, string Category,
        decimal Content, string ContentUnit, decimal Price, decimal Cost);

    private static readonly StaffFixture[] Staff =
    [
        new("Demo Administrador", "admin@sora.demo", "ADMIN"),
        new("Demo Vendedor", "vendedor@sora.demo", "SELLER"),
        new("Demo Almacén", "almacen@sora.demo", "WAREHOUSE"),
        new("Demo Jefe de despacho", "despacho@sora.demo", "DISPATCH_LEAD"),
        new("Demo Ayudante", "ayudante@sora.demo", "DISPATCH_ASSISTANT"),
        new("Demo Repartidor", "repartidor@sora.demo", "DRIVER")
    ];

    private static readonly CustomerFixture[] Customers =
    [
        new("STORE", "DEMO - Tienda Los Pinos", "DEMO-70000001", "Dirección ficticia - Zona Norte, Santa Cruz", -17.738100m, -63.170200m),
        new("STORE", "DEMO - Abarrotes La Esquina", "DEMO-70000002", "Dirección ficticia - Av. Alemana, Santa Cruz", -17.748300m, -63.164100m),
        new("STORE", "DEMO - Mini Mercado Central", "DEMO-70000003", "Dirección ficticia - Zona Norte, Santa Cruz", -17.729200m, -63.176500m),
        new("FAMILY", "DEMO - Familia Rojas", "DEMO-70000004", "Dirección ficticia - Barrio Norte, Santa Cruz", -17.735600m, -63.182300m),
        new("FAMILY", "DEMO - Familia Vargas", "DEMO-70000005", "Dirección ficticia - Barrio Los Sauces, Santa Cruz", -17.753200m, -63.185400m),
        new("FAMILY", "DEMO - Familia Suárez", "DEMO-70000006", "Dirección ficticia - Zona Norte, Santa Cruz", -17.721900m, -63.158700m),
        new("INSTITUTION", "DEMO - Comedor Comunitario", "DEMO-70000007", "Dirección ficticia - Santa Cruz", -17.743000m, -63.173000m),
        new("INSTITUTION", "DEMO - Centro Educativo", "DEMO-70000008", "Dirección ficticia - Santa Cruz", -17.760100m, -63.169900m)
    ];

    private static readonly ProductFixture[] Products =
    [
        new("DEMO-ARZ-001", "Arroz grano de oro", "Grano Norte", "Granos", 5m, "kg", 10m, 7.50m),
        new("DEMO-FID-001", "Fideo tallarín", "Doña Pasta", "Pastas", 400m, "g", 8m, 5.50m),
        new("DEMO-AZU-001", "Azúcar blanca", "Dulce Valle", "Abarrotes", 1m, "kg", 9.50m, 7m),
        new("DEMO-ACE-001", "Aceite vegetal", "Sol Dorado", "Aceites", 1m, "L", 15m, 12m),
        new("DEMO-HAR-001", "Harina de trigo", "Molino Real", "Abarrotes", 1m, "kg", 8.50m, 6.70m),
        new("DEMO-FRI-001", "Frejol rojo", "Grano Norte", "Granos", 1m, "kg", 13m, 10m),
        new("DEMO-SAL-001", "Sal yodada", "Sal Andina", "Abarrotes", 1m, "kg", 5.50m, 3.80m),
        new("DEMO-LEN-001", "Lenteja", "Grano Norte", "Granos", 1m, "kg", 18m, 14m),
        new("DEMO-AVE-001", "Avena", "Campo Verde", "Cereales", 500m, "g", 12m, 9m),
        new("DEMO-LEC-001", "Leche en polvo", "Lácteos del Valle", "Lácteos", 400m, "g", 25m, 20m),
        new("DEMO-FID-002", "Fideo corbatita", "Doña Pasta", "Pastas", 250m, "g", 5m, 3.20m),
        new("DEMO-ATU-001", "Atún en agua", "Mar Azul", "Enlatados", 170m, "g", 12m, 9m)
    ];

    public static async Task SeedAsync(SoraDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword, 12);
        var accounts = new Dictionary<string, StaffAccount>(StringComparer.OrdinalIgnoreCase);

        foreach (var fixture in Staff)
        {
            var email = fixture.Email.ToUpperInvariant();
            var account = await db.StaffAccounts.Include(x => x.RoleAssignments)
                .SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
            if (account is null)
            {
                account = new StaffAccount(fixture.Name, email, passwordHash, now);
                db.StaffAccounts.Add(account);
            }
            if (!account.RoleAssignments.Any(x => x.RoleCode == fixture.Role))
                account.AssignRole(fixture.Role, now);
            accounts[fixture.Role] = account;
        }

        if (await db.Products.AnyAsync(x => x.InternalCode == "DEMO-ARZ-001", cancellationToken))
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var customers = Customers.Select(x => new Customer(x.Type, x.Name, x.Phone, x.Address,
            x.Latitude, x.Longitude, now)).ToArray();
        var products = Products.Select(x => new Product(x.Code, null, x.Name, x.Brand, x.Category,
            x.Content, x.ContentUnit, "unidad", x.Price, now)).ToArray();
        foreach (var product in products)
        {
            product.AddPresentation("Paquete", 6);
            product.AddPresentation("Caja", 24);
        }

        var suppliers = new[]
        {
            new Supplier("DEMO - Distribuidora Andina", "DEMO-70000101"),
            new Supplier("DEMO - Alimentos del Oriente", "DEMO-70000102"),
            new Supplier("DEMO - Comercial San Martín", "DEMO-70000103")
        };
        db.Customers.AddRange(customers);
        db.Products.AddRange(products);
        db.Suppliers.AddRange(suppliers);
        await db.SaveChangesAsync(cancellationToken);

        var receipts = suppliers.Select((supplier, i) => new PurchaseReceipt(supplier.Id,
            $"DEMO-FAC-{i + 1:000}", now.AddDays(-14 + i), accounts["WAREHOUSE"].Id)).ToArray();
        db.PurchaseReceipts.AddRange(receipts);

        var lotsByCode = new Dictionary<string, List<(InventoryLot Lot, InventoryLotBalance Balance)>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < products.Length; i++)
        {
            var fixture = Products[i];
            var supplierIndex = i % suppliers.Length;
            var lotCount = i == 0 ? 2 : 1;
            for (var lotIndex = 0; lotIndex < lotCount; lotIndex++)
            {
                var quantity = i == 0 ? (lotIndex == 0 ? 8 : 40) : 48;
                var lot = new InventoryLot(receipts[supplierIndex].Id, products[i].Id,
                    $"DEMO-{fixture.Code[^3..]}-{(char)('A' + lotIndex)}",
                    DateOnly.FromDateTime(now.UtcDateTime).AddMonths(lotIndex == 0 ? 3 : 6),
                    quantity, fixture.Cost + (lotIndex * 0.10m));
                var balance = new InventoryLotBalance(lot.Id, quantity);
                if (!lotsByCode.TryGetValue(fixture.Code, out var items))
                    lotsByCode[fixture.Code] = items = [];
                items.Add((lot, balance));
                db.InventoryLots.Add(lot);
                db.InventoryLotBalances.Add(balance);
                db.InventoryMovements.Add(new InventoryMovement(lot.Id, "RECEIPT", quantity, 0,
                    accounts["WAREHOUSE"].Id, now.AddDays(-14 + supplierIndex), "Ingreso ficticio de demostración"));
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        var store = customers[0];
        var family = customers[3];
        var seller = accounts["SELLER"];
        var rice = products[0];
        var pasta = products[1];

        var pickupOrder = new SalesOrder("DEMO-ORDER-001", store.Id, seller.Id, "REGISTERED",
            "PICKUP_LATER", store.PrimaryAddress, store.Latitude, store.Longitude, 120m, now.AddMinutes(-35));
        var riceLine = new OrderLine(pickupOrder.Id, rice.Id, null, 12m, 12, rice.UnitSalePrice,
            120m, 48, 0);
        pickupOrder.Lines.Add(riceLine);
        Allocate(pickupOrder, riceLine, lotsByCode[rice.InternalCode], 12, seller.Id, now, db);
        pickupOrder.History.Add(new OrderHistoryEntry(pickupOrder.Id, seller.Id, "order.created",
            "{\"source\":\"demo\"}", now.AddMinutes(-35)));
        db.NotificationOutbox.Add(new NotificationOutboxEntry(pickupOrder.Id, store.Id, "ORDER_RECEIVED",
            store.Phone, $"El pedido {pickupOrder.OrderNumber} fue recibido.", now.AddMinutes(-35)));

        var routeOrder = new SalesOrder("DEMO-ORDER-002", family.Id, seller.Id, "DISPATCHED",
            "DELIVERY", family.PrimaryAddress, family.Latitude, family.Longitude, 32m, now.AddMinutes(-25));
        var pastaLine = new OrderLine(routeOrder.Id, pasta.Id, null, 4m, 4, pasta.UnitSalePrice,
            32m, 48, 0);
        routeOrder.Lines.Add(pastaLine);
        var pastaAllocation = Allocate(routeOrder, pastaLine, lotsByCode[pasta.InternalCode], 4,
            seller.Id, now, db).Single();
        routeOrder.Payments.Add(new OrderPayment(routeOrder.Id, 10m, "DEMO-CAJA-002", seller.Id,
            now.AddMinutes(-20)));
        routeOrder.History.Add(new OrderHistoryEntry(routeOrder.Id, seller.Id, "order.created",
            "{\"source\":\"demo\"}", now.AddMinutes(-25)));

        var dispatch = new Dispatch("DEMO-DSP-001", "Camioneta DEMO 01", accounts["DISPATCH_LEAD"].Id,
            accounts["DRIVER"].Id, now.AddMinutes(-10), now.AddMinutes(-10));
        var dispatchOrder = new DispatchOrder(dispatch.Id, routeOrder.Id, 1, 1);
        pastaAllocation.SetPicked(4);
        var pastaBalance = lotsByCode[pasta.InternalCode][0].Balance;
        if (!pastaBalance.TryChangeReserved(-4) || !pastaBalance.TryAdjustOnHand(-4))
            throw new InvalidOperationException("No se pudo preparar el stock de demostración.");
        db.InventoryMovements.Add(new InventoryMovement(pastaAllocation.LotId, "DISPATCH", -4, -4,
            accounts["DISPATCH_LEAD"].Id, now.AddMinutes(-10), "Salida ficticia de demostración",
            routeOrder.Id, dispatchOrder.Id));
        routeOrder.History.Add(new OrderHistoryEntry(routeOrder.Id, accounts["DISPATCH_LEAD"].Id,
            "order.dispatched", "{\"source\":\"demo\"}", now.AddMinutes(-10)));
        db.NotificationOutbox.Add(new NotificationOutboxEntry(routeOrder.Id, family.Id, "OUT_FOR_DELIVERY",
            family.Phone, $"El pedido {routeOrder.OrderNumber} salió a reparto.", now.AddMinutes(-10)));
        dispatch.Orders.Add(dispatchOrder);
        db.Dispatches.Add(dispatch);
        db.DispatchOrders.Add(dispatchOrder);
        db.SalesOrders.AddRange(pickupOrder, routeOrder);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static List<OrderLotAllocation> Allocate(SalesOrder order, OrderLine line,
        List<(InventoryLot Lot, InventoryLotBalance Balance)> lots, int requestedUnits, Guid actorId,
        DateTimeOffset now, SoraDbContext db)
    {
        var remaining = requestedUnits;
        var allocations = new List<OrderLotAllocation>();
        foreach (var (lot, balance) in lots.OrderBy(x => x.Lot.ExpiryDate))
        {
            var quantity = Math.Min(remaining, balance.AvailableUnits);
            if (quantity <= 0) continue;
            if (!balance.TryChangeReserved(quantity))
                throw new InvalidOperationException("No se pudo reservar el stock de demostración.");
            var allocation = new OrderLotAllocation(line.Id, lot.Id, quantity, lot.UnitPurchaseCost);
            line.LotAllocations.Add(allocation);
            allocations.Add(allocation);
            db.InventoryMovements.Add(new InventoryMovement(lot.Id, "RESERVE", 0, quantity, actorId,
                now, "Reserva ficticia de demostración", order.Id));
            remaining -= quantity;
            if (remaining == 0) break;
        }
        if (remaining > 0)
            throw new InvalidOperationException("Stock insuficiente al preparar la demostración.");
        return allocations;
    }
}
