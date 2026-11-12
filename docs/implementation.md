# Implementación AutoCheck

Versión 1.0 · 2026-11-11 · Equipo AutoCheck / INTEC. Historial de cambios: Git y PR asociados.

## Fuente y decisiones

La autoridad funcional es `AutoCheck_SRS aprobado.pdf`, versión 1.1, en la carpeta documental del proyecto. La arquitectura fija Next.js/Vercel, API .NET 10/Azure App Service, Supabase PostgreSQL/Auth/Realtime, Gemini y SendGrid. Los PDF del equipo no se publican automáticamente en este repositorio.

Decisiones aprobadas en la planificación:

1. PostgreSQL es autoritativo y MongoDB una proyección asíncrona. El cierre nunca depende de Atlas. La copia conserva un ObjectId canónico generado antes del cierre y lo reutiliza al reintentar.
2. Los roles funcionales son Ciudadano, Inspector y Administrador. Los permisos y centro se consultan en PostgreSQL en cada solicitud; los roles enviados por el cliente o en metadata editable se descartan.
3. Reinspección = nueva cita e inspección vinculadas a un rechazo del mismo vehículo. Reagendar = cancelar y reservar de nuevo, con el flujo normal de pago simulado.
4. GitHub aloja el código y Actions ejecuta CI. La diferencia con RD-10/RL-04 del SRS se registra aquí como decisión del equipo; esta nota no modifica por sí sola el SRS aprobado. Azure DevOps puede seguir siendo el gestor del backlog.
5. El PDF final firmado se guarda privadamente como `bytea` en PostgreSQL. Esto permite publicar bytes, hash y estado de emisión en una transacción, sin añadir un proveedor de archivos al MVP. Se sirve exclusivamente desde el endpoint del propietario.

## Responsables y entregas

| Responsable | Entrega completa | CU / PA |
|---|---|---|
| A — Sebastián Tavares | Auth, Usuarios/permisos, Vehículos/catálogos, Certificados, consulta de Auditoría y base CI | CU-01/02/06/07/11/13; PA-01/02/06/07/11/13 |
| B — Manuel Polo | Centros/rampas/horarios, disponibilidad, Citas, retenciones, pagos sandbox, Notificaciones | CU-03/04/12; PA-03/04/12 |
| C — Engels Vicioso | Inspecciones/reglas, Reinspecciones/historial, MongoDB, Reportes y Chatbot | CU-05/08/09/10; PA-05/08/09/10 |

Cada persona entrega endpoint, DTO/validación, autorización por recurso, persistencia, auditoría y pruebas. B revisa a A, C revisa a B y A revisa a C. No se sustituyen las revisiones humanas por aprobaciones del bot.

## Contratos y vertical slices

La convención de nuevas slices y su extracción incremental se define en la [guía compartida VSA/.NET 10/C# 14](architecture/AI_Backend_Architecture_Guidelines.md). Las observaciones de la revisión de Engels se siguen en [PR #1](reviews/pr-1-follow-up.md); no se consideran resueltas solo porque el PR esté fusionado.

Las 49 operaciones de negocio están en `AutoCheck.Api/Features/{Recurso}/{CasoDeUso}/`. Cada `*Endpoint.cs` reúne registro, handler, validación y DTOs exclusivos; `Common/` contiene únicamente contratos/reglas reutilizados dentro del recurso. Los enrutadores del módulo solo componen casos; `Program.cs` compone la aplicación. La infraestructura se divide por responsabilidad en `Http`, `Data`, `Security`, `Time`, `Diagnostics` e `Integrations`. Infraestructura compartida: `AutoCheckDb`, errores, identidad actual, reloj, transacción/idempotencia, outbox y adaptadores sustituibles. No hay repositorio genérico, MediatR ni llamadas HTTP entre features.

Los contratos completos se consultan en OpenAPI y el índice `endpoints.md`. Todas las entradas se validan antes de efectos secundarios. Las colecciones usan `pagina` (desde 1) y `tamano` (1–20). Citas e inspecciones filtran por `estado` y `desde`/`hasta` inclusivos locales de su fecha de inicio; usuarios por `rol`/`activo`; catálogos por `padreId`. Los errores incluyen Problem Details, `codigo` y `traceId`, excepto asistencia que conserva su JSON exacto.

`Idempotency-Key` (1–128 caracteres) es obligatorio al iniciar pago/inspección, cancelar, cerrar y solicitar certificado. La clave se asocia a actor, ruta, método y fingerprint del cuerpo. Una misma clave/cuerpo reproduce estado HTTP, cuerpo y Location originales; un cuerpo distinto devuelve 409. Estos registros no almacenan credenciales ni tokens.

### Identidad

Supabase gestiona credenciales y recuperación; la API valida issuer, audiencia, firma, lifetime y JWKS rotativas. Solo las sesiones iniciadas a través del backend quedan registradas en la aplicación. Un JWT obtenido por otro camino no elude el bloqueo ni habilita acceso a datos.

El restablecimiento de contraseña revoca todas las sesiones locales de la identidad confirmada por Supabase y registra auditoría sin credenciales. La operación entre Auth y PostgreSQL no es una transacción distribuida; si PostgreSQL falla después del cambio de contraseña se debe reintentar y reconciliar. Las respuestas `/api` usan `Cache-Control: no-store`; una política de autorización por defecto protege rutas nuevas que omitan metadatos.

Los 60 minutos de inactividad se comprueban exactamente por `session_id`. GET/polling y refresh no actualizan la actividad. Las acciones interactivas exitosas sí; `POST /api/auth/actividad` permite registrar una acción explícita de navegación. El frontend debe invocarlo desde interacción real, nunca desde un temporizador de fondo.

Cinco fallos de login bloquean la cuenta durante 15 minutos. El contador se serializa en PostgreSQL, no depende de una instancia API y no almacena contraseñas. La recuperación no borra el bloqueo. Los cambios de permisos tienen efecto en la siguiente llamada.

### Reserva y pago

RF-CIT-01–03: bloque de 30 minutos por rampa, retención exclusiva de 300 segundos desde el reloj del servidor. El bloqueo PostgreSQL por centro coordina retención, confirmación, cancelación y cambios operativos entre réplicas. Una vista obsoleta nunca permite reservar sin esta comprobación.

`POST /api/pagos/intentos` admite `escenario` APROBADO/RECHAZADO/ERROR y `demoraSegundos` 0–600, exclusivamente para la simulación académica. El monto procede de configuración del servidor. Se crea la retención y el intento antes de ejecutar `IPaymentSandbox` por outbox.

El webhook valida `X-Sandbox-Signature` hexadecimal HMAC-SHA256 sobre el cuerpo exacto, con `Sandbox__WebhookSecret`. Un redirect del navegador no confirma citas. Los eventos se deduplican por ID y payload. A los 300 segundos o después una aprobación se revierte una vez y no desplaza la reserva de otra solicitud.

Los cambios publican solo una invalidación `{centroId}` en el canal Realtime `disponibilidad:<centroId>`, evento `actualizada`. El cliente vuelve a consultar REST y conoce el vencimiento recibido; nunca confía en el evento como confirmación de una reserva. La limpieza física es secundaria a las comprobaciones de expiración.

### Inspección y certificado

RF-INS-01 / PA-05: `M=3` inicialmente. Rechazado si `G>=1 || Md>=2 || L>M`; en otro caso aprobado. Al iniciar se congelan versión y M y se guardan snapshots del vehículo y centro. El registro de defectos exige descripción, severidad y cantidad entera positiva; una lista vacía significa cero defectos, mientras que la ausencia de registro impide cerrar.

La transacción de cierre conserva defectos, conteos, reglas, resultado, auditoría y outbox. Para aprobadas reserva también el identificador y manifiesto del certificado; renderizar/firmar ocurre después. Fallar la firma no revierte ni cambia la inspección. Triggers PostgreSQL protegen reglas, auditoría, inspecciones cerradas y certificados publicados contra cambios ordinarios.

RF-CER-01: JSON RFC 8785 de esquema restringido a claves/valores ASCII, UUID, fechas UTC y enteros; claves ordenadas ordinalmente, UTF-8 sin BOM. Incluye exactamente los 14 campos del SRS. El SHA-256 es hexadecimal minúsculo de 64 caracteres. El QR compara hash recibido, almacenado y recalculado. No expone el manifiesto ni identificadores internos.

RF-CER-02: PDFsharp incorpora firma CMS SHA-256 después de adjuntar `manifiesto.json` y `manifiesto.sha256`. Se guarda adicionalmente SHA-256 de los bytes finales. Las descargas no regeneran el PDF. La clave privada se monta fuera de la imagen y la clave/certificado público se distribuye previamente al verificador académico. Las pruebas verifican ByteRange, CMS, confianza y alteración sin red usando un verificador independiente del firmador PDFsharp.

### Reportes y asistencia

RF-DAS-01–03: el worker consolida cada 30 segundos mientras la API está activa. Usa cierres y días en America/Santo_Domingo. PostgreSQL calcula agregados y ventanas mediante SQL parametrizado: no se carga todo el historial en memoria de la API ni se eliminan métricas en cada ejecución. La reconciliación completa incluye cierres de commit tardío; su costo en la base todavía debe medirse con volumen representativo. Cada ventana incluye 21 inspecciones del mismo inspector/centro, ordenadas por cierre e ID, incluso cruzando días/rampas; 20/21 aprobaciones alertan. El filtro de rampa selecciona ventanas completas sin cambiar el denominador. Solo hay consulta con permiso de auditoría.

RF-CHAT-01/02 / IS-05: `/api/asistencia` acepta `consulta` de 1–2000 caracteres tras trim y devuelve únicamente `respuesta`, `categoria`, `codigo`. Tiene timeout de dos segundos y no reintenta sincrónicamente. Errores o salida inválida producen 503 con la contingencia exacta antes de tres segundos. Contexto: `Assets/asistencia-aprobada.txt`; no se envían tokens ni datos de otro propietario ni existen herramientas transaccionales en el chatbot.

## Persistencia, migración y operación

El esquema `autocheck` es privado: no se otorgan accesos Data API a anon/authenticated. La API aplica filtros incluso si la conexión de servidor tiene privilegios elevados. Usar un rol SQL de ejecución con mínimos privilegios y un rol separado para migraciones; no conectar el navegador con la conexión privada. Las tablas fuera de esquemas expuestos no requieren habilitar Data API.

La primera migración crea el modelo; la segunda agrega restricciones, historial inmutable y un puente opcional de usuarios/centros del esquema documentado si existen sus tablas. Convierte Propietario → Ciudadano y AdminTaller/AdminINTRANT → Administrador, preservando permisos/alcance; inspectores o administradores locales sin centro válido quedan inactivos. Las tablas originales se conservan. La importación de otros datos operativos de una base existente requiere inventario y migración revisada; no se presupone que el SQL documental esté desplegado.

El 12 de noviembre de 2026 se aplicaron las cuatro migraciones EF a Supabase `AutoCheckBD`, después de validarlas en PostgreSQL aislado. Se conservaron las tablas originales en `public`. `HardenDatabaseBoundary` agrega el índice de expiración y protege las funciones y el historial EF. La conexión HTTP usa `autocheck_api`; migraciones usan `autocheck_migrations`, sin superusuario ni bypass RLS. Ver [puesta en marcha y evidencia](development-setup.md). Los cambios posteriores deben ser aditivos y probarse sobre la base de main. Las migraciones de protección son forward-only para evitar borrar evidencia mediante rollback ordinario.

Outbox usa `FOR UPDATE SKIP LOCKED`, identidades durables y backoff hasta 300 segundos. Tiene consumidores independientes para pagos, firma, Atlas, correo y Realtime; una dependencia lenta no bloquea la emisión PDF. MongoDB usa upsert por ObjectId y un cliente singleton con pool acotado, creado al primer envío. Enviar correo tiene semántica at-least-once: un fallo incierto tras la aceptación del proveedor puede repetir un mensaje; nunca repite el pago o la cita. Se conserva delivery ID para correlación y se omite una confirmación pendiente si la cita ya fue cancelada. Monitorizar mensajes pendientes/reintentos y el checkpoint de consolidación.

Azure App Service debe mantener activo el proceso/worker durante validaciones y asegurar HTTPS. Esto es necesario para el consolidado antes de las 00:05 y para la emisión en menos de diez segundos. Configurar PFX y proveedores antes de una demostración; las pruebas con dobles no validan credenciales reales.

## Evidencia y límites de validación

xUnit cubre reglas y límites; RestAssured.Net verifica HTTP real con Kestrel. PostgreSQL aislado prueba carreras, límites 299/300/301 segundos, reintentos, rollback inducido, permisos e inmutabilidad. MongoDB aislado prueba upsert. El PDF se verifica criptográficamente y se inspecciona visualmente.

Los checks de CI no sustituyen PA-10b/RS-08 con 100 usuarios y red individual controlada, ni las veinte operaciones PDF/IA contra los proveedores configurados. Ejecutar el harness de `tests/load` en el entorno de validación y conservar la evidencia de red y recursos antes de declarar esas PA aprobadas.

La [revisión de calidad y seguridad del 12/11/2026](reviews/backend-quality-2026-11-12.md) registra herramientas, carpetas revisadas, correcciones OWASP y límites de evidencia.
