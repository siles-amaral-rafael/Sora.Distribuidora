# Copias de seguridad y restauración local

## Alcance

El contenedor `sora-backup` genera un respaldo personalizado de PostgreSQL al iniciar y luego cada 24 horas. Conserva los siete respaldos más recientes en `../backups/`, una carpeta local excluida de Git. Cada archivo se escribe primero con extensión `.partial`, se valida con `pg_restore --list` y solo entonces se publica como `.dump`. Si PostgreSQL todavía no está disponible, espera a que responda; si falla una copia, vuelve a intentarla cada cinco minutos.

Las firmas de recepción y rechazo están almacenadas como `bytea` en PostgreSQL, así que están incluidas en el respaldo. No se encontró almacenamiento separado de archivos subidos por la aplicación.

Los archivos locales están protegidos con permisos de usuario (`0700` en la carpeta y `0600` en cada respaldo). Esta copia está en el mismo equipo que la base: protege de errores lógicos o de una corrupción recuperable, pero no de la pérdida o daño del equipo.

## Revisar el estado

Desde `web/`:

```bash
podman ps --filter name=sora-backup
podman logs --tail 30 web_sora-backup_1
ls -lh ../backups
```

Para generar un respaldo manual:

```bash
podman exec web_sora-backup_1 /bin/sh /scripts/backup-once.sh
```

El contenedor lee las credenciales desde `web/.env`; no se guardan en los scripts ni en los archivos de respaldo.

## Comprobar una restauración sin tocar la base activa

Desde `web/`, escoge el nombre de un archivo existente en `../backups/` y reemplaza `sora_YYYYMMDDTHHMMSSZ.dump` en los comandos:

```bash
podman exec web_sora-postgres_1 createdb -U sora sora_restore_check
cat ../backups/sora_YYYYMMDDTHHMMSSZ.dump | podman exec -i web_sora-postgres_1 pg_restore -U sora -d sora_restore_check --no-owner --no-privileges
podman exec web_sora-postgres_1 psql -U sora -d sora_restore_check -Atc "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public';"
podman exec web_sora-postgres_1 psql -U sora -d sora_restore_check -Atc 'SELECT count(*) FROM "__EFMigrationsHistory";'
podman exec web_sora-postgres_1 dropdb -U sora sora_restore_check
```

La comprobación se considera correcta si `pg_restore` termina sin error, las consultas devuelven conteos y la base de verificación puede eliminarse. `sora_restore_check` es una base temporal separada; no usar `sora` como destino de esta prueba.

## Recuperación de emergencia

Primero restaura y valida el archivo en una base nueva con el procedimiento anterior. Solo después de validar tablas y datos, coordina el cambio de conexión de la aplicación a la base recuperada. No ejecutes una restauración con `--clean` sobre `sora` mientras el sistema está activo: podría borrar información.
