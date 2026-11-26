# Proyectos de prueba

- **Sora.UnitTests:** reglas aisladas de pedidos/FEFO, precios y descuento, stock, autenticación, roles, clientes, productos, inventario, proveedores y despachos. Moq simula repositorios para comprobar que las validaciones cortan antes de persistir.
- **Sora.IntegrationTests:** conexión real a PostgreSQL, identidad de base/usuario, historial EF y transacción revertida. Exige `sora_it` en `127.0.0.1:55433`.
- **Sora.ApiTests:** `WebApplicationFactory<Program>` contra el API real con su PostgreSQL de integración; comprueba health, JWT, acceso anónimo, permiso de rol y validación del modelo.
- **`scripts/run-integration-tests.py`:** recorre flujos de API y comprueba estado persistido por PostgreSQL, incluyendo cliente, proveedor-producto, recepciones, factura, FEFO, faltantes, pagos, despacho y pedido nocturno.

Los proyectos xUnit se agregan a `Sora.sln`. La base solo puede ser `sora_it`/`sora_it` por `127.0.0.1:55433`; el script de infraestructura usa red y volumen exclusivos. Los avisos están excluidos porque los avisos simulados fueron retirados del producto. Inspección/rechazo de retornos y restauración automatizada de respaldos siguen pendientes y se muestran así en el portal; no se cuentan como aprobados.

El hallazgo de mensaje inconsistente al duplicar código de producto está en [KNOWN-FINDINGS.md](KNOWN-FINDINGS.md). La suite valida que no se guarde el duplicado y deja constancia del código de error actual; no corrige lógica funcional.
