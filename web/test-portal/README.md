# Portal independiente de pruebas Sora

Este portal está fuera de `src/Sora.Blazor`: no es parte del producto Sora y no necesita la API para abrir la página. Presenta la matriz de procedimientos y el último resultado anonimizado que generó la suite.

Desde `web/`:

```bash
./scripts/run-quality-suite.sh
python3 -m http.server 8765 --directory test-portal
```

Abre `http://localhost:8765`. Para las pruebas hace falta Podman; el script crea/usa solo la base aislada `sora_it`. También requiere .NET SDK 10 en el host, o usa la imagen oficial del SDK dentro de Podman si no está instalado. Python 3 sirve para comprobaciones API y publicación del reporte.

La suite exporta `test-portal/data/latest.json`; se puede versionar como evidencia de la ejecución si se desea. No contiene credenciales ni tokens. Cada caso que no se haya ejecutado aparece como pendiente; los avisos simulados figuran fuera de alcance porque esta versión los retiró. Cobertura se muestra únicamente si Coverlet produjo datos.

La base y el portal funcionan independientemente del despliegue normal. No configures el proyecto de demostración ni `web_sora-postgres-data` como destino de pruebas.
