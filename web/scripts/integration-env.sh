#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WEB_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$WEB_DIR"

PROJECT="sora-it"
NETWORK="sora-it_default"
VOLUME="sora_it_postgres_data"
API_CONTAINER="sora-it-api"
DB_CONTAINER="sora-it-postgres"
API_IMAGE="localhost/sora-integration-api:latest"
ENV_FILE=".env.integration"
API_URL="http://127.0.0.1:5251"
DB_NAME="sora_it"
DB_USER="sora_it"

usage() {
    cat <<'EOF'
Entorno de integración aislado para Sora (Podman directo; no usa podman-compose).

Uso:
  ./scripts/integration-env.sh up      Construye la imagen de pruebas, levanta PostgreSQL/API,
                                       aplica migraciones y carga datos ficticios.
  ./scripts/integration-env.sh status  Verifica contenedores, DB, migraciones, cuentas y API.
  ./scripts/integration-env.sh down    Elimina SOLO los contenedores de integración; conserva DB.
  ./scripts/integration-env.sh reset   Elimina SOLO el volumen sora_it_postgres_data.

API: http://127.0.0.1:5251   PostgreSQL: 127.0.0.1:55433
El entorno no lee .env y no comparte puertos, red, volumen ni etiqueta de imagen con la demo.
EOF
}

fail() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

for tool in podman openssl curl; do
    command -v "$tool" >/dev/null 2>&1 || fail "No se encontró el comando requerido: $tool"
done

create_env() {
    if [[ ! -e "$ENV_FILE" ]]; then
        local db_password jwt_key
        db_password="$(openssl rand -hex 24)"
        jwt_key="$(openssl rand -hex 48)"
        cat >"$ENV_FILE" <<EOF
SORA_IT_POSTGRES_DB=$DB_NAME
SORA_IT_POSTGRES_USER=$DB_USER
SORA_IT_POSTGRES_PASSWORD=$db_password
SORA_IT_JWT_SIGNING_KEY=$jwt_key
EOF
        chmod 600 "$ENV_FILE"
        printf 'Creada configuración local aislada: %s (permisos 600).\n' "$ENV_FILE"
    fi
    chmod 600 "$ENV_FILE"

    local key value extra
    local seen_db=0 seen_user=0 seen_password=0 seen_jwt=0
    while IFS='=' read -r key value extra || [[ -n "${key:-}${value:-}${extra:-}" ]]; do
        [[ -z "${extra:-}" ]] || fail "$ENV_FILE contiene una línea no válida."
        case "$key" in
            SORA_IT_POSTGRES_DB)
                [[ "$value" == "$DB_NAME" ]] || fail "La base de integración debe llamarse exactamente $DB_NAME."
                export SORA_IT_POSTGRES_DB="$value"; seen_db=1 ;;
            SORA_IT_POSTGRES_USER)
                [[ "$value" == "$DB_USER" ]] || fail "El usuario de PostgreSQL de prueba debe llamarse exactamente $DB_USER."
                export SORA_IT_POSTGRES_USER="$value"; seen_user=1 ;;
            SORA_IT_POSTGRES_PASSWORD)
                [[ "$value" =~ ^[0-9a-f]{48}$ ]] || fail "La clave PostgreSQL de prueba no tiene el formato esperado."
                export SORA_IT_POSTGRES_PASSWORD="$value"; seen_password=1 ;;
            SORA_IT_JWT_SIGNING_KEY)
                [[ "$value" =~ ^[0-9a-f]{96}$ ]] || fail "La clave JWT de prueba no tiene el formato esperado."
                export SORA_IT_JWT_SIGNING_KEY="$value"; seen_jwt=1 ;;
            '') ;;
            *) fail "$ENV_FILE contiene una clave no permitida: $key" ;;
        esac
    done < "$ENV_FILE"
    [[ $seen_db == 1 && $seen_user == 1 && $seen_password == 1 && $seen_jwt == 1 ]] || fail "$ENV_FILE está incompleto."
}

verify_target() {
    [[ "$SORA_IT_POSTGRES_DB" == "$DB_NAME" && "$SORA_IT_POSTGRES_USER" == "$DB_USER" ]] || fail "Destino de base inesperado."
    [[ "$NETWORK" == "sora-it_default" && "$VOLUME" == "sora_it_postgres_data" ]] || fail "La red o el volumen no coinciden con los valores protegidos."
    [[ "$API_URL" == "http://127.0.0.1:5251" ]] || fail "La API de prueba debe quedar enlazada solo a localhost:5251."
    if podman volume exists web_sora-postgres-data >/dev/null 2>&1; then
        [[ "$VOLUME" != "web_sora-postgres-data" ]] || fail "El volumen coincide con el volumen de la demostración."
    fi
}

ensure_owned_volume() {
    if ! podman volume exists "$VOLUME" >/dev/null 2>&1; then
        podman volume create --label io.sora.project="$PROJECT" "$VOLUME" >/dev/null
    fi
    local name labels
    name="$(podman volume inspect --format '{{.Name}}' "$VOLUME")"
    labels="$(podman volume inspect --format '{{.Labels}}' "$VOLUME")"
    [[ "$name" == "$VOLUME" ]] || fail "Nombre de volumen inesperado; no se usará."
    [[ "$labels" == *"sora-it"* ]] || fail "El volumen existente no está identificado como volumen de integración; no se usará ni eliminará."
}

ensure_owned_network() {
    if ! podman network exists "$NETWORK"; then
        podman network create --label io.sora.project="$PROJECT" "$NETWORK" >/dev/null
    fi
    local labels
    labels="$(podman network inspect --format '{{.Labels}}' "$NETWORK")"
    [[ "$labels" == *"sora-it"* ]] || fail "La red existente no está identificada como red de integración."
}

assert_owned_container() {
    local name labels
    name="$1"
    if podman container exists "$name"; then
        labels="$(podman inspect --format '{{.Config.Labels}}' "$name")"
        [[ "$labels" == *"sora-it"* ]] || fail "Existe un contenedor llamado $name que no pertenece al entorno de integración."
    fi
}

remove_test_containers() {
    assert_owned_container "$API_CONTAINER"
    assert_owned_container "$DB_CONTAINER"
    podman rm -f "$API_CONTAINER" >/dev/null 2>&1 || true
    podman rm -f "$DB_CONTAINER" >/dev/null 2>&1 || true
}

confirm_db_identity() {
    local actual migration_count demo_users
    actual="$(podman exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -Atc 'select current_database() || chr(58) || current_user' 2>/dev/null)" || fail "No se pudo consultar la identidad de PostgreSQL de prueba."
    [[ "$actual" == "$DB_NAME:$DB_USER" ]] || fail "Identidad inesperada de base de prueba: $actual"
    migration_count="$(podman exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -Atc 'select count(*) from "__EFMigrationsHistory"' 2>/dev/null)" || fail "No se encontró el historial de migraciones."
    [[ "$migration_count" =~ ^[0-9]+$ && "$migration_count" -gt 0 ]] || fail "La base aislada aún no tiene migraciones aplicadas."
    demo_users="$(podman exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -Atc "select count(*) from staff_accounts where email in ('ADMIN@SORA.DEMO','VENDEDOR@SORA.DEMO','ALMACEN@SORA.DEMO','DESPACHO@SORA.DEMO','AYUDANTE@SORA.DEMO','REPARTIDOR@SORA.DEMO')" 2>/dev/null)" || fail "No se pudo comprobar la semilla de cuentas ficticias."
    [[ "$demo_users" =~ ^[0-9]+$ && "$demo_users" -ge 6 ]] || fail "Faltan las cuentas ficticias de prueba."
    printf 'Destino verificado: base=%s usuario=%s contenedor=%s volumen=%s.\n' "$DB_NAME" "$DB_USER" "$DB_CONTAINER" "$VOLUME"
    printf 'Migraciones verificadas: %s; cuentas ficticias verificadas: %s.\n' "$migration_count" "$demo_users"
}

build_isolated_image() {
    local demo_image_before=""
    if podman image exists localhost/web_sora-api:latest; then
        demo_image_before="$(podman image inspect --format '{{.Id}}' localhost/web_sora-api:latest)"
    fi
    podman build -f "$WEB_DIR/Containerfile.api" -t "$API_IMAGE" "$WEB_DIR"
    if [[ -n "$demo_image_before" ]]; then
        local demo_image_after
        demo_image_after="$(podman image inspect --format '{{.Id}}' localhost/web_sora-api:latest)"
        if [[ "$demo_image_before" != "$demo_image_after" ]]; then
            podman tag "$demo_image_before" localhost/web_sora-api:latest
            fail "La construcción cambió la etiqueta de imagen de la demo; se restauró a su imagen anterior y no se iniciaron pruebas."
        fi
    fi
}

start_env() {
    create_env
    verify_target
    ensure_owned_volume
    ensure_owned_network
    build_isolated_image
    remove_test_containers

    printf 'Levantando PostgreSQL de prueba en red exclusiva %s.\n' "$NETWORK"
    podman run -d --name "$DB_CONTAINER" \
        --label io.sora.project="$PROJECT" \
        --label io.podman.compose.project="$PROJECT" \
        --network "$NETWORK" --network-alias "$DB_CONTAINER" \
        --publish 127.0.0.1:55433:5432 \
        --env "POSTGRES_DB=$DB_NAME" --env "POSTGRES_USER=$DB_USER" \
        --env "POSTGRES_PASSWORD=$SORA_IT_POSTGRES_PASSWORD" \
        --volume "$VOLUME:/var/lib/postgresql/data" \
        --health-cmd "pg_isready -U $DB_USER -d $DB_NAME" \
        --health-interval 3s --health-timeout 3s --health-retries 20 \
        --log-driver k8s-file docker.io/library/postgres:17-alpine >/dev/null

    local attempt
    for attempt in $(seq 1 60); do
        if podman exec "$DB_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then break; fi
        sleep 2
    done
    podman exec "$DB_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1 || fail "PostgreSQL de prueba no quedó disponible."

    printf 'Levantando API de prueba en %s.\n' "$API_URL"
    podman run -d --name "$API_CONTAINER" \
        --label io.sora.project="$PROJECT" \
        --label io.podman.compose.project="$PROJECT" \
        --network "$NETWORK" \
        --publish 127.0.0.1:5251:8080 \
        --env ASPNETCORE_ENVIRONMENT=Development \
        --env ASPNETCORE_HTTP_PORTS=8080 \
        --env "ConnectionStrings__Postgres=Host=$DB_CONTAINER;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$SORA_IT_POSTGRES_PASSWORD;SSL Mode=Disable" \
        --env Database__MigrateOnStartup=true \
        --env DemoData__Enabled=true \
        --env Jwt__Issuer=Sora.Api.IntegrationTests \
        --env Jwt__Audience=Sora.Api.IntegrationTests \
        --env "Jwt__SigningKey=$SORA_IT_JWT_SIGNING_KEY" \
        --env Jwt__ExpiresMinutes=30 \
        --env Cors__Origins__0=http://localhost:5251 \
        --log-driver k8s-file "$API_IMAGE" >/dev/null

    for attempt in $(seq 1 60); do
        if curl --silent --show-error --fail "$API_URL/api/health" >/tmp/sora-it-health.json 2>/dev/null && grep -q '"status":"ok"' /tmp/sora-it-health.json; then
            confirm_db_identity
            printf 'API de integración saludable: %s/api/health\n' "$API_URL"
            printf 'Datos ficticios disponibles: admin@sora.demo / SoraDemo2026! (solo esta base aislada).\n'
            rm -f /tmp/sora-it-health.json
            return 0
        fi
        sleep 2
    done
    rm -f /tmp/sora-it-health.json
    fail "La API no quedó saludable. La base aislada se conserva para diagnóstico; consulta podman logs $API_CONTAINER."
}

status_env() {
    if [[ ! -f "$ENV_FILE" ]]; then
        printf 'El entorno aislado aún no fue creado. Ejecuta: %s up\n' "$0"
        exit 0
    fi
    create_env
    verify_target
    ensure_owned_volume
    ensure_owned_network
    podman ps -a --filter name=sora-it --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}'
    confirm_db_identity
    curl --silent --show-error --fail "$API_URL/api/health" || fail "La API de prueba no responde."
    printf '\n'
}

stop_env() {
    create_env
    verify_target
    ensure_owned_volume
    ensure_owned_network
    remove_test_containers
    printf 'Contenedores de prueba eliminados; red %s y volumen %s conservados.\n' "$NETWORK" "$VOLUME"
}

reset_env() {
    create_env
    verify_target
    ensure_owned_volume
    ensure_owned_network
    printf 'Se borrará únicamente la base aislada del proyecto %s (%s). No se toca el volumen de demostración.\n' "$PROJECT" "$VOLUME"
    remove_test_containers
    local actual_volume
    actual_volume="$(podman volume inspect --format '{{.Name}}' "$VOLUME")"
    [[ "$actual_volume" == "$VOLUME" ]] || fail "Nombre de volumen inesperado; se cancela la limpieza."
    podman volume rm "$VOLUME"
    printf 'Base aislada eliminada. Ejecuta “up” para crear una base vacía y recargar datos ficticios.\n'
}

action="${1:-help}"
case "$action" in
    up) start_env ;;
    status) status_env ;;
    down) stop_env ;;
    reset) reset_env ;;
    help|-h|--help) usage ;;
    *) usage >&2; fail "Acción desconocida: $action" ;;
esac
