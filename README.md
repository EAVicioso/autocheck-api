# AutoCheck API

Prototipo académico del INTEC, SRS aprobado 1.1. .NET 10 Minimal APIs, vertical slices, PostgreSQL/Supabase, outbox y proyección MongoDB. Pagos y certificados son académicos.

- [Guía compartida VSA, .NET 10 y C# 14](docs/architecture/AI_Backend_Architecture_Guidelines.md)
- [Implementación, decisiones y reparto A/B/C](docs/implementation.md)
- [Observaciones de Engels y seguimiento del PR #1](docs/reviews/pr-1-follow-up.md)
- [Endpoints y trazabilidad](docs/endpoints.md)
- [Activar protección y revisión automática](docs/github-setup.md)
- [Validación de rendimiento](tests/load/README.md)

## Desarrollo

Responsables: **A Sebastián Tavares**, **B Manuel Polo**, **C Engels Vicioso**. La guía de arquitectura versionada es la fuente común para el equipo y sus agentes. Al clonar/actualizar el repositorio todos reciben el mismo contexto. Codex lo encuentra desde `AGENTS.md`, Copilot desde `.github/copilot-instructions.md`, Claude Code desde `CLAUDE.md`; con otros agentes, incluir explícitamente la guía en su contexto. Los enlaces no garantizan que toda herramienta la cargue automáticamente: comprobarlo al iniciar la tarea.

Requiere el SDK de `global.json` y PostgreSQL 17. Configurar secretos mediante variables de entorno o `dotnet user-secrets` en el proyecto API; no editar el código con credenciales.

```powershell
dotnet restore AutoCheck.slnx --locked-mode
dotnet build AutoCheck.slnx -c Release --no-restore -warnaserror
dotnet run --project AutoCheck.Api -- --migrate
dotnet run --project AutoCheck.Api
```

Las migraciones se aplican únicamente con `--migrate`, nunca al iniciar el servidor normalmente. Revisar el destino antes de ejecutar el comando. `/health` comprueba el proceso; `/health/ready` comprueba PostgreSQL y que no queden migraciones pendientes. OpenAPI: `/openapi/v1.json` en desarrollo o con `OpenApi__Enabled=true`.

La primera cuenta administrativa se vincula a una identidad **ya creada y verificada en Supabase**: configurar `Bootstrap__AuthId` y `Bootstrap__Email`, y ejecutar `dotnet run --project AutoCheck.Api -- --bootstrap-admin`. Este comando es administrativo, no un endpoint público. Los demás usuarios se registran como ciudadanos; el administrador cambia los accesos de inspectores y administradores locales.

## Configuración

| Variable | Uso |
|---|---|
| `ConnectionStrings__Postgres` | Conexión privada PostgreSQL del backend. |
| `Supabase__Url`, `Supabase__PublishableKey` | Identidad; JWT firmados y claves JWKS rotativas. |
| `Supabase__SecretKey` | Publicación REST de disponibilidad, solo en el servidor. |
| `Mongo__ConnectionString` | Copia asíncrona en `autocheck_inspections_db.reportes_defectos`. |
| `SendGrid__ApiKey`, `SendGrid__From` | Confirmaciones y cancelaciones. |
| `Gemini__ApiKey`, `Gemini__Model` | Asistencia; modelo predeterminado `gemini-2.5-flash`. |
| `Certificates__PfxPath`, `Certificates__PfxPassword` | Clave privada académica montada fuera de la imagen. |
| `Certificates__PublicBaseUrl` | Base HTTPS de la API pública para los QR. |
| `Sandbox__WebhookSecret` | Secreto de al menos 32 caracteres para HMAC-SHA256 del cuerpo UTF-8 exacto. |
| `Sandbox__Monto` | Monto simulado, predeterminado 1000 DOP; no representa una tarifa oficial. |
| `AllowedOrigins` | Orígenes web separados por comas. Predeterminado `http://localhost:3000`. |
| `Workers__Enabled` | `false` para desactivar consumidores en exportación OpenAPI/pruebas. |

Los proveedores no configurados fallan de forma controlada y dejan efectos asíncronos pendientes. No existe una autenticación de desarrollo que pueda activarse en producción. `Testing__JwtKey` se acepta exclusivamente en el entorno `Testing`.

## Pruebas

```powershell
dotnet test AutoCheck.slnx -c Release --filter 'Category!=Integration'
# El destino debe ser loopback y el nombre comenzar por autocheck_tests.
$env:AUTOCHECK_TEST_POSTGRES = 'Host=127.0.0.1;Database=autocheck_tests;Username=postgres;Password=<clave-local>'
$env:AUTOCHECK_TEST_MONGO = 'mongodb://127.0.0.1:27017'
dotnet test AutoCheck.slnx -c Release
```

Las pruebas crean y eliminan bases propias con sufijo aleatorio. Rechazan destinos remotos. CI suministra sus propios servicios efímeros y falla si falta configuración requerida. Supabase, SendGrid y Gemini se sustituyen por dobles en CI; la firma PDF se prueba criptográficamente con una clave académica temporal.

Docker: `docker build -f AutoCheck.Api/Dockerfile -t autocheck-api:local .`. La imagen usa un usuario sin privilegios y no incluye secretos, herramientas ni artefactos de prueba.
