# Estado de implementación — DEC-LIFECYCLE-01

Base: main ca598f3. Rama: feat/resource-lifecycle. Responsable A; revisión humana B.

## Implementado

Las 30 operaciones de Identidad, Usuarios, Permisos, Vehículos/historial, Certificados y Auditoría tienen fronteras tipadas y handlers propios. Los adaptadores `IData` son exclusivos de cada slice y permiten probar reglas con NSubstitute sin simular EF. Las mutaciones mantienen locks y transacción PostgreSQL; `Mutation.RunTyped<T>` conserva DTOs en memoria y serializa únicamente el registro idempotente. El resto de módulos conserva su patrón existente.

Ocho rutas adicionales completan consultas y archivado: roles, permisos, detalle de usuario/acceso, DELETE lógico de usuario/vehículo, lista de certificados y detalle de auditoría. Total: 57 rutas de negocio. Catálogos RBAC fijos, certificados publicados y auditorías no admiten CRUD destructivo. La [matriz de 30 slices](lifecycle-slices.md) documenta contratos exactos, escenarios, archivos de contexto y verificación por ola; los escenarios Gherkin están en `behavior/` y se verifican mediante xUnit explícito.

Usuarios/vehículos incorporan `version` y `archivadoUtc`. PATCH admite `version` opcional para clientes anteriores. Cuando se envía, exige que coincida con la vigente. Las mutaciones conservan el token de concurrencia y responden 409 ante un conflicto detectado. DELETE archiva, conserva identidad/unicidad/historial, impide trabajo nuevo y revoca sesiones locales de usuarios archivados. Se protege al último administrador global y se rechazan operaciones pendientes. Reservas e inicio de inspección coordinan los locks con el archivo lógico.

Listados de usuarios, vehículos y catálogos tienen filtros/órdenes allowlist y tamaño máximo 20. Auditoría, certificados e historial admiten offset o cursor estable fecha/id. Las consultas proyectan DTOs, propagan cancelación y evitan tracking en lectura; descargar PDF valida primero autorización/estado antes de leer bytea. La verificación QR conserva cinco campos públicos y comprueba tres hashes: recibido, almacenado y recalculado.

Políticas nativas consultan AppActor cargado desde PostgreSQL; metadata editable no concede roles. JWT valida issuer, audience, firma, expiración, sub/session_id y role de transporte authenticated. Logout revoca localmente una vez antes de la llamada remota y permite reintentar con el mismo JWT propio. Refresh respeta la barrera de reset y 60 minutos exactos de inactividad, sin extender actividad. Login conserva cinco fallos/15 minutos y la recuperación conserva la intención durable antes de cambiar Auth.

`IExceptionHandler` central conserva Problem Details/codigo/traceId y el JSON especial de asistencia. Los payloads desconocidos se rechazan; límite 128 KiB y rate limiting con Retry-After. Auditoría incorpora IP y snapshots sanitizados; mutaciones y fallos críticos añaden eventos inmutables. Un fallo de persistencia de auditoría de seguridad genera log Critical sin secretos ni cuerpos de proveedor.

La migración aditiva `AddLifecycleConcurrency` combina columnas/índices y enriquecimiento de auditoría. `DurablePasswordResetRevocation` la precede. Runtime no puede DELETE usuarios/vehículos ni UPDATE/DELETE auditorías. El rollback de protección es forward-only.

## Evidencia del 14/11/2026

- `dotnet restore AutoCheck.slnx --locked-mode`: correcto.
- `dotnet build AutoCheck.slnx -c Release --no-restore -warnaserror`: cero warnings y errores.
- Suite Release en PostgreSQL 17 loopback: **147 aprobadas, 0 fallidas, 3 omitidas** (150 totales). Omisiones: una de MongoDB por falta de servicio local y dos hosted Supabase por opt-in desactivado. Log local ignorado por Git: `artifacts/lifecycle-final.log`.
- EF `has-pending-model-changes`: sin diferencias respecto a la última migración.
- Inspección MCP de Supabase: seis migraciones aplicadas; anon/authenticated sin USAGE de autocheck; runtime sin CREATE de esquema ni UPDATE/DELETE de AuditEvent. Verificados los tipos bigint/timestamptz de ciclo de vida e inet/jsonb de auditoría, y los triggers immutable_audit, immutable_certificate, immutable_manifest, immutable_inspection e immutable_rules con sus funciones protect_history/protect_certificate_manifest. No se añadieron políticas RLS ni permisos Data API.
- `--check-database` con la configuración protegida local actual falla con **28P01, password authentication failed for autocheck_api**. No se cambió la contraseña compartida ni se sustituyó el rol runtime por el migrador. La prueba anterior de CRUD revertido fue válida en aquella ejecución; no demuestra conectividad runtime vigente hoy.

## Verificación local reproducible

Desde la raíz del repositorio, usar exclusivamente un PostgreSQL loopback aislado:

```powershell
$env:AUTOCHECK_TEST_POSTGRES='Host=127.0.0.1;Port=55432;Database=autocheck_tests;Username=postgres'
dotnet restore AutoCheck.slnx --locked-mode
dotnet build AutoCheck.slnx -c Release --no-restore -warnaserror
# Ejecutar solamente si el build anterior terminó correctamente.
dotnet test AutoCheck.slnx -c Release --no-build --no-restore
```

Las fixtures destructivas rechazan hosts remotos y bases sin prefijo autocheck_tests. Para probar la proyección Atlas, configurar `AUTOCHECK_TEST_MONGO` hacia MongoDB loopback según `development-setup.md`; nunca apuntar estas fixtures al servicio compartido.

## Harness HTTP real de Supabase

Proyecto permitido: AutoCheckBD, odrsvktbllsfdqyazxsg. Ambos harness verifican URL, host/proyecto y base antes de mutar. Inician Kestrel con JWT y proveedores reales y workers locales desactivados. No crean bases, aplican migraciones, borran historial ni heredan DatabaseTest.

Configurar en el almacén protegido de AutoCheck.Api (UserSecretsId del csproj), sin incluir valores en Git, chat o artefactos:

| Clave | Uso |
|---|---|
| `ConnectionStrings:Postgres` | Credencial runtime válida para autocheck_api y TLS VerifyFull. Confirmar acceso de red y vigencia de la credencial. |
| `Supabase:Url`, `Supabase:PublishableKey` | Proyecto y clave pública ya usados por la API. |
| `SupabaseE2E:Email`, `SupabaseE2E:Password` | Cuenta Ciudadano sintética confirmada para el smoke CRUD. |
| `SupabaseE2E:AdminSecret` | Credencial Auth Admin dedicada al harness, nunca usada por la API. |
| `Certificates:PfxPath`, `Certificates:PfxPassword` | PFX académico con clave privada, fuera del repositorio. |
| `Certificates:PublicBaseUrl` | URL pública usada al construir QR. |
| `Sandbox:WebhookSecret` | HMAC del pago simulado del fixture. |
| `MongoDb:ConnectionString` | Para validar Atlas con `AUTOCHECK_ATLAS_E2E=true`, URI SRV de ClusterAC con TLS y permisos sobre `AutoCheckReports.Reports`. |

Las pruebas comprueban primero que `autocheck_api` puede conectarse y que no hay migraciones pendientes. `SupabaseLifecycleTests` usa la cuenta confirmada para crear, consultar, editar y archivar un vehículo y comprobar auditoría con un DbContext nuevo. `SupabaseFullLifecycleTests` crea tres identidades sintéticas con alias únicos del buzón E2E (debe admitir plus addressing y usuario local de hasta 48 caracteres), eleva solo su administrador de fixture en PostgreSQL con auditoría y recorre:

1. Registro/login reales y rechazo de escalación por metadata; perfil y catálogos de usuarios/roles/permisos.
2. Centro/rampa/horarios sintéticos propios, asignación de inspector y CRUD de vehículo.
3. Pago simulado mediante webhook HMAC, inspección cerrada aprobada y emisión por outbox filtrada por su aggregate.
4. Listado/detalle, descarga PDF repetida, SHA-256, firma CMS independiente, QR de cinco campos e historial.
5. Auditoría con cursor, actividad, refresh, recuperación/reset, revocación de JWT anteriores, logout y archivado lógico.

El despachador filtra por tipo y agregado sintético. Con `AUTOCHECK_ATLAS_E2E=true` y `MongoDb:ConnectionString` autorizado para `ClusterAC`, el harness también entrega `COPIA_MONGO` y compara el documento de Atlas con el cierre de PostgreSQL por ObjectId, resultado, versión de reglas y conteos. Al finalizar, bloquea las identidades nuevas en Auth, inactiva sus usuarios y sesiones locales y retira únicamente sus mensajes técnicos pendientes. Conserva cierres, certificados y auditorías. Los correos de registro y recuperación se dirigen al buzón sintético.

Después de corregir/configurar secretos:

```powershell
$env:AUTOCHECK_SUPABASE_E2E='true'
dotnet test AutoCheck.slnx -c Release --no-build --no-restore --filter 'Category=SupabaseE2E'
Remove-Item Env:AUTOCHECK_SUPABASE_E2E
```

## Pendientes de aceptación

El intento alojado del 19/11/2026 se detuvo antes de crear datos al comprobar que faltaba la cuenta sintética. También faltan la credencial Auth Admin, el PFX académico y la URI del adaptador de Atlas. La comprobación directa de PostgreSQL agotó el tiempo de conexión al pooler. El 28P01 corresponde al intento anterior. El endpoint de claves públicas de Supabase Auth respondió 200 y el conector confirmó seis migraciones.

El conector de Atlas abrió `AutoCheck / ClusterAC` (proyecto `6ae7feb8191eab6c71464edb`). El proyecto `Project 0` de la URL compartida no mostró clústeres. `ClusterAC` tiene `AutoCheckReports.Reports`. La rama anterior a la integración con el PR #12 proyectaba en `autocheck_inspections_db.reportes_defectos`. El conector aún no encontró esa colección en esa revisión. La conexión TCP directa al primer nodo agotó el tiempo desde la estación de prueba.

El 20/11/2026, Edge confirmó el pooler IPv4 de sesión de AutoCheckBD y la estación logró abrir TCP al puerto 5432. La configuración local tiene el host, el usuario con sufijo del proyecto, la base y TLS VerifyFull esperados. PostgreSQL rechazó la contraseña de `autocheck_api` con `28P01`. El conector confirmó las seis migraciones. En Atlas respondieron por TCP los tres nodos de ClusterAC y el panel señaló que la IP actual carecía de autorización. Quedaron preparados en Edge el formulario para una regla temporal de seis horas y el de una clave Supabase dedicada al harness, pendientes de confirmación. El E2E alojado requiere además las credenciales sintéticas, el PFX académico y la URI de una cuenta de Atlas con acceso al destino del adaptador.

Con la regla temporal de Atlas activa, el driver .NET autenticó `autocheck_api` con SCRAM y TLS en ClusterAC. Antes de asignar el rol de proyección, la prueba de solo lectura confirmó acceso a `AutoCheckReports.Reports` y denegación (código 8000) en `autocheck_inspections_db.reportes_defectos`. Evidencia en `artifacts/atlas-temporary-ip-scope.log`.

Atlas asignó después a `autocheck_api` el rol `autocheck_inspection_projection`, limitado a `find`, `insert` y `update` en `autocheck_inspections_db.reportes_defectos`. El adaptador real entregó y reentregó la misma inspección sintética en ClusterAC. Se verificaron el mismo ObjectId, un solo documento, la versión de reglas, el defecto y los conteos. El documento sintético se conservó como evidencia. Esta validación cubre el adaptador y su destino alojado. El E2E conjunto por HTTP, Supabase y Atlas sigue pendiente. Evidencia en `artifacts/atlas-projection-e2e.log`.

## Integración con el PR #12

La rama conserva el destino canónico `AutoCheckReports.Reports` y `MongoDb:ConnectionString` establecidos en main. El adaptador integrado escribió y repitió una proyección sintética con SCRAM/TLS. La colección canónica conservó un documento con el mismo ObjectId. La evidencia de esa ejecución está en `artifacts/atlas-integrated-projection.log`. La colección anterior contiene la evidencia histórica de una prueba previa.

La comparación OpenAPI con main terminó sin cambios incompatibles según oasdiff. La actualización aislada aplicó las nueve migraciones y conservó el vehículo, la inspección cerrada y la auditoría de la base. En la primera versión del PR, los PATCH de perfil, acceso y vehículo exigían versión. La actualización posterior acepta el cuerpo anterior y conserva la precondición para clientes que envían `version`. El E2E HTTP alojado de los seis módulos permanece pendiente de una cuenta sintética confirmada, Auth Admin y el PFX académico.

## Validación local del 19/11/2026

La suite funcional Release aprobó 149 pruebas, sin fallos ni omisiones en el filtro local. PostgreSQL 17.7 y MongoDB Community 8.0.38 se ejecutaron en loopback. La prueba nueva verifica que el cierre HTTP llega al outbox, se proyecta en MongoDB real y una segunda entrega mantiene un documento con el mismo ObjectId, defecto y conteos. Resultados en `artifacts/nov19-functional.trx`.

La línea base local sin emulación procesó 3.000 consultas de 100 sesiones sobre 10.000 inspecciones. Registró 0 fallos, p95 de 25,81 ms y páginas de hasta 4.879 bytes. Se emitieron, descargaron y verificaron criptográficamente 20 PDF distintos con firma de prueba. El máximo de emisión y descarga fue de 2.113,26 ms. La memoria máxima de 220,5 MiB corresponde al proceso conjunto de pruebas y API. Resultados en `artifacts/local-performance.trx`.

La ejecución local RS-08 usó k6 2.4.0, Kestrel y PostgreSQL 17.7 con 10.000 inspecciones y 100 sesiones sintéticas. Tras la rampa de 60 segundos, los 300 segundos sostenidos incluyeron 24.720 consultas con p95 de 231 ms y 0 fallos. La ejecución completa registró 27.219 consultas correctas y actividad de los 100 enlaces. Las respuestas se midieron sin compresión y cumplieron los límites de 20 elementos y 100 KiB. En el perfil de certificados, el worker emitió 20 PDF distintos y k6 los descargó con un máximo de 1.307 ms y 0 fallos.

Cada enlace HTTP se configuró a 150 ms de RTT, 1,6 Mbps de bajada, 750 kbps de subida y 0 % de pérdida inducida. Las sondas observaron 168–184 ms, 1,55–1,60 Mbps y 708–752 kbps, respectivamente. El equipo usó Windows con 8 CPU lógicas y 7,8 GiB de RAM. Las 59 muestras del proceso conjunto de pruebas y API registraron un pico de 202,1 MiB. Evidencia en `artifacts/rs08-54e69a45/` y `artifacts/rs08-loopback.trx`. Los resultados corresponden a loopback, con identidad sintética y firma de la fixture. La validación en un despliegue con los proveedores y el certificado académico del equipo sigue pendiente.

## Actualización del PR #13

- Perfil, acceso y vehículo aceptan las solicitudes PATCH de main que omiten `version`. Cuando el cliente envía una versión positiva, una versión obsoleta devuelve 409. Los tres casos tienen cobertura HTTP sobre PostgreSQL. El archivado conserva la versión en query.
- Supabase AutoCheckBD recibió las migraciones EF `20261117030636_SectionCHistoryIndex`, `20261117031441_SectionCDefectCatalog` y `20261117041238_SectionCQueryIndexes` con el rol migrador. Se verificaron las nueve entradas del historial EF, cinco índices y 9 categorías con 18 subcategorías. La API levantada con la conexión runtime de AutoCheckBD respondió 200 tanto en `/health` como en `/health/ready`.
- El recorrido alojado completo sigue sujeto a la identidad sintética, Auth Admin, la firma académica y la URI de Atlas descritas en el harness.
