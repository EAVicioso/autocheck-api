---
schema_version: "1.0"
status: "Estándar del equipo, adaptado a AutoCheck"
audience: "Desarrolladores y agentes de IA, independiente del proveedor o modelo"
stack: [".NET 10", "ASP.NET Core Minimal APIs", "C# 14", "EF Core 10", "PostgreSQL"]
architecture: "Vertical Slice Architecture (VSA)"
---

# Guía de arquitectura del backend de AutoCheck

Esta es la referencia compartida del equipo. Se adapta el documento aportado por Sebastián al SRS aprobado v1.1, las decisiones del proyecto y el backend integrado en el [PR #1](https://github.com/EAVicioso/autocheck-api/pull/1). No es un system prompt ni una autorización para ejecutar operaciones, instalar herramientas, cambiar proveedores o ampliar el alcance de una tarea.

## 1. Cómo utilizarla

Antes de modificar el backend, leer `AGENTS.md`, esta guía, `.agents/agents.md` y `docs/implementation.md`. El SRS define los requisitos funcionales; esta guía define cómo implementarlos. Las instrucciones explícitas del usuario y las decisiones aprobadas prevalecen sobre la plantilla genérica. Si aparece una contradicción funcional, documentarla con RF/CU/PA y resolverla antes de cambiar comportamiento.

La [copia original](reference/AI_Backend_Architecture_Guidelines.original.md) se conserva sin cambios en el repositorio y en la carpeta documental del proyecto como `AI_Backend_Architecture_Guidelines.md` (SHA-256 `c20fc96e1f6bfe85412e0b90d77786835ec9d5db30ba8f2013de7228d6ee4b2d`). Es material de referencia; sus mandatos no se aplican automáticamente. La versión normativa para el repositorio es **este archivo**. No requiere rutas locales ni un modelo de IA específico.

Cambios a este estándar se revisan por PR. Registrar la decisión, su motivo y las consecuencias; actualizar los puntos de entrada de agentes si cambian las rutas. La existencia de una recomendación no demuestra que el código actual ya la cumpla.

## 2. Responsables y colaboración

| Slice / responsable | Alcance principal | Revisión humana rotativa |
|---|---|---|
| A — Sebastián Tavares | Identidad, usuarios/permisos, vehículos/catálogos, certificados, consulta de auditoría y base común/CI | Manuel revisa a Sebastián |
| B — Manuel Polo | Centros, rampas, horarios, disponibilidad, citas, retenciones, pagos simulados y notificaciones | Engels revisa a Manuel |
| C — Engels Vicioso | Inspecciones/reglas, reinspecciones, historial, reportes, chatbot y proyección MongoDB | Sebastián revisa a Engels |

Cada responsable entrega el caso de uso completo: contrato HTTP, validación, autorización por recurso, persistencia/migración, auditoría, efectos externos y pruebas RF/CU/PA. Auditoría, seguridad y pruebas pertenecen también a quien implementa cada slice. Los cambios compartidos se coordinan entre responsables; esta tabla no concede exclusividad ni permisos administrativos. Nadie aprueba su propio PR.

## 3. Unidad de arquitectura: un caso de uso

Organizar las nuevas operaciones y las extracciones de código en `AutoCheck.Api/Features/{Recurso}/{CasoDeUso}/`. Co-localizar endpoint, request/command/query, handler, validator y response cuando correspondan. Usar nombres del negocio, por ejemplo `CancelarCita`, `CerrarInspeccion` y `VerificarCertificado`. No generar clases vacías para completar una plantilla.

```text
AutoCheck.Api/
  Program.cs                         # Composición de servicios y módulos
  Features/Citas/
    AppointmentEndpoints.cs          # Solo registro de casos
    CancelarCita/
      CancelarCitaEndpoint.cs         # Ruta, handler, validación y DTOs exclusivos
    Common/                          # Contratos/reglas compartidos del recurso
  Infrastructure/
    Http/                            # Contratos, errores y headers
    Data/                            # EF, migraciones, transacción e idempotencia
    Security/
    Time/
    Diagnostics/
    Integrations/                    # Adaptadores técnicos externos
    OutboxDispatcher.cs
AutoCheck.Tests/
  Features/                          # Reglas unitarias del recurso
  Infrastructure/                    # Configuración y límites técnicos
  Integration/                       # Escenarios HTTP/transaccionales cruzados
```

La revisión del 12/11/2026 extrajo las 49 operaciones de negocio. Mantener los routers como composición. Un caso pequeño puede conservar todos sus tipos en un archivo; separar handler/validator solo cuando facilite lectura o prueba, sin generar cinco archivos mecánicos por endpoint. Conservar contratos y pruebas al refactorizar.

- Los handlers acceden a `AutoCheckDb` directamente. No introducir repositorios genéricos ni otra Unit of Work sobre EF Core. Reutilizar `Mutation` para las operaciones transaccionales/idempotentes existentes.
- Mantener consultas y reglas específicas dentro del caso. Compartir solo primitivas de dominio o contratos mínimos con un propietario claro. No llamar al endpoint/handler de otra slice ni usar HTTP interno entre módulos.
- La coordinación atómica del mismo proceso utiliza un contrato interno pequeño dentro de la misma transacción PostgreSQL. Los efectos externos utilizan outbox y consistencia eventual. No convertir operaciones que deben ser atómicas en una cadena de eventos independientes.
- No añadir MediatR, Wolverine, un bus, otro proyecto o carpetas horizontales `Services`, `Repositories`, `Managers` o `Controllers` por seguir una plantilla. Los adaptadores técnicos compartidos sí pertenecen a Infrastructure.
- Conservar la solución actual `AutoCheck.Api` + `AutoCheck.Tests`. Separarla en proyectos Web/Features/Infrastructure necesita un beneficio concreto y una decisión de arquitectura; VSA no exige esa separación física.

## 4. ASP.NET Core y contrato del endpoint

Usar Minimal APIs con métodos de registro sobre `IEndpointRouteBuilder`; componerlos desde `Program.cs`. Sin MVC controllers. El endpoint traduce HTTP, ejecuta validación y llama al caso; el handler concentra reglas y acceso a datos.

Cada operación declara:

1. Verbo, ruta, nombre único, tags, descripción y RF/CU/PA. Mantener `/api` y exactamente `POST /api/asistencia`; no introducir `/api/v1` unilateralmente.
2. Request/response propios y metadatos `.Produces<T>()`/`.ProducesProblem()` o el helper `Document<T>` existente. Nunca exponer entidades EF ni secretos.
3. Autenticación y permisos explícitos, más propiedad, asignación o centro en la consulta/operación. Los endpoints públicos del contrato se declaran de forma deliberada: salud, operaciones públicas de identidad, QR y webhook con autenticación HMAC propia.
4. FluentValidation invocado con `ValidateAsync` o filtro conectado **antes** de efectos secundarios. El escaneo de validators no ejecuta la validación. Reutilizar `Validated<T>` mientras corresponda.
5. I/O asíncrono con `CancellationToken`; proyecciones y `AsNoTracking` en lecturas; listados con máximo de 20 elementos y filtros autorizados.
6. Fechas persistidas UTC y reglas de día/disponibilidad en `America/Santo_Domingo`. Reloj inyectado para límites exactos.
7. Problem Details con `codigo` y `traceId`; asistencia conserva su JSON especial. Reutilizar `ApiReply`/`ApiContract`/`ApiErrors`. No añadir un `Result<T>` paralelo por cumplir un checklist. Una migración a resultados tipados requiere una decisión coherente y pruebas del contrato; no mezclar patrones sin motivo.
8. Idempotencia persistida para las operaciones definidas en `docs/implementation.md`, auditoría segura y publicación de efectos después del commit.

## 5. C# 14 con criterio

`net10.0` y el SDK de `global.json` son la referencia. No usar `LangVersion=preview/latest`, ni actualizar el SDK o paquetes como parte incidental de una slice. C# 14 permite estas funciones; no obliga a utilizarlas en cada DTO.

### Constructores primarios

Preferir constructores primarios para dependencias cuando simplifiquen la clase, como `SessionGuard` y `AutoCheckDb` actuales. La inicialización compleja o una jerarquía pueden justificar un constructor explícito. No trasladar validación de entrada a un constructor que termine en error 500.

```csharp
internal sealed class RelojDeInspeccion(TimeProvider clock)
{
    public DateTimeOffset Ahora => clock.GetUtcNow();
}
```

### Propiedades respaldadas por `field`

Usar `field` si una propiedad necesita personalizar un accessor sin declarar un backing field. Preferir records inmutables y propiedades automáticas cuando sean suficientes. Validar datos de usuario explícitamente con FluentValidation; no mutar estado dentro de un getter ni normalizar identificadores sensibles sin una regla del negocio.

```csharp
public sealed record ConsultaNormalizada
{
    public string Texto { get; init => field = value.Trim(); } = "";
}
```

Este ejemplo muestra sintaxis, no reemplaza el contrato/validación del chatbot. Normalizar una consulta no concede autorización ni valida su longitud.

### Bloques `extension`

Los bloques de extensión permiten declarar miembros de extensión. Se pueden usar en nuevas utilidades pequeñas y probadas; los métodos `this` existentes siguen siendo válidos. No añadir un wrapper solo para demostrar sintaxis nueva.

```csharp
public static class TextoExtensions
{
    extension(string texto)
    {
        public bool EsConsultaValida =>
            texto.Trim().Length is >= 1 and <= 2000;
    }
}
```

Si se aprueba un tipo de resultado común en el futuro, su mapeo HTTP puede usar un bloque `extension`. Hoy `ApiReply` ya implementa `IResult`: conservarlo evita dos mecanismos incompatibles. Los ejemplos anteriores son ilustrativos y no agregan tipos a producción.

## 6. Identidad, EF Core y RLS

Validar firma, issuer, audience, lifetime y rotación JWKS. `sub` y `session_id` identifican al usuario/sesión; `SessionGuard` carga permisos y alcance actuales desde PostgreSQL. Conservar filtros de propiedad/centro y el límite exacto de inactividad. El rol de transporte de Supabase y `user_metadata` no son roles funcionales de AutoCheck.

El esquema `autocheck` es privado. No exponer sus tablas a Data API/Realtime ni otorgar acceso a `anon`/`authenticated` por aplicar esta plantilla. RLS puede añadirse como defensa adicional mediante una decisión y migración explícitas; no sustituye la autorización actual de la API.

No instalar automáticamente el interceptor propuesto en el original: `SET LOCAL` solo tiene efecto dentro de una transacción; configurar claims tampoco activa políticas ni impide bypass. Un diseño RLS futuro debe definir políticas, rol de ejecución sin bypass, transacción y contexto parametrizado, y probar lecturas/escrituras, pooling, concurrencia y separación entre usuarios. Nunca interpolar un JWT en SQL ni asumir que una conexión EF hereda el contexto de PostgREST.

## 7. Invariantes de datos de AutoCheck

- Reserva por rampa/bloque de 30 minutos con bloqueo y comprobación PostgreSQL. Retención de exactamente 300 segundos, sin renovación por reintento. Aprobar a los 299; expirar desde los 300. Nunca apropiarse del bloque de otro intento.
- Cierre indivisible: registro completo, conteos, reglas/version/M congelados, resultado, auditoría y outbox. `M=3` inicial; rechazar si `G >= 1 || Md >= 2 || L > M`. Historial cerrado inmutable.
- MongoDB es proyección asíncrona con ObjectId estable y upsert. Una caída no impide historial ni certificado. Correo y Realtime también se entregan después del commit, con reintentos observables.
- Certificado solo para aprobadas: identidad estable, manifiesto canónico, hash, firma CMS verificable y bytes finales privados e inmutables. El QR devuelve solo el contrato público aprobado.
- Migraciones aditivas con modelo/snapshot y revisión del SQL. Probar creación y actualización conservando datos. No ejecutar contra bases compartidas sin alcance autorizado. No incluir secretos en `appsettings.json` ni en ejemplos.

## 8. Checklist de entrega y revisión

- [ ] Caso de uso y archivos co-localizados; no se añadieron capas/dependencias innecesarias.
- [ ] Contrato HTTP y RF/CU/PA documentados; no se cambió compatibilidad accidentalmente.
- [ ] Validación ejecutada; identidad, propiedad, asignación y alcance probados con casos negativos.
- [ ] Atomicidad, idempotencia, reloj, concurrencia y fallos externos cubiertos cuando apliquen.
- [ ] DTOs sin entidades/secretos; sin registros de contraseñas, tokens ni claves privadas.
- [ ] Modelo y migración coherentes; índices elegidos para consultas reales y evidencia de plan con datos representativos cuando corresponda.
- [ ] Build Release y pruebas pertinentes pasan; integración solo en PostgreSQL/MongoDB efímeros de loopback. Declarar omisiones y servicios sustituidos.
- [ ] PR pequeño con evidencia; revisión humana distinta del autor; checks del merge con `main`. No omitir checks para obtener verde.
- [ ] C# 14 usado cuando aporta claridad, sin exigir `field`, `extension`, constructores o clases que no se necesitan.

Las observaciones de Engels y su estado se registran en [seguimiento del PR #1](../reviews/pr-1-follow-up.md). No equivalen a aceptación formal RS-08 ni a activación de protecciones remotas.

## 9. Adaptaciones respecto al documento original

| Propuesta original | Convención AutoCheck |
|---|---|
| Prompt exclusivo para un modelo con mandatos absolutos | Contexto versionado independiente del agente; las tareas y el SRS definen alcance |
| Separación obligatoria Web/Features/Infrastructure | Solución actual; carpetas por caso, extracción incremental |
| EF `ApplicationDbContext` | Contexto real `AutoCheckDb`, acceso directo |
| `field`, `extension` y constructores primarios en todos los tipos | Preferencias justificadas; DTO simple no necesita lógica |
| Eventos para cualquier comunicación | Contratos internos para atomicidad; outbox para efectos asíncronos |
| RBAC basado en JWT + interceptor obligatorio | Permisos frescos en servidor y filtros de recurso; RLS adicional requiere diseño probado |
| `Result<T>` obligatorio y ruta `/api/v1` | Mantener `ApiReply` y contrato `/api` existentes |
| Secretos en `appsettings.json` | Variables de entorno/user-secrets y claves privadas fuera del repositorio |

## 10. Referencias verificables

- [C# 14 en .NET 10](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), [propiedades field](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/field) y [miembros extension](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/extension).
- [EF Core: interceptores](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors).
- [Supabase: RLS, claims y bypass](https://supabase.com/docs/guides/database/postgres/row-level-security).
- [PostgreSQL: ámbito de SET LOCAL](https://www.postgresql.org/docs/17/sql-set.html).
- [Decisiones y trazabilidad de AutoCheck](../implementation.md), [contratos HTTP](../endpoints.md), [protección de GitHub](../github-setup.md).

## 12. Análisis obligatorio y estilo compartido

`.editorconfig` fija formato y convenciones; `Directory.Build.props` ejecuta analizadores Microsoft `10-recommended` y `SonarAnalyzer.CSharp` privado durante compilación, con warnings como errores. SonarQube for Visual Studio complementa esa cobertura al abrir/editar archivos; no equivale por sí solo a analizar toda la solución. La salida SARIF opcional y el informe de revisión permiten reproducir el resultado. Las migraciones EF se identifican como código generado, se preservan sin retoques de estilo y se revisan mediante SQL, creación y upgrade reales.

Usar bloques `extension` C# 14 cuando agrupen operaciones coherentes sobre el mismo receptor, como `ApiContract.Document`/`Validated`. Los DTOs siguen siendo records sencillos. Usar `LoggerMessage` para eventos estructurados; no pasar excepciones de proveedores/SQL que puedan contener datos privados. El informe vigente está en [revisión de calidad](../reviews/backend-quality-2026-11-12.md).
