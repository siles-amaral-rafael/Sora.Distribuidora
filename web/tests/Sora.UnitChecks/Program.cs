using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using Sora.Application.Orders;
using Sora.Domain.Entities;
using Sora.Domain.Security;
using Sora.Shared.Orders;
using Sora.Shared.Products;

var results = new List<object>();
void Check(string id, string title, Action assertion)
{
    var timer = Stopwatch.StartNew();
    try { assertion(); results.Add(new { id, title, status = "APROBADO", durationMs = timer.Elapsed.TotalMilliseconds, error = (string?)null }); }
    catch (Exception ex) { results.Add(new { id, title, status = "FALLIDO", durationMs = timer.Elapsed.TotalMilliseconds, error = ex.Message }); }
}
void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
void Equal<T>(T expected, T actual, string message) where T : notnull { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{message}: esperado {expected}, recibido {actual}"); }
bool IsValid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);

var today = new DateOnly(2026, 11, 25);
var lotEarly = Guid.Parse("00000000-0000-0000-0000-000000000001");
var lotLate = Guid.Parse("00000000-0000-0000-0000-000000000002");

Check("UT-FEFO-01", "Reserva FEFO por vencimiento", () =>
{
    var plan = OrderPricingPolicy.PlanFefo(12,
    [new(lotLate, today.AddMonths(6), 40, 8m), new(lotEarly, today.AddMonths(3), 8, 8m)]);
    Equal(2, plan.Allocations.Count, "cantidad de lotes");
    Equal(lotEarly, plan.Allocations[0].LotId, "primer lote");
    Equal(8, plan.Allocations[0].Units, "unidades del primer lote");
    Equal(4, plan.Allocations[1].Units, "unidades del segundo lote");
    Equal(0, plan.MissingUnits, "faltante");
});
Check("UT-FEFO-02", "Lote sin vencimiento va al final", () =>
{
    var plan = OrderPricingPolicy.PlanFefo(4,
    [new(lotLate, null, 4, 8m), new(lotEarly, today.AddDays(1), 4, 8m)]);
    Equal(lotEarly, plan.Allocations[0].LotId, "prioridad de lote fechado");
});
Check("UT-STK-03", "Reserva exacta, una menos y una más", () =>
{
    var balance = new InventoryLotBalance(lotEarly, 10, 4);
    Equal(6, balance.AvailableUnits, "unidades disponibles");
    True(balance.TryChangeReserved(6), "debe aceptar reserva exacta");
    Equal(10, balance.ReservedUnits, "saldo reservado después del límite exacto");
    True(!balance.TryChangeReserved(1), "debe rechazar una unidad sobre el límite");
    Equal(10, balance.ReservedUnits, "el rechazo no debe mutar el saldo");
});
Check("UT-STK-04", "Ajuste no consume unidades reservadas", () =>
{
    var balance = new InventoryLotBalance(lotLate, 10, 4);
    True(!balance.TryAdjustOnHand(-7), "debe rechazar una unidad menos que lo reservado");
    Equal(10, balance.OnHandUnits, "el saldo físico permanece igual");
    True(balance.TryAdjustOnHand(-6), "debe aceptar reducción hasta lo reservado");
    Equal(0, balance.AvailableUnits, "saldo disponible después de ajustar hasta el reservado");
});
Check("UT-PRC-05", "Subtotal por unidad y redondeo monetario", () =>
{
    var line = new OrderLine(Guid.NewGuid(), Guid.NewGuid(), null, 12m, 12, 10m, 120m);
    Equal(120m, line.SubtotalAmount, "subtotal");
    var rounded = new OrderLine(Guid.NewGuid(), Guid.NewGuid(), null, 1m, 1, 1.005m, 1.01m);
    Equal(1.01m, rounded.SubtotalAmount, "redondeo AwayFromZero a dos decimales");
});
Check("UT-DIS-06", "Tope de descuento 20 por ciento del margen", () =>
{
    Equal(0.40m, OrderPricingPolicy.CalculateDiscount(10m, 8m, 80m), "descuento máximo permitido");
    Equal(0m, OrderPricingPolicy.CalculateDiscount(10m, 10m, 100m), "sin margen no hay descuento");
    Equal(0m, OrderPricingPolicy.CalculateDiscount(8m, 10m, 50m), "costo sobre precio no produce descuento");
});
Check("UT-DIS-07", "Descuento pedido dentro del límite y redondeo", () =>
{
    Equal(0.20m, OrderPricingPolicy.CalculateDiscount(10m, 8m, 2m), "aplica porcentaje solicitado bajo el tope");
    Equal(0.33m, OrderPricingPolicy.CalculateDiscount(10m, 8.34m, 100m), "redondeo del margen al centavo sin exceder 20 por ciento");
});
Check("UT-ORD-08", "Descuento no puede exceder subtotal", () =>
{
    var line = new OrderLine(Guid.NewGuid(), Guid.NewGuid(), null, 1m, 1, 10m, 10m);
    line.ApplyDiscount(12m);
    Equal(10m, line.DiscountAmount, "descuento limitado al subtotal");
    Equal(0m, line.LineTotal, "total mínimo cero");
});
Check("UT-VAL-09", "Partición inválida de cantidad de pedido", () =>
{
    var invalid = new CreateOrderLineRequest { ProductId = Guid.NewGuid(), Quantity = 0m };
    True(!IsValid(invalid), "cantidad cero debe fallar");
    invalid.Quantity = 0.001m;
    True(IsValid(invalid), "límite inferior válido debe pasar");
});
Check("UT-VAL-10", "Partición válida e inválida de descuento", () =>
{
    var valid = new SaveProductDiscountRuleRequest { MinimumQuantityUnits = 1, DiscountPercent = 100m };
    True(IsValid(valid), "porcentaje de límite superior debe pasar validación de formato");
    valid.DiscountPercent = 0m;
    True(!IsValid(valid), "cero debe fallar validación de porcentaje");
    valid.DiscountPercent = 100.01m;
    True(!IsValid(valid), "porcentaje sobre 100 debe fallar");
});
Check("UT-ACL-11", "Catálogo de permisos único y roles esperados", () =>
{
    var all = PermissionCatalog.All.Select(x => x.Code).ToArray();
    Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count(), "códigos duplicados");
    foreach (var role in new[] { "ADMIN", "SELLER", "WAREHOUSE", "DISPATCH_LEAD", "DISPATCH_ASSISTANT", "DRIVER" })
        True(PermissionCatalog.DefaultsByRole.ContainsKey(role), $"falta el rol {role}");
    True(PermissionCatalog.DefaultsByRole["ADMIN"].Contains("staff.manage"), "admin debe tener gestión de personal");
    True(!PermissionCatalog.DefaultsByRole["DRIVER"].Contains("staff.manage"), "repartidor no debe gestionar personal");
});

Console.WriteLine(JsonSerializer.Serialize(new { suite = "Sora.UnitChecks", generatedAt = DateTimeOffset.Now, results }, new JsonSerializerOptions { WriteIndented = true }));
if (results.Any(x => JsonSerializer.Serialize(x).Contains("FALLIDO", StringComparison.Ordinal))) Environment.ExitCode = 1;
