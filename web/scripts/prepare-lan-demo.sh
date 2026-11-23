#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Uso: ./scripts/prepare-lan-demo.sh <IP-local-del-servidor>" >&2
    exit 2
fi

if [[ -e .env ]]; then
    echo "Ya existe .env; no lo sobrescribo. Protege ese archivo y edítalo manualmente si hace falta." >&2
    exit 1
fi

if [[ ! -f .env.example ]]; then
    echo "No se encontró .env.example. Ejecuta el script desde la carpeta web/." >&2
    exit 1
fi

python3 - "$1" <<'PY'
import ipaddress
import sys
ipaddress.IPv4Address(sys.argv[1])
PY

if ! command -v openssl >/dev/null 2>&1; then
    echo "Instala openssl para generar secretos locales." >&2
    exit 1
fi

db_password="$(openssl rand -hex 32)"
jwt_signing_key="$(openssl rand -hex 48)"
cp .env.example .env
sed -i \
    -e "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=${db_password}/" \
    -e "s/Password=CHANGE_THIS_LOCAL_PASSWORD/Password=${db_password}/" \
    -e "s#^Jwt__SigningKey=.*#Jwt__SigningKey=${jwt_signing_key}#" \
    -e 's/^SORA_HOST_BIND_ADDRESS=.*/SORA_HOST_BIND_ADDRESS=0.0.0.0/' \
    -e 's/^SORA_ASPNETCORE_ENVIRONMENT=.*/SORA_ASPNETCORE_ENVIRONMENT=Production/' \
    -e 's/^SORA_DEMO_DATA=.*/SORA_DEMO_DATA=true/' \
    -e "s#^SORA_WEB_ORIGIN=.*#SORA_WEB_ORIGIN=http://${1}:5173#" .env
chmod 600 .env
unset db_password jwt_signing_key
echo "Entorno demo preparado en .env (permisos 600). No compartas ni subas ese archivo."
