# Sora — Revisión 4

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

La solución ya contiene el esqueleto por capas y una base de autenticación con registro/inicio de sesión, JWT, BCrypt, EF Core y repositorio. La cadena de PostgreSQL y la llave JWT se configuran localmente; no se guardan en Git.

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

La migración inicial crea la tabla `staff_accounts` para las cuentas internas. La base relacional de Sora se ampliará con migraciones sucesivas según se implementen sus demás módulos.
