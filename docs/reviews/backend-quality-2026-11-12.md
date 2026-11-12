# Revisión del backend: VSA, Sonar y seguridad

Fecha: 12/11/2026. Alcance: API y Supabase para desarrollo del prototipo SRS 1.1. No se certifica preparación para producción ni cumplimiento absoluto de OWASP. SMTP se pospone por decisión del equipo.

## Herramientas y resultado reproducible

- SDK .NET 10.0.403 / C# 14; Microsoft Code Analysis `10-recommended`, estilo en compilación y warnings como errores.
- SonarQube for Visual Studio 11.3.0.17527, firmado por SonarSource, instalado con autorización del usuario. Análisis local del IDE al abrir archivos; sin vincular ni subir código a un servidor Sonar.
- `SonarAnalyzer.CSharp` 10.36.0.15337, dependencia privada compartida por ambos proyectos: análisis completo en compilación, también en CI. Esto complementa el alcance por archivo del IDE; no es una ejecución de SonarQube Server ni de sus reglas comerciales de taint analysis.
- La línea base tenía 128 advertencias únicas de los analizadores activados. Se corrigieron estructura de bloques, nombres/parámetros, campos de fixtures, cultura de fechas/números, logging, expresiones regulares y código innecesario.
- Compilación Release: **0 errores y 0 advertencias**, sin `NoWarn`, desactivar reglas globales o rebajar su severidad. Las nueve fuentes EF generadas se conservan intactas y se identifican como generadas en `.editorconfig`; su SQL se revisa y se ejecuta en pruebas de creación/upgrade PostgreSQL.

```powershell
dotnet restore AutoCheck.slnx --locked-mode
dotnet build AutoCheck.slnx -c Release --no-restore -warnaserror -p:AutoCheckEmitAnalysis=true
dotnet test AutoCheck.slnx -c Release --no-restore
dotnet package list --project AutoCheck.slnx --vulnerable --include-transitive --format json --no-restore
```

El build opcional genera `artifacts/analysis/AutoCheck.Api.sarif` y `AutoCheck.Tests.sarif`; CI conserva estos archivos siete días. Los resultados TRX, contratos exportados, compatibilidad de base, Docker y CodeQL están asociados al commit del PR. No versionar logs de proveedores, tokens o claves de pruebas.

## Cobertura de carpetas

Se inventariaron 128 fuentes de la API (incluidas nueve generadas) y 11 de pruebas. El análisis de compilación cubre ambos proyectos completos. Se revisó cada recurso con sus subcarpetas de casos y `Common`; los routers dejaron de contener reglas y consultas.

| Carpeta | Revisión y resultado |
|---|---|
| `Features/Auth` | Siete casos: validación, bloqueo de cuenta, JWT/sesión, recuperación y revocación; contratos sin credenciales en errores/auditoría. |
| `Features/Usuarios` | Cuatro casos: permisos funcionales desde PostgreSQL, gestión restringida y DTOs explícitos. |
| `Features/Vehiculos` | Cinco casos: propiedad, catálogos válidos, unicidad, filtros paginados y proyecciones. |
| `Features/Centros` | Ocho casos: alcance de centro, locks/compromisos, horarios, rampas y disponibilidad; corregidos elementos nulos y overflow de fecha. |
| `Features/Citas` | Cinco casos: propiedad/asignación, cancelación e idempotencia, locks compartidos y notificación por outbox. |
| `Features/Pagos` | Tres casos: pago académico, HMAC de bytes exactos, deduplicación y expiración estricta; mantenido workflow transaccional compartido del recurso. |
| `Features/Inspecciones` | Nueve casos: permisos, concurrencia optimista, reglas congeladas, cierre atómico y lista de defectos validada antes de persistir. |
| `Features/Certificados` | Cuatro casos: reserva idempotente, PDF firmado inmutable, descarga privada y QR público limitado; verificador CMS independiente. |
| `Features/Reportes` | Dos casos: alcance administrativo, días locales, ventanas de 21, agregados y filtros. Consolidación trasladada al servidor PostgreSQL. |
| `Features/Auditorias` | Un caso: permiso específico, filtros parametrizados y paginación. |
| `Features/Chatbot` | Un caso: contrato exacto, entrada/salida acotadas, timeout/contingencia y sin herramientas transaccionales. Regex de modelo con límite y timeout. |
| `Features/Notificaciones`, `Features/Reinspecciones` | Directorios históricos sin código; las implementaciones viven en adaptadores/outbox e Inspecciones. No se añadieron clases vacías. |
| `Infrastructure/Http`, `Diagnostics` | Errores y validación conectada, headers, JSON, logging estructurado seguro con `LoggerMessage`. |
| `Infrastructure/Data`, `Data/Migrations` | EF/Npgsql, TLS, pools/timeouts, roles separados, locks, transacciones, invariantes y ledger; no se reescribieron migraciones aplicadas. |
| `Infrastructure/Security`, `Time` | Issuer/audience/firma/lifetime/JWKS, roles y permisos leídos de DB, reloj inyectado y zona horaria explícita. |
| `Infrastructure/Integrations`, outbox | Cliente Mongo singleton y pool acotado; HTTP clients con timeouts, entrega al menos una vez, upsert, rollback/backoff y separación de consumidores. |
| Raíz API, `Properties`, `Assets` | Composición explícita, límite de cuerpo, CORS, autorización por defecto, Scalar/OpenAPI solo habilitados por entorno/configuración; referencia del chatbot y licencia de fuente. |
| `AutoCheck.Tests` y todas sus subcarpetas | Contratos, reglas, configuración, HTTP real/Kestrel, PostgreSQL, Mongo y verificación criptográfica; datos y claves sintéticos aislados. |
| `scripts`, `tests/load`, `.github` | Setup protegido, auditoría de dependencias, upgrade/API compatibility, imagen sin privilegios, acciones fijadas, CI obligatorio y arnés de carga existente. |
| `.agents`, documentación | Estándar compartido actualizado a la estructura real; eliminada afirmación obsoleta de que no existía CI. |

## Decisiones de refactorización

Las **49 operaciones de negocio** están en `Features/{Recurso}/{CasoDeUso}/{CasoDeUso}Endpoint.cs`; salud/readiness completan 51 operaciones OpenAPI. Cada caso conserva ruta, DTOs exclusivos, handler y validación juntos. `Common` contiene solo contratos/reglas reutilizados del recurso. No se introducen repositorios genéricos, mediadores, controllers, buses ni proyectos adicionales.

`ApiContract` usa un bloque `extension` de C# 14 para agrupar `Document`/`Validated` sobre `RouteHandlerBuilder`; dependencias sencillas usan constructores primarios y DTOs records. Se expandieron sentencias comprimidas a bloques legibles. No se fuerza `field` en propiedades que no lo necesitan.

El adaptador Mongo reutiliza un cliente singleton con creación diferida y pool máximo de 20; evita construir pools por cada reintento. Reportes usa agregación, ventanas y upsert en PostgreSQL: evita transferir todo el historial a memoria y borrar/reinsertar métricas. La reconciliación completa conserva commits tardíos y debe perfilarse a escala; no se declara aceptación de rendimiento sin medición.

## OWASP Top 10:2025

| Categoría | Control revisado/corrección y evidencia |
|---|---|
| A01 Broken Access Control | Roles/permisos/centro autoritativos desde DB, propiedad y asignación; fallback de autenticación para rutas futuras. Prueba enumera las 49 operaciones, limita a siete las públicas de negocio y comprueba 401 de todas las protegidas antes de binding/DB. Pruebas de otro propietario y rol JWT falsificado. |
| A02 Security Misconfiguration | TLS VerifyFull remoto, CORS explícito, documentación desactivada por defecto en producción, límite 128 KiB, headers `nosniff`, `DENY`, `no-referrer`; `/api` sin caché, incluso errores/tokens. |
| A03 Software Supply Chain Failures | Versiones y lockfiles, restore bloqueado, analizadores privados, acciones fijadas por SHA, revisión transitiva NuGet y CodeQL en CI. La ausencia de advisories conocidos depende del feed y de la fecha. |
| A04 Cryptographic Failures | JWT firmado, HTTPS obligatorio antes de transmitir credenciales a Auth, HMAC-SHA256, claves fuera del repo, PDF CMS y hashes verificados independientemente; E2E de firma/alteración existentes. |
| A05 Injection | LINQ/SQL interpolado parametrizado y nombres SQL estáticos; ningún identificador/orden SQL procede del cliente. Regex de modelo acotada; prompt externo tratado como dato y sin acceso a operaciones. |
| A06 Insecure Design | Reserva 300 s coordinada en DB, idempotencia durable, cierre atómico y outbox; pruebas de carreras, 299/300/301, fallo inducido, reintentos y aislamiento entre recursos. |
| A07 Authentication Failures | Firma/issuer/audience/lifetime, bloqueo, inactividad y revocación. Restablecer contraseña ahora revoca todas las sesiones locales de la identidad retornada por Auth; token inválido no afecta otras cuentas. E2E verifica múltiples sesiones propias y conservación de cuenta ajena. |
| A08 Software or Data Integrity Failures | HMAC de webhook, deduplicación, triggers de inmutabilidad, manifiesto canónico, firma PDF y copia Mongo idempotente; el cierre nunca depende de MongoDB. |
| A09 Security Logging and Alerting Failures | Auditoría transaccional e inmutable, eventos de acceso/pago/recuperación, trace/delivery ID y logs estructurados por clasificación. Sin tokens/passwords ni texto de excepciones SQL/proveedor; alertas requieren permiso de auditoría. |
| A10 Mishandling of Exceptional Conditions | Elementos nulos en horarios/defectos producen 400 sin cambios; fecha máxima produce 400; errores sanitizados, readiness 503, timeouts, cancellation, rollback y backoff. Pruebas de contingencia IA y dependencias caídas existentes. |

## E2E y límites

Los escenarios de backend recorren HTTP real en Kestrel y PostgreSQL 17 con migraciones reales. Cubren registro/login/RBAC, reservas/pagos/cancelaciones, inspección/cierre, emisión/descarga/QR del PDF firmado, outbox, reportes y contingencia. Las pruebas nuevas cubren regresiones de seguridad, caché y límites de fecha; el consolidado SQL se compara con la regla de referencia incluyendo cruce de medianoche y ejecución repetida.

Resultado local: **58 aprobadas, 0 fallidas y 1 omitida por ausencia de MongoDB local**. CI suministra PostgreSQL y MongoDB aislados y debe ejecutar las 59 sin omisiones antes de pedir aprobación. No se cambia la condición de omisión ni se desactiva un check para obtener verde.

Supabase/Auth, SendGrid, Gemini y Realtime se sustituyen por dobles en estas pruebas: no acreditan entrega SMTP ni credenciales reales de esos proveedores. La conectividad EF con Supabase y sus permisos/TLS se verificó separadamente en la puesta en marcha documentada. Auth y PostgreSQL no comparten transacción distribuida: un fallo tras actualizar la contraseña requiere reintentar/reconciliar la revocación local. SMTP, observabilidad/alertas operativas, firma/secretos de staging y aceptación formal de carga RS-08 siguen siendo pasos de despliegue; no bloquean que el equipo desarrolle con configuración segura y pruebas aisladas.

## Fuentes oficiales consultadas

- [Microsoft: C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14).
- [Microsoft: Minimal APIs .NET 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0).
- [Microsoft: mejores prácticas ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0).
- [Microsoft: analizadores de código](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview).
- [SonarSource: alcance del análisis en Visual Studio](https://docs.sonarsource.com/sonarqube-for-visual-studio/using/scan-my-project).
- [OWASP Top 10:2025](https://top10.owasp.org/2025/).
- [MongoDB: ciclo de vida singleton de MongoClient](https://www.mongodb.com/docs/drivers/csharp/current/connect/mongoclient/).
- [Supabase Auth: implementación oficial de actualización del usuario](https://github.com/supabase/auth/blob/master/internal/api/user.go).
