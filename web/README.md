# Sora — Revisión 5

Solución base en .NET 10 para Ubuntu, Visual Studio Code y Podman.

## Proyectos

```text
Sora.sln
└── src/
    ├── Sora.Domain          Entidades y contratos del dominio
    ├── Sora.Shared          DTOs compartidos por API y Blazor
    ├── Sora.Application     Casos de uso y servicios de aplicación
    ├── Sora.Infrastructure  Persistencia PostgreSQL y repositorios
    ├── Sora.Api             ASP.NET Core Web API, controladores y composición
    └── Sora.Blazor          Cliente Blazor WebAssembly independiente
```

## Dependencias entre capas

```text
Sora.Blazor ──HTTP/JSON──> Sora.Api
     │                       ├── Sora.Application ──> Sora.Domain
     └── Sora.Shared         ├── Sora.Infrastructure ──> Sora.Application
                             └── Sora.Shared
```

La API se conecta a PostgreSQL en un contenedor Podman mediante EF Core y Npgsql. Compose conserva los datos en un volumen local. Para la versión final se podrá desplegar PostgreSQL en un servidor o servicio administrado; Supabase ya no es una dependencia de este prototipo.

## Framework objetivo

La solución usa `net10.0`. El SDK 10 está activo y es LTS. Como la consigna no fija versión, se toma .NET 10 como objetivo. Visual Studio 2022 no admite proyectos `net10.0`; para abrirlos con el IDE usado en clases se necesitaría que el revisor acepte Visual Studio 2026 o acordar una versión anterior de .NET.

## Estado

La solución implementa autenticación de personal con JWT y BCrypt, además de clientes, catálogo, proveedores, inventario por lote, pedidos y despachos. PostgreSQL conserva lotes, reservas y movimientos; la reserva selecciona primero los lotes con vencimiento más cercano (FEFO). Las operaciones sensibles se ejecutan dentro de transacciones.

## Diseño de interfaz y perfiles

La interfaz adopta **Material Design 3 (Material 3)** como guía visual: jerarquía tipográfica clara, superficies y tarjetas, navegación agrupada, colores semánticos por espacio de trabajo, estados visibles, controles con foco perceptible y adaptación a pantallas estrechas. Se eligió porque permite mantener un lenguaje visual consistente en formularios y operaciones, mientras cada rol prioriza sus tareas. Es una implementación inspirada en sus principios, no una declaración de certificación de conformidad.

Cada perfil presenta una portada, navegación y acento visual propios: administración, ventas, almacén, jefatura de despacho, ayudante de despacho y reparto. La lista de accesos sigue filtrada por los permisos efectivos de la sesión y las páginas/API conservan sus validaciones de autorización; el diseño no concede permisos nuevos. Si una cuenta reúne varios roles, la portada usa este orden de perfil principal: administrador, jefe de despacho, almacén, vendedor, ayudante y repartidor; sus accesos visibles continúan combinando los permisos de la cuenta.

Los listados extensos de clientes, productos, proveedores, stock por lote, pedidos, despachos y administración muestran 10 filas o tarjetas por página y permiten elegir 10, 25 o 50. La paginación se realiza en la interfaz sobre los resultados devueltos por la API.

La pantalla **Preferencias** permite seleccionar tema claro, oscuro o el del sistema, tamaño de texto de 100% a 200% y contraste reforzado. Las opciones se guardan en `localStorage` bajo el identificador de la cuenta (claim `sub`) y se aplican al volver a iniciar sesión en ese navegador; no se sincronizan con otros navegadores o dispositivos.

La accesibilidad visual toma **WCAG 2.2 nivel AA** como objetivo de diseño: ampliación de texto, contraste, foco visible, controles etiquetados y adaptación a pantallas estrechas. La preferencia de movimiento reducido respeta el ajuste del sistema. Es un objetivo, no una certificación; hace falta una evaluación formal para afirmar conformidad.

## Módulos disponibles

- **Clientes:** alta, consulta, edición y desactivación; guarda una dirección y coordenadas opcionales.
- **Productos:** alta, consulta, edición, desactivación y presentaciones con equivalencia a unidades base.
- **Proveedores:** alta, consulta, edición y desactivación.
- **Inventario:** recepción por factura, producto, lote, vencimiento y costo; consulta de saldo físico, reservado y disponible; ajustes con motivo e historial inmutable.
- **Pedidos:** registro y edición antes de despachar, cálculo del importe según precio unitario y reserva FEFO atómica. Si falta stock, guarda el pedido como pendiente de confirmación, sin apartar unidades; el vendedor registra el acuerdo con el cliente y ajusta cantidades antes de reservar.
- **Pagos de prueba:** registra uno o más abonos con el monto y referencia que entrega caja. Los clientes particulares pueden pagar parcialmente; tiendas e instituciones deben cubrir el total en una sola operación. Todo saldo pendiente bloquea nuevos pedidos del mismo cliente. El despacho de un particular permite saldo después del primer abono; los demás tipos deben estar pagados completamente. Esta etapa no se conecta al sistema real de caja.
- **Beneficios por cantidad:** el administrador configura tramos por producto. El sistema usa el costo de los lotes FEFO realmente reservados y limita el descuento a un máximo del 20% del margen bruto de la venta; se bloquea el pedido si el precio no cubre el costo del lote.
- **Acceso y auditoría:** el administrador registra cuentas, asigna uno o más roles, cambia permisos por rol y activa o desactiva cuentas. La API consulta permisos vigentes en PostgreSQL y conserva auditoría de cambios. Protege la última cuenta con permiso de administración.
- **Despachos:** una salida puede incluir varios pedidos; registra vehículo y chofer. Captura firma dibujada para recepción o rechazo. Un cliente no encontrado puede tener un segundo intento y el rechazo devuelve las unidades al stock.

## Rutas principales de la API

Todas las rutas de negocio requieren un JWT y una política de permisos almacenada en PostgreSQL. El registro público solo está habilitado en `Development`; en otros entornos devuelve 404.

| Ruta | Operación |
|---|---|
| `GET /api/health` | Estado del servicio |
| `POST /api/auth/login` | Inicio de sesión |
| `GET /api/staff` | Listar cuentas (permiso `staff.manage`) |
| `POST /api/staff` | Crear cuenta y asignar roles |
| `GET /api/staff/roles`, `GET /api/staff/permissions` | Consultar roles y catálogo de permisos |
| `PUT /api/staff/{id}/roles`, `PUT /api/staff/{id}/active` | Administrar asignaciones y estado de cuenta |
| `PUT /api/staff/roles/{roleCode}/permissions` | Configurar permisos por rol |
| `GET /api/staff/audit` | Consultar auditoría de cambios de acceso |
| `GET, POST /api/customers` | Consultar y registrar clientes |
| `GET, PUT, DELETE /api/customers/{id}` | Consultar, editar y desactivar cliente |
| `GET, POST /api/products` | Consultar y registrar productos |
| `POST /api/products/bulk` | Registrar hasta 50 productos y generar automáticamente códigos internos numéricos |
| `GET, PUT, DELETE /api/products/{id}` | Consultar, editar y desactivar producto |
| `POST /api/products/{id}/presentations` | Registrar presentación |
| `PUT /api/products/{id}/discount-rules` | Configurar tramos de beneficio (administrador) |
| `GET, POST /api/suppliers` | Consultar y registrar proveedores |
| `PUT /api/suppliers/{id}/products` | Vincular productos abastecidos por un proveedor |
| `GET /api/inventory/stock` | Consultar saldo por lote |
| `POST /api/inventory/receipts` | Registrar recepción y lotes |
| `POST /api/inventory/adjustments` | Ajustar saldo con historial |
| `GET, POST /api/orders` | Consultar y registrar pedidos |
| `GET /api/orders/{id}`, `PUT /api/orders/{id}` | Consultar y editar un pedido antes del despacho |
| `PUT /api/orders/{id}/availability` | Registrar la decisión sobre faltantes |
| `PUT /api/orders/{id}/prepare`, `/pickup`, `/cancel` | Preparar, confirmar retiro o cancelar pedido |
| `POST /api/orders/{id}/payments` | Registrar el importe y la referencia de caja; no reemplaza al sistema de caja |
| `GET /api/dispatches/drivers?query={texto}` | Buscar repartidores por nombre, correo o identificador |
| `POST /api/dispatches` | Crear salida con varios pedidos |
| `POST /api/dispatches/orders/{dispatchOrderId}/result` | Registrar resultado de entrega |

En `Development`, `POST /api/auth/development/roles` solo permite promover una vez a la primera cuenta autenticada al rol ADMIN, como bootstrap inicial. Una vez que existe un administrador, el endpoint rechaza nuevos cambios. En otros entornos responde 404. El administrador crea las demás cuentas desde `/staff`.

## Secretos locales de desarrollo

Desde `web/`, copia `.env.example` como `.env` y define una contraseña local para PostgreSQL y una llave JWT aleatoria. No subas `.env` al repositorio ni compartas las credenciales en el chat. Para ejecutar la API fuera de contenedores, configura el secreto así desde `src/Sora.Api`:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5433;Database=sora;Username=sora;Password=<tu-clave-local>;SSL Mode=Disable"
dotnet user-secrets set "Jwt:SigningKey" "<una-clave-aleatoria-de-al-menos-32-bytes>"
```

El endpoint `POST /api/auth/register` está habilitado solo en Development para la prueba académica. En los demás entornos responde 404. En producción las cuentas deberán crearse mediante un flujo administrativo autenticado.

## Migración y ejecución local

Desde `web/`, después de crear `.env` con valores locales:

```bash
dotnet tool restore
podman-compose up -d sora-postgres
dotnet ef database update --project src/Sora.Infrastructure --startup-project src/Sora.Api
dotnet run --project src/Sora.Api --launch-profile http
```

Para levantar toda la solución en contenedores, usa `podman-compose up --build`. PostgreSQL se publica solo en `127.0.0.1:5433` y mantiene sus datos en el volumen `sora-postgres-data`.

Las migraciones `Revision5OperationalModules` y `Revision5RejectionInspection` contienen la base operativa de la revisión 5. `PhaseFinalAccessAndOrderControls` agrega permisos configurables, auditorías, reglas de descuentos, estados de confirmación de faltantes e historial de pedidos. `PhaseFinalPaymentsAndNotifications` incorporó pagos y una cola temporal de avisos simulados; `UserWorkflowRefresh` agrega los vínculos proveedor-producto y elimina esa cola y su permiso. La migración descarta las filas de avisos simulados existentes. Los avisos reales por WhatsApp o Telegram quedan fuera de esta versión. Tras actualizar el código, ejecuta `dotnet ef database update --project src/Sora.Infrastructure --startup-project src/Sora.Api`. Cierra sesión y vuelve a entrar para que el token de interfaz recoja los permisos iniciales. Para actualizar imágenes sin reiniciar PostgreSQL usa `podman-compose up --build -d sora-api sora-blazor`; el volumen `sora-postgres-data` conserva sus datos.

La interfaz queda disponible localmente en `http://localhost:5173` y la API en `http://localhost:5151`.

## Preparar una demostración en otra computadora (Debian/Ubuntu)

El host solo necesita Git, Podman, `podman-compose`, Python 3 y OpenSSL; .NET 10, PostgreSQL y Nginx se ejecutan o compilan dentro de contenedores. Debian no es un impedimento: Podman está disponible en los repositorios de Debian 11 en adelante. Las imágenes .NET 10 oficiales usan Ubuntu como sistema base dentro del contenedor, de modo que no dependen de que el host sea Debian o Ubuntu. Consulta la [guía oficial de instalación de Podman](https://podman.io/docs/installation) y la [nota de Microsoft sobre las imágenes de .NET 10](https://learn.microsoft.com/dotnet/core/compatibility/containers/10.0/default-images-use-ubuntu).

Para una demo cómoda, recomiendo Debian 12 o posterior en una máquina de 64 bits, 2 núcleos, 4 GB de RAM y al menos 20 GB libres. Son valores recomendados para construir y ejecutar estas imágenes, no requisitos mínimos garantizados. Instala `git podman podman-compose python3 openssl`; no hace falta instalar PostgreSQL ni el SDK de .NET en el host.

### Primera ejecución

1. Instala Git, Podman, `podman-compose`, Python 3 y OpenSSL en Debian. Comprueba `podman info` y `podman-compose --version`.
2. Reserva una dirección local para el servidor en el router; así los otros equipos usarán siempre la misma IP.
3. Clona el repositorio, entra a `web/` y genera automáticamente el archivo local de configuración:

   ```bash
   git clone https://github.com/siles-amaral-rafael/Sora.Distribuidora.git
   cd Sora.Distribuidora/web
   ./scripts/prepare-lan-demo.sh 192.168.1.50
   ```

   Sustituye `192.168.1.50` por la IP reservada para Debian. El script crea `.env` con contraseñas aleatorias, habilita la carga demo, configura Production y limita el acceso al puerto de la interfaz. Protege `.env` con permisos `600`; no lo subas a Git ni lo compartas.

4. Construye y levanta la aplicación:

   ```bash
   podman-compose up --build -d
   ```

   La API ejecuta las migraciones al iniciar. Con `SORA_DEMO_DATA=true` se cargan una sola vez los datos de demostración. La interfaz queda disponible en `http://192.168.1.50:5173` (reemplaza esa IP por la reservada). PostgreSQL y el puerto directo de la API siguen limitados al propio host; Nginx entrega la interfaz y reenvía `/api/` internamente a la API.

5. Desde los otros equipos de la misma red, abre esa dirección en el navegador. Si Debian usa UFW, permite el puerto 5173 solo desde el rango de la red local, por ejemplo `sudo ufw allow from 192.168.1.0/24 to any port 5173 proto tcp`, cambiando el rango por el de tu red. No configures reenvío de puertos del router hacia internet para esta demostración.

### Cuentas de demostración

La semilla crea seis usuarios, uno por rol. Todas las cuentas usan la contraseña temporal `SoraDemo2026!`:

| Rol | Usuario |
|---|---|
| Administrador | `admin@sora.demo` |
| Vendedor | `vendedor@sora.demo` |
| Encargado de almacén | `almacen@sora.demo` |
| Jefe de despacho | `despacho@sora.demo` |
| Ayudante | `ayudante@sora.demo` |
| Repartidor | `repartidor@sora.demo` |

Estas credenciales son únicamente para una demostración local con datos ficticios. No actives la carga de demo en un sistema real, no expongas este prototipo a internet y cambia las contraseñas antes de cualquier uso real.

La semilla carga aproximadamente 120 registros relacionados en cuentas y roles, clientes, productos y presentaciones, proveedores, recepciones, lotes y movimientos, pedidos, pagos, avisos y un despacho de ejemplo. Los nombres, teléfonos, direcciones, ubicaciones y operaciones llevan la marca `DEMO`; no representan clientes ni transacciones reales. No se copian datos ni respaldos de la computadora original.

### Actualizar y detener

Para obtener cambios desde GitHub, ejecuta `git pull` y luego `podman-compose up --build -d`. El volumen `sora-postgres-data` conserva la base durante actualizaciones normales. Para detener los servicios usa `podman-compose down`; no añadas `-v` salvo que quieras borrar definitivamente la base de demostración. Las copias automáticas se guardan localmente en `../backups/` y no se suben a Git.

El acceso por IP es para la misma red local. Un nombre DNS no es necesario para la demo; se puede reservar una IP local estable en el router. El acceso desde fuera de esa red requiere una publicación separada con HTTPS y controles de seguridad adicionales.
