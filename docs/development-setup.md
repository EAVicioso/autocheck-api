# Supabase + API: desarrollo del equipo

Revisión del 12 de noviembre de 2026. Responsables: Sebastián (A), Manuel (B), Engels (C).
Preparación común de persistencia/documentación para RF de las slices, RD-04, RS-03,
PA-02/03/04/05/11; no constituye aceptación funcional de todas las features.

## Estado verificado

Proyecto **AutoCheckBD**, ref `odrsvktbllsfdqyazxsg`, PostgreSQL 17, región `us-west-2`.
Las 21 tablas autoritativas están en `autocheck`, con cuatro migraciones EF aplicadas,
catálogos mínimos y reglas V1/M=3. Se conservaron las 17 tablas antiguas de `public`
y sus cuatro roles. No había usuarios/datos operativos para importar.

La Data API estaba desactivada; se conserva así. `anon` y `authenticated` no tienen
USAGE sobre `autocheck`. No se publica el esquema ni se replica información sensible
para conectar EF. RLS y políticas del esquema antiguo se conservaron.

Se habilitó **Enforce SSL on incoming connections**. Npgsql usa **VerifyFull** y
la [CA pública oficial](../certs/README.md), validando cadena y nombre del servidor.
Pooler de **sesión** IPv4, verificado en Connect del panel:
`aws-1-us-west-2.pooler.supabase.com:5432`, base `postgres`.
No usar el puerto de transacciones 6543 para migraciones ni copiar una URL de URI
como si fuera una cadena Npgsql de pares `clave=valor`.

| Identidad | Uso y límites |
|---|---|
| `autocheck_api` | HTTP/workers. DML en `autocheck`, lectura del ledger EF. Sin CREATE, TRUNCATE, superusuario, creación de roles/bases ni bypass RLS. Auditoría/reglas solo permiten SELECT/INSERT; certificados no permiten DELETE. Triggers protegen historia cerrada. Límite SQL de 15 conexiones. |
| `autocheck_migrations` | Solo mantenimiento. Propietario de objetos de `autocheck`, DML del ledger EF; CREATE en `public` porque Npgsql ejecuta `CREATE TABLE IF NOT EXISTS` aun cuando el ledger existe. Sin acceso a credenciales de `auth` ni privilegios administrativos del clúster. Límite de 3 conexiones. |

Contraseñas aleatorias SCRAM distintas, fuera de Git; configuración local de Sebastián
guardada en el almacén .NET user-secrets. Ese almacén es local al usuario y no cifra
el archivo: proteger la cuenta/equipo y no copiar su contenido a chats, PRs o logs.
El rol de migración no se distribuye por defecto a quienes solo prueban endpoints.
El administrador conserva propiedad del ledger `public.__EFMigrationsHistory`;
RLS y ACL bloquean clientes públicos, y la API solo lo consulta para readiness.

## Primera ejecución de cada integrante

1. Obtener esta rama/PR y usar el SDK de `global.json` y PowerShell 7.
2. Pedir al mantenedor la contraseña de `autocheck_api` por canal seguro y la clave
   **publishable** desde Supabase → Project Settings → API Keys.
3. Desde la raíz del repo:

   ```powershell
   pwsh -File scripts/setup-development.ps1
   dotnet run --project AutoCheck.Api
   ```

4. Abrir [Scalar](http://localhost:5004/scalar/) y
   [readiness](http://localhost:5004/health/ready). Debe devolver 200/`Ready`.

El script escribe user-secrets por stdin, nunca pasa contraseñas como argumentos,
restaura herramientas y paquetes fijados, compila y ejecuta el diagnóstico EF.
`-UseExistingSecrets` repite la verificación sin pedir ni cambiar credenciales.
`-ConfigureMigrations` solicita además la contraseña privilegiada; usarlo únicamente
si corresponde mantener migraciones. Cada máquina resuelve la ruta absoluta de la CA.

Cada instancia de desarrollo usa un pool de **4** conexiones: tres integrantes pueden
usar hasta 12, por debajo del pool compartido de 15 y con margen para diagnóstico.
Para varias réplicas o más desarrolladores, revisar el presupuesto total antes de
subir el pool. Timeouts: conexión 10s, comando 30s, inactividad de conexiones 60s,
keepalive 30s. Los límites configurables se validan y no hay fallback silencioso a
otra base. No se habilita replay automático de transacciones con efectos externos.

El proceso normal no migra ni cambia roles. El diagnóstico explícito:

```powershell
dotnet run --project AutoCheck.Api -- --check-database
```

comprueba el ledger, consultas LINQ, INSERT/UPDATE/DELETE y rollback de un registro
sintético. No crea usuarios, citas o inspecciones, ni arranca workers.

## Scalar y pruebas de endpoints

La documentación interactiva está en `/scalar/`, con recursos JS servidos por el
paquete fijado `Scalar.AspNetCore` (sin depender de un CDN para el bundle).
El contrato sigue en `/openapi/v1.json`; no se usa Swagger UI/Swashbuckle.
Ambas rutas se habilitan en Development/Testing o con `OpenApi__Enabled=true`.
Producción las mantiene desactivadas salvo habilitación deliberada.

Usar `POST /api/auth/login`; copiar su `accessToken` al campo Bearer de Scalar.
No introducir contraseñas SQL, secret/service_role keys o refresh tokens en Bearer.
Los permisos funcionales se resuelven en PostgreSQL; un token con metadata de admin
no concede privilegios. La primera cuenta administrativa se vincula mediante
`--bootstrap-admin` a una identidad Supabase ya creada/verificada, como indica README.
No se crearon cuentas administrativas ficticias durante esta preparación.

El backend ya contiene endpoints del PR #1. Cada responsable debe desarrollar y
revisar su slice, conservando trazabilidad RF/CU/PA, validación y autorización por
recurso. Consultar `docs/endpoints.md` y el seguimiento de la revisión de Engels.

## Migraciones y permisos futuros

EF es la fuente de verdad del esquema de aplicación. No editar el esquema manualmente
desde Table Editor ni crear un segundo historial paralelo de migraciones nativas
Supabase. La preparación inicial se registró también en el ledger de Supabase como
bootstrap administrativo; el estado que verifica la API es el historial EF.

```powershell
dotnet tool restore
dotnet ef migrations add Nombre --project AutoCheck.Api --context AutoCheckDb --output-dir Infrastructure/Data/Migrations
dotnet ef migrations script --idempotent --project AutoCheck.Api --context AutoCheckDb --output artifacts/migration.sql
# Después de revisar SQL, destino y PR:
dotnet run --project AutoCheck.Api -- --migrate
dotnet run --project AutoCheck.Api -- --check-database
```

Servidor y CLI comparten la misma configuración. `--migrate`/la fábrica de diseño
usan `ConnectionStrings:PostgresMigrations` si existe; las solicitudes HTTP y
`--check-database` siempre usan `ConnectionStrings:Postgres`. Usar el rol migrador
para nuevas tablas mantiene los default grants al rol API. `scripts/supabase-access.sql`
documenta el aprovisionamiento idempotente inicial sin contraseñas; ejecutarlo requiere
el administrador de Supabase. Si una migración la aplica otro propietario, revisar
grants explícitos antes de entregar.

En Azure, definir ambas conexiones en configuración protegida y el entorno correcto;
usar `Root Certificate=/app/certs/supabase-prod-ca-2021.crt` (la CA pública ya se
incluye en publish/Docker). El rol migrador se proporciona
solo al paso de despliegue. Este trabajo no despliega una nueva instancia Azure ni
afirma que su configuración remota esté comprobada.

## Auth, Realtime y dependencias

Auth revisado: email habilitado, confirmación requerida, registro habilitado,
anónimos deshabilitados; JWKS publica clave ES256. Site URL de desarrollo:
`http://localhost:3000`. No se ampliaron redirects con comodines.

**SMTP implementado en el PR #11**: la API usa MailKit y el outbox; Supabase Auth
usa custom SMTP del mismo proveedor con una clave Mail Send independiente.
Configurar y comprobar siguiendo [SMTP/SendGrid](smtp-setup.md) y
`scripts/configure-smtp.ps1`. No desactivar confirmación de email para eludir
fallos del proveedor. `--check-smtp` verifica TLS y autenticación sin enviar correo.
Las claves de Gemini, MongoDB Atlas, secret key para broadcast Realtime y
PFX académico siguen siendo configuración de cada integración.
Los consumidores no configurados conservan los trabajos pendientes con reintentos.
`Workers__Enabled=false` sirve para sesiones que no deben consumir tareas compartidas;
coordinar al menos una instancia con workers y proveedores para los flujos asíncronos.

Realtime de disponibilidad usa **broadcast de invalidación** por `{centroId}`;
no requiere publicar las tablas privadas ni activar replicación de datos personales.
Storage no tiene buckets y el certificado permanece privado en PostgreSQL según
la decisión aprobada. No se crearon buckets públicos ni Edge Functions innecesarias.

## Verificación y límites

Conexión real desde EF como `autocheck_api`: 0 migraciones pendientes, 4 catálogos,
1 versión de regla, CRUD con rollback, `CanCreateSchemaObjects=false`.
CLI EF contra Supabase con la identidad de migración: base al día, sin reaplicar cambios.
Asesor de seguridad: **0 avisos**. Los índices sin uso son informativos por falta de
tráfico representativo; no se eliminan índices de integridad por ese aviso.

Pruebas destructivas de integración únicamente en PostgreSQL loopback con prefijo
`autocheck_tests`, nunca contra esta base compartida. CI suministra PostgreSQL/MongoDB
efímeros. Las pruebas SMTP de loopback acreditan el transporte TLS y outbox; la entrega externa requiere remitente/cuenta verificados. Los dobles no acreditan IA, Atlas, firma real o RS-08.
No se compraron proyectos, ramas, plan de pago, backups o add-ons. El plan Free y la
ausencia de backups gestionados requieren una decisión antes de datos de producción.

Referencias: [conexión Supabase](https://supabase.com/docs/guides/database/connecting-to-postgres),
[parámetros Npgsql](https://www.npgsql.org/doc/connection-string-parameters.html),
[Scalar ASP.NET Core](https://scalar.com/products/api-references/integrations/aspnetcore/integration).
