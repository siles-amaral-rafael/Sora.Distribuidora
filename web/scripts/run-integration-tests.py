#!/usr/bin/env python3
"""Revision 6 API/PostgreSQL integration checks. Runs only against sora-it."""
import csv
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import date, timedelta
from pathlib import Path

API = os.environ.get("SORA_IT_API_URL", "http://127.0.0.1:5251").rstrip("/")
DB_CONTAINER = os.environ.get("SORA_IT_DB_CONTAINER", "sora-it-postgres")
DB_NAME = "sora_it"
DB_USER = "sora_it"
PASSWORD = "SoraDemo2026!"
RESULT_DIR = Path(os.environ.get("SORA_IT_RESULT_DIR",
    Path(__file__).resolve().parents[1] / "test-results" / "revision6"))
RESULT_DIR.mkdir(parents=True, exist_ok=True)
results = []
state = {}
run_tag = str(int(time.time()))

if API != "http://127.0.0.1:5251" or DB_CONTAINER != "sora-it-postgres":
    raise SystemExit("Destino rechazado: integración solo permite API loopback:5251 y contenedor sora-it-postgres.")


class CheckFailure(Exception):
    pass


def assert_true(condition, message):
    if not condition:
        raise CheckFailure(message)


def api(method, path, body=None, token=None, expected=None):
    data = None if body is None else json.dumps(body).encode()
    headers = {"Accept": "application/json"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = "Bearer " + token
    request = urllib.request.Request(API + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            status, payload = response.status, response.read()
    except urllib.error.HTTPError as error:
        status, payload = error.code, error.read()
    text = payload.decode("utf-8", errors="replace")
    parsed = None
    if text:
        try:
            parsed = json.loads(text)
        except json.JSONDecodeError:
            parsed = text
    if expected is not None and status not in expected:
        raise CheckFailure(f"{method} {path}: HTTP {status}, esperado {expected}; respuesta={text[:500]}")
    return status, parsed


def db(sql):
    p = subprocess.run(["podman", "exec", DB_CONTAINER, "psql", "-X", "-U", DB_USER,
                        "-d", DB_NAME, "-At", "-c", sql], capture_output=True, text=True, timeout=15)
    if p.returncode:
        raise CheckFailure("Consulta de persistencia falló: " + p.stderr.strip()[:400])
    return p.stdout.strip()


def scalar(sql):
    return int(db(sql))


def test(test_id, title, action):
    started = time.perf_counter()
    error = None
    try:
        evidence = action()
        status = "APROBADO"
    except Exception as exc:  # keep running all cases to collect a complete report
        status, error, evidence = "FALLIDO", str(exc), ""
    duration = round((time.perf_counter() - started) * 1000, 2)
    results.append({"id": test_id, "title": title, "status": status,
                    "durationMs": duration, "error": error or "", "evidence": str(evidence or "")})
    print(f"{status:8} {test_id:16} {duration:8.2f} ms {title}" + (f" | {error}" if error else ""))


def login(email, password=PASSWORD, expected=200):
    status, payload = api("POST", "/api/auth/login", {"email": email, "password": password}, expected=[expected])
    assert_true(isinstance(payload, dict) and payload.get("accessToken"), f"login {email} no emitió token")
    return payload["accessToken"]


def db_identity():
    assert_true(db("select current_database() || ':' || current_user") == "sora_it:sora_it",
                "Destino inesperado; cancelada la suite para proteger datos de negocio")
    assert_true(scalar('select count(*) from "__EFMigrationsHistory"') > 0, "base sin migraciones")
    status, payload = api("GET", "/api/health", expected=[200])
    assert_true(payload.get("status") == "ok", "API de prueba no saludable")
    return f"base=sora_it; user=sora_it; migraciones={scalar('select count(*) from \"__EFMigrationsHistory\"')}; health=200"


def auth_checks():
    state["admin"] = login("admin@sora.demo")
    state["seller"] = login("vendedor@sora.demo")
    state["warehouse"] = login("almacen@sora.demo")
    state["dispatch"] = login("despacho@sora.demo")
    state["driver"] = login("repartidor@sora.demo")
    _, me = api("GET", "/api/auth/me", token=state["admin"], expected=[200])
    assert_true(me.get("email", "").lower() == "admin@sora.demo", "token identifica cuenta incorrecta")
    api("POST", "/api/auth/login", {"email": "admin@sora.demo", "password": "clave-incorrecta-123"}, expected=[401])
    api("GET", "/api/auth/me", expected=[401])
    return "5 roles con JWT válido; /me identifica al actor; credencial inválida y acceso anónimo=401"


def acl_checks():
    api("GET", "/api/staff", token=state["seller"], expected=[403])
    api("GET", "/api/products", token=state["driver"], expected=[403])
    status, _ = api("PUT", "/api/staff/00000000-0000-0000-0000-000000000001/active",
                    {"isActive": False}, token=state["admin"], expected=[404])
    staff = api("GET", "/api/staff", token=state["admin"], expected=[200])[1]
    admin = next(x for x in staff if any(r["code"] == "ADMIN" for r in x["roles"]) and x["isActive"])
    api("PUT", f"/api/staff/{admin['id']}/active", {"isActive": False}, token=state["admin"], expected=[409])
    return "vendedor/repartidor sin permiso=403; último ADMIN protegido=409"


def customer_crud():
    body = {"customerType": "FAMILY", "fullName": f"QA Familia {run_tag}", "phone": f"QA-{run_tag}",
            "primaryAddress": "Dirección ficticia de integración", "latitude": -17.75, "longitude": -63.18}
    _, customer = api("POST", "/api/customers", body, state["admin"], [201])
    cid = customer["id"]
    assert_true(scalar(f"select count(*) from customers where id='{cid}' and latitude=-17.75 and longitude=-63.18") == 1,
                "cliente no persistió coordenadas")
    body["fullName"] += " editado"
    api("PUT", f"/api/customers/{cid}", body, state["admin"], [200])
    assert_true(scalar(f"select count(*) from customers where id='{cid}' and full_name like '%editado'") == 1,
                "edición de cliente no persistió")
    api("DELETE", f"/api/customers/{cid}", token=state["admin"], expected=[204])
    assert_true(scalar(f"select count(*) from customers where id='{cid}' and is_active=false") == 1,
                "desactivación de cliente no persistió")
    api("POST", f"/api/customers/{cid}/activate", token=state["admin"], expected=[204])
    state["customer"] = cid
    return f"cliente {cid}: crear, leer/escribir y activar/desactivar persistidos"


def create_catalog():
    _, supplier = api("POST", "/api/suppliers", {"name": f"QA Proveedor {run_tag}", "phone": f"QA-{run_tag}"}, state["admin"], [201])
    _, product = api("POST", "/api/products", {"name": f"QA Arroz {run_tag}", "brand": "QA", "category": "Alimentos",
        "netContent": 1000, "contentUnit": "g", "baseUnit": "unidad", "unitSalePrice": 10}, state["admin"], [201])
    state["supplier"], state["product"] = supplier["id"], product["id"]
    api("PUT", f"/api/suppliers/{supplier['id']}/products", {"productIds": [product["id"]]}, state["admin"], [200])
    assert_true(scalar(f"select count(*) from supplier_products where supplier_id='{supplier['id']}' and product_id='{product['id']}'") == 1,
                "vínculo proveedor-producto no persistió")
    api("DELETE", f"/api/suppliers/{supplier['id']}", token=state["admin"], expected=[204])
    api("POST", f"/api/suppliers/{supplier['id']}/activate", token=state["admin"], expected=[204])
    assert_true(scalar(f"select count(*) from suppliers where id='{supplier['id']}' and is_active=true") == 1,
                "proveedor no pudo reactivarse")
    return f"proveedor {supplier['id']} y producto {product['id']} creados y vinculados"


def receive_lot(lot_code, quantity, expiry):
    _, receipt = api("POST", "/api/inventory/receipts", {"supplierId": state["supplier"], "lines": [
        {"productId": state["product"], "lotCode": lot_code, "expiryDate": expiry.isoformat(),
         "quantity": quantity, "unitPurchaseCost": 8}]}, state["admin"], [201])
    return receipt


def inventory_reception():
    first = receive_lot(f"QA-{run_tag}-A", 3, date.today() + timedelta(days=30))
    assert_true(first["invoiceNumber"].startswith("REC-"), "no generó factura interna")
    assert_true(scalar(f"select count(*) from purchase_receipts where id='{first['id']}'") == 1, "recepción no persistió")
    assert_true(scalar(f"select count(*) from inventory_movements m join inventory_lots l on l.id=m.lot_id where l.lot_code='QA-{run_tag}-A' and m.movement_type='RECEIPT'") == 1,
                "movimiento de recepción no persistió")
    state["lot_a"], state["lot_b"] = f"QA-{run_tag}-A", f"QA-{run_tag}-B"
    state["invoice_a"] = first["invoiceNumber"]
    return f"recepción {first['invoiceNumber']} con lote, saldo y movimiento persistidos"


def receipt_sequence():
    first_number = int(state["invoice_a"].split("-")[1])
    second = receive_lot(f"QA-{run_tag}-B", 12, date.today() + timedelta(days=90))
    next_number = int(second["invoiceNumber"].split("-")[1])
    assert_true(next_number == first_number + 1, "número de factura no incrementó en uno")
    assert_true(scalar(f"select count(*) from purchase_receipts where id='{second['id']}'") == 1, "segunda factura no persistió")
    return f"consecutivo verificado: REC-{first_number:06d} → {second['invoiceNumber']}"


def order_create(quantity, customer_id=None, night=False):
    req = {"customerId": customer_id or state["customer"], "fulfillment": "DELIVERY", "isNightOrder": night,
           "lines": [{"productId": state["product"], "quantity": quantity}]}
    return api("POST", "/api/orders", req, state["admin"], [201, 202])


def order_fefo():
    status, order = order_create(5)
    assert_true(status == 201 and order["status"] == "REGISTERED", "pedido con stock suficiente no quedó registrado")
    assert_true(float(order["totalAmount"]) == 50.0, "total del pedido no coincide")
    lots = order["lines"][0]["lots"]
    assert_true([(x["lotCode"], x["quantityReserved"]) for x in lots] == [(state["lot_a"], 3), (state["lot_b"], 2)],
                f"reserva FEFO incorrecta: {lots}")
    oid = order["id"]
    assert_true(scalar(f"select count(*) from sales_orders where id='{oid}' and total_amount=50") == 1, "pedido/total no persistió")
    assert_true(scalar(f"select count(*) from order_lot_allocations a join inventory_lots l on l.id=a.lot_id join order_lines ol on ol.id=a.order_line_id where ol.order_id='{oid}' and l.lot_code='{state['lot_a']}' and a.quantity_reserved=3") == 1,
                "asignación FEFO no persistió")
    state["order"] = oid
    return f"pedido {order['orderNumber']}; total Bs 50; lote cercano=3, siguiente=2; persistido"


def shortage_case():
    _, cust = api("POST", "/api/customers", {"customerType": "FAMILY", "fullName": f"QA Faltante {run_tag}",
        "phone": f"QAS-{run_tag}", "primaryAddress": "Dirección ficticia"}, state["admin"], [201])
    status, order = order_create(100, cust["id"])
    assert_true(status == 202 and order["status"] == "PENDING_CONFIRMATION", "faltante no quedó pendiente")
    assert_true(order["lines"][0]["shortageUnits"] > 0, "faltante no se calculó")
    assert_true(scalar(f"select count(*) from order_lot_allocations a join order_lines ol on ol.id=a.order_line_id where ol.order_id='{order['id']}'") == 0,
                "pedido pendiente reservó stock antes de decisión")
    state["shortage_order"] = order["id"]
    return f"pedido {order['orderNumber']} pendiente; sin reservas previas a la decisión"


def payment_case():
    order = state["order"]
    api("POST", f"/api/orders/{order}/payments", {"amount": 50, "reference": f"QA-PAY-{run_tag}"}, state["admin"], [200])
    assert_true(scalar(f"select count(*) from order_payments where order_id='{order}' and amount=50") == 1, "pago no persistió")
    api("POST", f"/api/orders/{order}/payments", {"amount": 50, "reference": f"QA-PAY-{run_tag}"}, state["admin"], [409])
    api("POST", f"/api/orders/{order}/payments", {"amount": 0.01, "reference": f"QA-OVER-{run_tag}"}, state["admin"], [409])
    _, updated = api("GET", f"/api/orders/{order}", token=state["admin"], expected=[200])
    assert_true(float(updated["paidAmount"]) == 50 and float(updated["balanceDue"]) == 0, "pago/saldo no concuerdan")
    return "pago completo persistido; duplicado y sobrepago rechazados; saldo cero"


def payment_and_installments():
    evidence = payment_case()
    _, cust = api("POST", "/api/customers", {"customerType": "FAMILY", "fullName": f"QA Abono {run_tag}",
        "phone": f"QAP-{run_tag}", "primaryAddress": "Dirección ficticia"}, state["admin"], [201])
    status, order = order_create(1, cust["id"])
    assert_true(status == 201, "pedido de particular no creado")
    api("POST", f"/api/orders/{order['id']}/payments", {"amount": 5, "reference": f"QA-ABONO-A-{run_tag}"}, state["admin"], [200])
    api("POST", f"/api/orders/{order['id']}/payments", {"amount": 5, "reference": f"QA-ABONO-B-{run_tag}"}, state["admin"], [200])
    _, updated = api("GET", f"/api/orders/{order['id']}", token=state["admin"], expected=[200])
    assert_true(float(updated["balanceDue"]) == 0 and len(updated["payments"]) == 2, "abonos no concuerdan con saldo")
    return evidence + "; particular: 2 abonos con referencias distintas y saldo final cero"


def dispatch_case():
    order = state["order"]
    api("PUT", f"/api/orders/{order}/prepare", token=state["admin"], expected=[204])
    _, drivers = api("GET", "/api/dispatches/drivers?query=Demo%20Repartidor", token=state["admin"], expected=[200])
    driver = next(x for x in drivers if x["email"].lower() == "repartidor@sora.demo")
    _, dispatch = api("POST", "/api/dispatches", {"vehicleIdentifier": f"QA-{run_tag}",
        "driverStaffId": driver["id"], "orderIds": [order]}, state["admin"], [201])
    dispatch_order_id = dispatch["orders"][0]["dispatchOrderId"]
    sig = "dGVzdC1maXJtYS1maWN0aWNpYQ=="
    api("POST", f"/api/dispatches/orders/{dispatch_order_id}/result", {"result": "DELIVERED",
        "recipientName": "Receptor ficticio QA", "recipientRelationship": "Prueba",
        "signatureBase64": sig, "signatureMimeType": "image/png"}, state["driver"], [204])
    assert_true(scalar(f"select count(*) from dispatch_orders where id='{dispatch_order_id}' and result='DELIVERED' and result_recorded_by_id='{driver['id']}' and signature_bytes is not null") == 1,
                "resultado/firma/actor del despacho no persistió")
    assert_true(scalar(f"select count(*) from dispatches where id='{dispatch['id']}' and vehicle_identifier='QA-{run_tag}'") == 1,
                "salida no persistió")
    return f"salida {dispatch['dispatchNumber']}; repartidor autorizado registró recepción firmada"


def night_order():
    status, order = order_create(1, night=True)
    tomorrow = date.today() + timedelta(days=1)
    assert_true(status == 201 and order["isNightOrder"] is True, "pedido nocturno no creado")
    assert_true(order["scheduledDeliveryDate"] == tomorrow.isoformat(), "fecha nocturna no es mañana")
    assert_true(scalar(f"select count(*) from sales_orders where id='{order['id']}' and is_night_order=true and scheduled_delivery_date='{tomorrow.isoformat()}'") == 1,
                "programación nocturna no persistió")
    return f"pedido nocturno programado para {tomorrow.isoformat()}"


preflight = db_identity()  # hard guard: abort before mutations unless target is precisely sora_it/sora_it
test("IT-AUTH-01", "JWT, login válido/inválido y ruta protegida", auth_checks)
test("IT-ACL-02", "Permisos de rol y protección del último administrador", acl_checks)
test("IT-CLI-03", "CRUD, coordenadas y activación de clientes", customer_crud)
test("IT-SUP-14", "Proveedor, producto, vínculo y reactivación", create_catalog)
test("IT-INV-04", "Recepción de inventario y movimiento persistido", inventory_reception)
test("IT-INV-13", "Factura interna secuencial", receipt_sequence)
test("IT-ORD-05", "Total y reserva FEFO mult lote", order_fefo)
test("IT-STK-06", "Faltante pendiente sin reserva previa", shortage_case)
test("IT-PAY-07", "Pagos, saldo, idempotencia y abonos", payment_and_installments)
test("IT-DSP-08", "Preparación, salida y recepción firmada", dispatch_case)
test("IT-NOC-10", "Pedido nocturno al siguiente día", night_order)

generated = time.strftime("%Y-%m-%dT%H:%M:%S%z")
summary = {"suite": "Sora.IntegrationAPI", "generatedAt": generated, "api": API,
           "database": DB_NAME, "user": DB_USER, "caseCount": len(results),
           "preflight": preflight,
           "passed": sum(x["status"] == "APROBADO" for x in results),
           "failed": sum(x["status"] == "FALLIDO" for x in results),
           "durationMs": round(sum(x["durationMs"] for x in results), 2), "results": results}
(RESULT_DIR / "integration-results.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
with (RESULT_DIR / "integration-results.csv").open("w", newline="", encoding="utf-8-sig") as f:
    w = csv.DictWriter(f, fieldnames=["id", "title", "status", "durationMs", "error", "evidence"], delimiter=";")
    w.writeheader(); w.writerows(results)
print(f"Resumen integración: {summary['passed']}/{summary['caseCount']} aprobados; {summary['failed']} fallidos; {summary['durationMs']:.2f} ms sumados.")
print(f"Evidencia: {RESULT_DIR / 'integration-results.csv'} y integration-results.json")
sys.exit(1 if summary["failed"] else 0)
