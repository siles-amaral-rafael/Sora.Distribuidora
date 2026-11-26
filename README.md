# Sora Distribuidora

Sistema web local para administrar una distribuidora: personal y permisos, clientes, catálogo, proveedores, inventario por lotes, pedidos, pagos y despachos. El repositorio contiene la aplicación, los contenedores y la suite de pruebas. La carpeta `web/` contiene la solución .NET; el portal de resultados de pruebas está en `web/test-portal/` y funciona por separado del sistema Sora.

## Manual

El manual técnico y de usuario, con instalación, operación por rol, procesos de negocio, API, respaldos, pruebas y solución de problemas, está en [`output/pdf/Manual_Tecnico_Usuario_Sora.pdf`](output/pdf/Manual_Tecnico_Usuario_Sora.pdf).

## Arquitectura

```text
Navegador
   │ HTTP/JSON
   ▼
Blazor WebAssembly + Nginx ── /api ──> ASP.NET Core Web API
                                            │ EF Core / Npgsql
                                            ▼
                                      PostgreSQL
```

La solución está organizada en `web/Sora.sln`:

| Proyecto | Responsabilidad |
|---|---|
| `Sora.Blazor` | Interfaz web y vistas distintas según permisos/rol |
| `Sora.Api` | API REST, autenticación JWT, permisos y composición de servicios |
| `Sora.Application` | Casos de uso y reglas de negocio |
| `Sora.Domain` | Entidades y contratos del dominio |
| `Sora.Infrastructure` | Persistencia PostgreSQL, EF Core y migraciones |
| `Sora.Shared` | Contratos HTTP compartidos entre cliente y API |

## Funcionalidades

- Cuentas con roles y permisos configurables, auditoría de cambios y protección de la última cuenta administradora.
- Clientes con datos de contacto, dirección y coordenadas; selector de ubicación en mapa.
- Registro múltiple de productos, código Sora automático, presentaciones y tramos de beneficios sujetos a reglas de margen.
- Proveedores vinculados a los productos que abastecen.
- Recepciones, lotes con vencimiento, costo, ajustes e historial de movimientos.
- Pedidos con precios, reservas FEFO, faltantes sujetos a decisión, edición/cancelación, pagos y saldo. Los pedidos nocturnos se programan para la primera salida del día siguiente.
- Salidas con varios pedidos, vehículo, repartidor, resultados de entrega, firma y control de devoluciones mediante inspección.
- Preferencias de interfaz por usuario: tema, escala de texto y contraste. Las tablas incluyen búsqueda, ordenamiento y paginación.
- Copias locales automáticas de PostgreSQL, con retención limitada y procedimiento de restauración documentado.

## Requisitos

Para ejecutar en Linux se recomienda Git, Podman, `podman-compose`, Python 3 y OpenSSL. Los contenedores proporcionan PostgreSQL 17, .NET 10 y Nginx; no hace falta instalar PostgreSQL ni .NET en el host si se trabaja con Podman. En instalaciones rootless de Podman, asegúrate de tener los componentes de red que requiera tu distribución (por ejemplo, `slirp4netns`).

## Inicio rápido local

Desde la raíz del repositorio:

```bash
cd web
cp .env.example .env
```

Edita `web/.env`: establece una contraseña local para PostgreSQL, utiliza la misma contraseña en `ConnectionStrings__Postgres` y genera una llave JWT aleatoria de al menos 32 bytes. Después ejecuta:

```bash
podman-compose up --build -d
```

Abre la interfaz en <http://localhost:5173>. La API responde en <http://localhost:5151/api/health>. PostgreSQL queda disponible solo en el host en `127.0.0.1:5433`; sus datos persisten en el volumen Podman `web_sora-postgres-data`.

Para una demostración en otra computadora de la misma red, consulta `web/README.md` y ejecuta `./scripts/prepare-lan-demo.sh <IP-local-del-servidor>` desde `web/` antes del primer arranque. No expongas esta configuración de demostración a Internet.

### Cuentas de demostración

La semilla de demostración crea una cuenta por rol: `admin@sora.demo`, `vendedor@sora.demo`, `almacen@sora.demo`, `despacho@sora.demo`, `ayudante@sora.demo` y `repartidor@sora.demo`. La contraseña temporal es `SoraDemo2026!`. Solo deben usarse con datos ficticios durante una demostración local. No habilites `SORA_DEMO_DATA` en una instancia con datos de negocio.

## Actualizar o detener

```bash
cd web
git pull
podman-compose up --build -d
```

Para detener los servicios sin borrar la base:

```bash
podman-compose down
```

No agregues `-v` si quieres conservar la base del volumen.

## Pruebas y portal independiente

La suite combina pruebas unitarias, integración con PostgreSQL aislado y pruebas HTTP de API. El entorno automatizado usa exclusivamente la base `sora_it`, puerto `55433`, una red y volumen propios. Desde `web/`:

```bash
./scripts/run-quality-suite.sh
```

El script genera/actualiza el reporte `web/test-portal/data/latest.json`. Para mostrarlo aparte del sistema:

```bash
python3 -m http.server 8765 --directory test-portal
```

Visita <http://localhost:8765>. Nunca configures la suite para apuntar a la base activa de demostración o a una base real.

## Seguridad y alcance

Las contraseñas se almacenan con BCrypt y la API usa JWT y autorización basada en permisos. Los archivos `.env`, `.env.integration`, respaldos y datos de contenedores son locales y no deben subirse a Git. Los pagos registran montos y referencias suministradas por caja; Sora no sustituye ni cobra por el sistema real de caja. Los avisos externos por WhatsApp o Telegram no forman parte de esta versión. La demostración por IP está pensada para una red local: no incluye HTTPS ni despliegue seguro en Internet.

## Documentación técnica

- Guía completa de uso y operación: [`output/pdf/Manual_Tecnico_Usuario_Sora.pdf`](output/pdf/Manual_Tecnico_Usuario_Sora.pdf).
- Guía de desarrollo, contenedores y entorno aislado: [`web/README.md`](web/README.md).
- Restauración de copias: [`web/RESTORE_BACKUP.md`](web/RESTORE_BACKUP.md).
- Casos y hallazgos de pruebas: [`web/tests/README.md`](web/tests/README.md) y [`web/tests/KNOWN-FINDINGS.md`](web/tests/KNOWN-FINDINGS.md).
