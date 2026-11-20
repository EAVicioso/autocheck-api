# DEC-LIFECYCLE-01: contrato y secuencia de las 30 slices

Decisión aprobada: Supabase Auth gestiona credenciales; `autocheck` permanece privado, sin grants Data API ni nuevas políticas RLS. La API valida identidad criptográfica y consulta rol/permisos/centro en PostgreSQL en cada solicitud. No existe tenant adicional al alcance por propietario, asignación y centro. Usuarios y vehículos se archivan lógicamente; auditoría, cierres y PDF publicado son inmutables. Roles y permisos son catálogos fijos: no se añaden mutaciones administrativas de esos catálogos ni PUT/DELETE de certificados o auditorías.

## Secuencia obligatoria por slice

BDD: leer el escenario identificado y sus fronteras comunes. CDD: fijar los records y la firma tipada antes de modificar reglas. Red: escribir el caso xUnit derivado y ejecutarlo contra Handler/validator con NSubstitute, más contrato HTTP cuando corresponda. Green: implementar lo mínimo manteniendo la transacción existente. Refactor: proyección SQL, AsNoTracking, cancelación y paginación; build sin warnings. Un fallo se corrige desde el diagnóstico/stacktrace real. Los archivos `.feature` son especificaciones; xUnit implementa su verificación explícita, sin un runner Gherkin adicional. La tabla enlaza evidencia por comportamiento y no constituye una declaración de cobertura exhaustiva por fila.

Contexto común C0 (cargar en una sesión limpia): `AGENTS.md`, `.agents/agents.md`, `docs/architecture/AI_Backend_Architecture_Guidelines.md`, `docs/implementation.md`, este documento, `AutoCheck.Api/Infrastructure/Data/AutoCheckDb.cs`, `Entities.cs`, `Infrastructure/Http/ApiContract.cs`, `ApiException.cs`, `ApiErrorCode.cs`, `Infrastructure/Security/AppActor.cs`. Añadir únicamente la carpeta de slice indicada y los archivos mínimos adicionales de cada tarea. No cargar carpetas de otros módulos salvo la dependencia explícita.

Fronteras comunes: 400 para JSON/validación y filtros; 401 para identidad/sesión inválida; 403 para permiso/propiedad/centro denegado; 404 para recurso inexistente; 409 para versión/unicidad/estado/idempotencia en conflicto. No se introduce 422 porque el contrato existente usa 400. No se inventan 404/409 para catálogos o listados sin recurso mutable. El validador se ejecuta antes de efectos. A01: filtros SQL + política; A02/A07: no persistir ni registrar secretos, JWT estricto; A03: EF/SQL parametrizado; A04/A05: límite 128 KiB, allowlist, rate limit/CORS/errores seguros; A08: hashes y firma de PDF; A09: mutaciones y fallos críticos auditados. La suite cubre además dependencias y configuración segura; no se afirma que una etiqueta OWASP equivalga a un pentest completo.

## Matriz HTTP

| ID | Método/ruta | Autorización | Contrato | HTTP | Auditoría |
|---|---|---|---|---|---|
| A01 | `POST /api/auth/registro` | Público | `RegisterRequest → RegistrationResponse` | 201; 400,409,429,503 | REGISTRO |
| A02 | `POST /api/auth/login` | Público | `LoginRequest → SessionResponse` | 200; 400,401,403,429,503 | LOGIN / INICIO_BLOQUEO |
| A03 | `POST /api/auth/refresh` | Público; sesión local válida | `RefreshRequest → SessionResponse` | 200; 400,401,429,503 | SESION_RENOVADA |
| A04 | `POST /api/auth/recuperacion` | Público | `RecoveryRequest → RecoveryResponse` | 202; 400,429,503 | RECUPERACION_SOLICITADA |
| A05 | `POST /api/auth/restablecimiento` | Bearer de recuperación validado por Auth | `ResetRequest → NoContent` | 204; 400,401,409,429,503 | RESTABLECIMIENTO_CONTRASENA / RESTABLECIMIENTO_RECHAZADO |
| A06 | `POST /api/auth/logout` | Sesión propia; reintento revocado permitido | `Command(AccessToken) → NoContent` | 204; 401,429,503 | LOGOUT |
| A07 | `POST /api/auth/actividad` | SesionActiva | `Sin cuerpo → NoContent` | 204; 401,403,429,503 | ACTIVIDAD_REGISTRADA |
| AU01 | `GET /api/auditorias` | AuditoriaGlobal | `Query → PageResponse<AuditResponse>` | 200; 400,401,403,503 | — |
| AU02 | `GET /api/auditorias/{id}` | AuditoriaGlobal | `Query(Id) → AuditResponse` | 200; 401,403,404,503 | — |
| U01 | `GET /api/usuarios/me` | SesionActiva; propio | `Sin cuerpo → UserResponse` | 200; 401,403,404,503 | — |
| U02 | `PATCH /api/usuarios/me` | SesionActiva; propio | `ProfileRequest → UserResponse` | 200; 400,401,403,404,409,503 | PERFIL_ACTUALIZADO |
| U03 | `GET /api/usuarios` | GestionUsuarios + alcance de centro | `Query → OffsetPageResponse<UserResponse>` | 200; 400,401,403,503 | — |
| U04 | `GET /api/usuarios/{id}` | GestionUsuarios + alcance de centro | `Query(Id) → UserResponse` | 200; 401,403,404,503 | — |
| U05 | `DELETE /api/usuarios/{id}?version=N` | GestionUsuarios + alcance; nunca propio | `Command(Id,Version) → NoContent` | 204; 400,401,403,404,409,503 | USUARIO_ARCHIVADO |
| P01 | `GET /api/roles` | GestionUsuarios | `Sin cuerpo → ItemsResponse<Response>` | 200; 401,403 | — |
| P02 | `GET /api/permisos` | GestionUsuarios | `Sin cuerpo → ItemsResponse<Response>` | 200; 401,403 | — |
| P03 | `GET /api/usuarios/{id}/acceso` | GestionUsuarios + alcance | `Query(Id) → AccessResponse` | 200; 401,403,404,503 | — |
| P04 | `PATCH /api/usuarios/{id}/acceso` | GestionUsuarios + alcance; sin autoelevación | `AccessRequest → UserResponse` | 200; 400,401,403,404,409,503 | CAMBIO_ACCESO |
| V01 | `GET /api/catalogos/{tipo}` | SesionActiva | `Query → PageResponse<CatalogResponse>` | 200; 400,401,404,503 | — |
| V02 | `POST /api/vehiculos` | Ciudadano; propietario desde AppActor | `VehicleRequest sin Version → VehicleResponse` | 201; 400,401,403,409,503 | VEHICULO_REGISTRADO |
| V03 | `GET /api/vehiculos` | SesionActiva; propietario en SQL | `Query → PageResponse<VehicleResponse>` | 200; 400,401,403,503 | — |
| V04 | `GET /api/vehiculos/{id}` | Propiedad/asignación/centro | `Query(Id) → VehicleResponse` | 200; 401,403,404,503 | — |
| V05 | `PATCH /api/vehiculos/{id}` | Ciudadano; propietario | `VehicleRequest con Version → VehicleResponse` | 200; 400,401,403,404,409,503 | VEHICULO_ACTUALIZADO |
| V06 | `DELETE /api/vehiculos/{id}?version=N` | Ciudadano; propietario | `Command(Id,Version) → NoContent` | 204; 400,401,403,404,409,503 | VEHICULO_ARCHIVADO |
| V07 | `GET /api/vehiculos/{id}/historial` | Propiedad/asignación/centro | `Query → PageResponse<HistoryResponse>` | 200; 400,401,403,404,503 | — |
| C01 | `POST /api/inspecciones/{id}/certificado` | Propietario; cierre Aprobado; Idempotency-Key | `Command(Id) → CertificateResponse` | 202/200; 400,401,403,404,409,503 | CERTIFICADO_SOLICITADO |
| C02 | `GET /api/certificados` | Propietario | `Query → PageResponse<CertificateResponse>` | 200; 400,401,403,503 | — |
| C03 | `GET /api/certificados/{id}` | Propietario | `Query(Id) → CertificateResponse` | 200; 401,403,404,503 | — |
| C04 | `GET /api/certificados/{id}/pdf` | Propietario; EMITIDO | `Query(Id) → FileContentHttpResult` | 200; 401,403,404,409,503 | — |
| C05 | `GET /api/certificados/{id}/verificacion?hash=sha256` | Público; limitador public | `Query(Id,Hash) → VerificationResponse` | 200; 400,404,409,429,503 | SEGURIDAD_RECHAZADA si integridad inválida |

## Olas y contexto de ejecución

Ola 1: base (`Pagination`, `ApiExceptionHandler`, `AccessPolicies`, `TransportClaims`, `SecurityAudit`, `Mutation.RunTyped`, `LifecycleServices`, `TimeProvider`), migración aditiva revisada, AU01–AU02 y A01–A07. Ola 2: U01–U05 y P01–P04. Ola 3: V01–V07 y C01–C05. Ola 4: suite local completa y harness real contra Supabase. La infraestructura es soporte; cada endpoint conserva su slice independiente.

En cada tarea: los archivos a crear/modificar son los `*Contracts.cs`, `*Handler.cs`, `*Data.cs`, `*Validator.cs` aplicables y `*Endpoint.cs` ya presentes en su carpeta; no crear adaptadores o validators vacíos para lecturas triviales. Registrar solo el caso en su router y en `Infrastructure/LifecycleServices.cs`. Añadir pruebas en `AutoCheck.Tests/Features/` y/o `Integration/` sin cargar módulos ajenos.

### A01 · Auth/RegistrarCiudadano · CU-01/PA-01

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auth/RegistrarCiudadano/` + `Infrastructure/Security/IdentityProvider.cs`, `AppActor.cs` (SessionGuard), `TransportClaims.cs`, `Features/Auth/Common/*`. Archivos del caso: `RegistrarCiudadanoContracts.cs`, `RegistrarCiudadanoData.cs`, `RegistrarCiudadanoEndpoint.cs`, `RegistrarCiudadanoHandler.cs`, `RegistrarCiudadanoValidator.cs`.

**BDD:** escenario `A01` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `201; 400,409,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `RegisterRequest → RegistrationResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Created<RegistrationResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(RegisterRequest r, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → BusinessFlowTests.PA01RegistrationValidatesAndPreventsDuplicateAccounts; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `REGISTRO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### A02 · Auth/IniciarSesion · CU-02/PA-02

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auth/IniciarSesion/` + `Infrastructure/Security/IdentityProvider.cs`, `AppActor.cs` (SessionGuard), `TransportClaims.cs`, `Features/Auth/Common/*`. Archivos del caso: `IniciarSesionContracts.cs`, `IniciarSesionData.cs`, `IniciarSesionEndpoint.cs`, `IniciarSesionHandler.cs`, `IniciarSesionValidator.cs`.

**BDD:** escenario `A02` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `LoginRequest → SessionResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<SessionResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(LoginRequest r, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → IdentityHandlerTests; BusinessFlowTests.PA02cFiveFailuresBlockWithoutExtendingAndUnlockAt15Minutes; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `AccountLock, AppSession, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `LOGIN / INICIO_BLOQUEO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### A03 · Auth/RenovarSesion · CU-02/RS-10

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auth/RenovarSesion/` + `Infrastructure/Security/IdentityProvider.cs`, `AppActor.cs` (SessionGuard), `TransportClaims.cs`, `Features/Auth/Common/*`. Archivos del caso: `RenovarSesionContracts.cs`, `RenovarSesionData.cs`, `RenovarSesionEndpoint.cs`, `RenovarSesionHandler.cs`, `RenovarSesionValidator.cs`.

**BDD:** escenario `A03` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `RefreshRequest → SessionResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<SessionResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(RefreshRequest r, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → IdentityHandlerTests.RefreshHonorsExactInactivityBoundary; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `AppSession, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `SESION_RENOVADA`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### A04 · Auth/SolicitarRecuperacion · IS-04/RS-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auth/SolicitarRecuperacion/` + `Infrastructure/Security/IdentityProvider.cs`, `AppActor.cs` (SessionGuard), `TransportClaims.cs`, `Features/Auth/Common/*`. Archivos del caso: `SolicitarRecuperacionContracts.cs`, `SolicitarRecuperacionData.cs`, `SolicitarRecuperacionEndpoint.cs`, `SolicitarRecuperacionHandler.cs`, `SolicitarRecuperacionValidator.cs`.

**BDD:** escenario `A04` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `202; 400,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `RecoveryRequest → RecoveryResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Accepted<RecoveryResponse>, Microsoft.AspNetCore.Http.HttpResults.ValidationProblem, Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>> HandleAsync(RecoveryRequest request, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleBoundaryTests.MalformedPublicIdentityInputsFailBeforeProviderSideEffects; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `RECUPERACION_SOLICITADA`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### A05 · Auth/RestablecerContrasena · RS-09/RS-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auth/RestablecerContrasena/` + `Infrastructure/Security/IdentityProvider.cs`, `AppActor.cs` (SessionGuard), `TransportClaims.cs`, `Features/Auth/Common/*`, `Infrastructure/Security/PasswordResetRevocation.cs`. Archivos del caso: `RestablecerContrasenaContracts.cs`, `RestablecerContrasenaEndpoint.cs`, `RestablecerContrasenaHandler.cs`, `RestablecerContrasenaValidator.cs`.

**BDD:** escenario `A05` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `204; 400,401,409,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `ResetRequest → NoContent`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(ResetRequest request, RestablecerContrasenaHandler handler, HttpContext http, CancellationToken ct);
```

**TDD:** Red → PasswordResetFlowTests; IdentityHandlerTests.ResetCannotChangeExternalPasswordWhenDurablePrepareFails; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `PasswordResetOperation, OutboxMessage, AppSession, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `RESTABLECIMIENTO_CONTRASENA / RESTABLECIMIENTO_RECHAZADO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### A06 · Auth/CerrarSesion · CU-02/RS-10

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auth/CerrarSesion/` + `Infrastructure/Security/IdentityProvider.cs`, `AppActor.cs` (SessionGuard), `TransportClaims.cs`, `Features/Auth/Common/*`. Archivos del caso: `CerrarSesionContracts.cs`, `CerrarSesionData.cs`, `CerrarSesionEndpoint.cs`, `CerrarSesionHandler.cs`.

**BDD:** escenario `A06` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `204; 401,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Command(AccessToken) → NoContent`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<NoContent> HandleAsync(HttpContext http, Handler handler, CancellationToken ct);
```

**TDD:** Red → QuerySecurityFlowTests.LogoutCanRetryItsOwnRevokedSession; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `AppSession, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `LOGOUT`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### A07 · Auth/RegistrarActividad · RS-10/PA-02

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auth/RegistrarActividad/` + `Infrastructure/Security/IdentityProvider.cs`, `AppActor.cs` (SessionGuard), `TransportClaims.cs`, `Features/Auth/Common/*`. Archivos del caso: `RegistrarActividadData.cs`, `RegistrarActividadEndpoint.cs`, `RegistrarActividadHandler.cs`.

**BDD:** escenario `A07` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `204; 401,403,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Sin cuerpo → NoContent`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Handler handler, CancellationToken ct);
```

**TDD:** Red → BusinessFlowTests.PA02bExactInactivityBoundaryIsEnforcedOnREST; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `AppSession, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `ACTIVIDAD_REGISTRADA`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### AU01 · Auditorias/ConsultarAuditorias · CU-13/PA-13

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auditorias/ConsultarAuditorias/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ConsultarAuditoriasContracts.cs`, `ConsultarAuditoriasData.cs`, `ConsultarAuditoriasEndpoint.cs`, `ConsultarAuditoriasHandler.cs`.

**BDD:** escenario `AU01` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query → PageResponse<AuditResponse>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<PageResponse<AuditResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → QuerySecurityFlowTests.AuditCursorDoesNotRepeatEventsAndRejectsWrongOrder; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### AU02 · Auditorias/ObtenerAuditoria · CU-13/PA-13

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Auditorias/ObtenerAuditoria/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ObtenerAuditoriaContracts.cs`, `ObtenerAuditoriaData.cs`, `ObtenerAuditoriaEndpoint.cs`, `ObtenerAuditoriaHandler.cs`.

**BDD:** escenario `AU02` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query(Id) → AuditResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<AuditResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleFlowTests.CatalogsAndDetailsRequireDatabasePermissions; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### U01 · Usuarios/ObtenerPerfil · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Usuarios/ObtenerPerfil/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ObtenerPerfilData.cs`, `ObtenerPerfilEndpoint.cs`, `ObtenerPerfilHandler.cs`.

**BDD:** escenario `U01` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Sin cuerpo → UserResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<UserResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleQueryHandlerTests; LifecycleBoundaryTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### U02 · Usuarios/ActualizarPerfil · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Usuarios/ActualizarPerfil/` + `Infrastructure/Data/Mutation.cs`, `Lifecycle.cs`, `AuditSnapshot.cs`. Archivos del caso: `ActualizarPerfilContracts.cs`, `ActualizarPerfilData.cs`, `ActualizarPerfilEndpoint.cs`, `ActualizarPerfilHandler.cs`, `ActualizarPerfilValidator.cs`.

**BDD:** escenario `U02` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,404,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `ProfileRequest → UserResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<UserResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(ProfileRequest r, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleFlowTests.ProfileRequiresCurrentVersionAndAuditsOnce; LifecycleMutationHandlerTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `PERFIL_ACTUALIZADO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### U03 · Usuarios/ListarUsuarios · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Usuarios/ListarUsuarios/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ListarUsuariosContracts.cs`, `ListarUsuariosData.cs`, `ListarUsuariosEndpoint.cs`, `ListarUsuariosHandler.cs`.

**BDD:** escenario `U03` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query → OffsetPageResponse<UserResponse>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<OffsetPageResponse<UserResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleQueryHandlerTests; LifecycleBoundaryTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### U04 · Usuarios/ObtenerUsuario · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Usuarios/ObtenerUsuario/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ObtenerUsuarioContracts.cs`, `ObtenerUsuarioData.cs`, `ObtenerUsuarioEndpoint.cs`, `ObtenerUsuarioHandler.cs`.

**BDD:** escenario `U04` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query(Id) → UserResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<UserResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleFlowTests.CatalogsAndDetailsRequireDatabasePermissions; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### U05 · Usuarios/ArchivarUsuario · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Usuarios/ArchivarUsuario/` + `Infrastructure/Data/Mutation.cs`, `Lifecycle.cs`, `AuditSnapshot.cs`. Archivos del caso: `ArchivarUsuarioContracts.cs`, `ArchivarUsuarioData.cs`, `ArchivarUsuarioEndpoint.cs`, `ArchivarUsuarioHandler.cs`.

**BDD:** escenario `U05` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `204; 400,401,403,404,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Command(Id,Version) → NoContent`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, long version, HttpContext http, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleFlowTests.ArchiveUserRevokesSessionsAndPreservesAuthIdentity; LifecycleMutationHandlerTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount, AppSession, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `USUARIO_ARCHIVADO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### P01 · Permisos/ListarRoles · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Permisos/ListarRoles/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ListarRolesContracts.cs`, `ListarRolesEndpoint.cs`, `ListarRolesHandler.cs`.

**BDD:** escenario `P01` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Sin cuerpo → ItemsResponse<Response>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static Results<Ok<ItemsResponse<Response>>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult> HandleAsync(Handler handler);
```

**TDD:** Red → LifecycleFlowTests.CatalogsAndDetailsRequireDatabasePermissions; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Catálogo fijo en código; sin tabla de roles` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### P02 · Permisos/ListarPermisos · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Permisos/ListarPermisos/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ListarPermisosContracts.cs`, `ListarPermisosEndpoint.cs`, `ListarPermisosHandler.cs`.

**BDD:** escenario `P02` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Sin cuerpo → ItemsResponse<Response>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static Results<Ok<ItemsResponse<Response>>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult> HandleAsync(Handler handler);
```

**TDD:** Red → LifecycleFlowTests.CatalogsAndDetailsRequireDatabasePermissions; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Catálogo fijo en código; sin tabla de permisos` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### P03 · Permisos/ObtenerAcceso · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Permisos/ObtenerAcceso/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ObtenerAccesoContracts.cs`, `ObtenerAccesoData.cs`, `ObtenerAccesoEndpoint.cs`, `ObtenerAccesoHandler.cs`.

**BDD:** escenario `P03` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query(Id) → AccessResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<AccessResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleFlowTests.CatalogsAndDetailsRequireDatabasePermissions; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### P04 · Permisos/CambiarAcceso · CU-11/PA-11

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Permisos/CambiarAcceso/` + `Infrastructure/Data/Mutation.cs`, `Lifecycle.cs`, `AuditSnapshot.cs`. Archivos del caso: `CambiarAccesoContracts.cs`, `CambiarAccesoData.cs`, `CambiarAccesoEndpoint.cs`, `CambiarAccesoHandler.cs`, `CambiarAccesoValidator.cs`.

**BDD:** escenario `P04` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,404,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `AccessRequest → UserResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<UserResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, AccessRequest r, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → QuerySecurityFlowTests.PermissionsChangeOnNextRequestWithoutRefreshingJwt; LifecycleMutationHandlerTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `UserAccount, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `CAMBIO_ACCESO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### V01 · Vehiculos/ConsultarCatalogo · IU-01

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Vehiculos/ConsultarCatalogo/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ConsultarCatalogoContracts.cs`, `ConsultarCatalogoData.cs`, `ConsultarCatalogoEndpoint.cs`, `ConsultarCatalogoHandler.cs`.

**BDD:** escenario `V01` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query → PageResponse<CatalogResponse>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<PageResponse<CatalogResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleQueryHandlerTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `CatalogItem` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### V02 · Vehiculos/RegistrarVehiculo · IU-01/PA-10

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Vehiculos/RegistrarVehiculo/` + `Infrastructure/Data/Mutation.cs`, `Lifecycle.cs`, `AuditSnapshot.cs`. Archivos del caso: `RegistrarVehiculoContracts.cs`, `RegistrarVehiculoData.cs`, `RegistrarVehiculoEndpoint.cs`, `RegistrarVehiculoHandler.cs`.

**BDD:** escenario `V02` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `201; 400,401,403,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `VehicleRequest sin Version → VehicleResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Created<VehicleResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(VehicleRequest r, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleValidatorTests; SupabaseFullLifecycleTests.CheckVehicleCrud; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Vehicle, UserAccount, CatalogItem, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `VEHICULO_REGISTRADO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### V03 · Vehiculos/ListarVehiculos · CU-10/PA-10

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Vehiculos/ListarVehiculos/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ListarVehiculosContracts.cs`, `ListarVehiculosData.cs`, `ListarVehiculosEndpoint.cs`, `ListarVehiculosHandler.cs`.

**BDD:** escenario `V03` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query → PageResponse<VehicleResponse>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<PageResponse<VehicleResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → QuerySecurityFlowTests.ArchivedVehiclesAreOptInAndSortIsValidated; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Vehicle, Appointment` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### V04 · Vehiculos/ObtenerVehiculo · CU-10/PA-10

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Vehiculos/ObtenerVehiculo/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ObtenerVehiculoContracts.cs`, `ObtenerVehiculoData.cs`, `ObtenerVehiculoEndpoint.cs`, `ObtenerVehiculoHandler.cs`.

**BDD:** escenario `V04` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query(Id) → VehicleResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<VehicleResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleQueryHandlerTests; LifecycleBoundaryTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Vehicle, Appointment` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### V05 · Vehiculos/ActualizarVehiculo · IU-01/PA-05

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Vehiculos/ActualizarVehiculo/` + `Infrastructure/Data/Mutation.cs`, `Lifecycle.cs`, `AuditSnapshot.cs`. Archivos del caso: `ActualizarVehiculoContracts.cs`, `ActualizarVehiculoData.cs`, `ActualizarVehiculoEndpoint.cs`, `ActualizarVehiculoHandler.cs`.

**BDD:** escenario `V05` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,404,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `VehicleRequest con Version → VehicleResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<VehicleResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, VehicleRequest r, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleValidatorTests; SupabaseLifecycleTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Vehicle, UserAccount, CatalogItem, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `VEHICULO_ACTUALIZADO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### V06 · Vehiculos/ArchivarVehiculo · IU-01/PA-10

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Vehiculos/ArchivarVehiculo/` + `Infrastructure/Data/Mutation.cs`, `Lifecycle.cs`, `AuditSnapshot.cs`. Archivos del caso: `ArchivarVehiculoContracts.cs`, `ArchivarVehiculoData.cs`, `ArchivarVehiculoEndpoint.cs`, `ArchivarVehiculoHandler.cs`.

**BDD:** escenario `V06` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `204; 400,401,403,404,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Command(Id,Version) → NoContent`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, long version, HttpContext http, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleFlowTests.VehicleArchiveRetainsIdentityAndRejectsNewWrites; LifecycleMutationHandlerTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Vehicle, BookingBlock, Appointment, Inspection, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `VEHICULO_ARCHIVADO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### V07 · Inspecciones/ConsultarHistorial · CU-10/PA-10

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Inspecciones/ConsultarHistorial/` + `Infrastructure/Http/Pagination.cs`. Archivos del caso: `ConsultarHistorialContracts.cs`, `ConsultarHistorialData.cs`, `ConsultarHistorialEndpoint.cs`, `ConsultarHistorialHandler.cs`.

**BDD:** escenario `V07` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query → PageResponse<HistoryResponse>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<PageResponse<HistoryResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → BusinessFlowTests.PA10OtherOwnerCannotReadHistoryEvenWithSpoofedAdminJWTRole; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Inspection, Vehicle, Appointment` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### C01 · Certificados/SolicitarCertificado · CU-06/PA-06

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Certificados/SolicitarCertificado/` + `Infrastructure/Data/Mutation.cs`, `Lifecycle.cs`, `AuditSnapshot.cs`, `Features/Certificados/Common/*`, `CertificateIssuer.cs`. Archivos del caso: `SolicitarCertificadoContracts.cs`, `SolicitarCertificadoData.cs`, `SolicitarCertificadoEndpoint.cs`, `SolicitarCertificadoHandler.cs`.

**BDD:** escenario `C01` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `202/200; 400,401,403,404,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Command(Id) → CertificateResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<CertificateResponse>, Accepted<CertificateResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, HttpContext h, Handler handler, CancellationToken ct);
```

**TDD:** Red → BusinessFlowTests.PA06cOutboxRetriesPreserveCertificateIdentityAndDoNotDependOnMongo; LifecycleMutationHandlerTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Inspection, Certificate, OutboxMessage, IdempotencyEntry, AuditEvent` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `CERTIFICADO_SOLICITADO`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### C02 · Certificados/ListarCertificados · CU-06/PA-06

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Certificados/ListarCertificados/` + `Infrastructure/Http/Pagination.cs`, `Features/Certificados/Common/*`, `CertificateIssuer.cs`. Archivos del caso: `ListarCertificadosContracts.cs`, `ListarCertificadosData.cs`, `ListarCertificadosEndpoint.cs`, `ListarCertificadosHandler.cs`.

**BDD:** escenario `C02` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,401,403,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query → PageResponse<CertificateResponse>`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<PageResponse<CertificateResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleQueryHandlerTests; SupabaseFullLifecycleTests.CheckCertificates; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Certificate` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### C03 · Certificados/ObtenerCertificado · CU-06/PA-06

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Certificados/ObtenerCertificado/` + `Infrastructure/Http/Pagination.cs`, `Features/Certificados/Common/*`, `CertificateIssuer.cs`. Archivos del caso: `ObtenerCertificadoContracts.cs`, `ObtenerCertificadoData.cs`, `ObtenerCertificadoEndpoint.cs`, `ObtenerCertificadoHandler.cs`.

**BDD:** escenario `C03` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403,404,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query(Id) → CertificateResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<CertificateResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleQueryHandlerTests; LifecycleBoundaryTests; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Certificate` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### C04 · Certificados/DescargarCertificado · CU-06/PA-06

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Certificados/DescargarCertificado/` + `Infrastructure/Http/Pagination.cs`, `Features/Certificados/Common/*`, `CertificateIssuer.cs`. Archivos del caso: `DescargarCertificadoContracts.cs`, `DescargarCertificadoData.cs`, `DescargarCertificadoEndpoint.cs`, `DescargarCertificadoHandler.cs`.

**BDD:** escenario `C04` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 401,403,404,409,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query(Id) → FileContentHttpResult`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<FileContentHttpResult, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, HttpContext http, Handler handler, CancellationToken ct);
```

**TDD:** Red → LifecycleMutationHandlerTests.UnauthorizedPdfNeverLoadsBinaryContent; BusinessFlowTests.PA05PA06PA07FullFlowClosesImmutablyAndDownloadsIdenticalSignedBytes; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Certificate` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `—`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

### C05 · Certificados/VerificarCertificado · CU-07/PA-07

**Contexto mínimo:** C0 + `AutoCheck.Api/Features/Certificados/VerificarCertificado/` + `Infrastructure/Http/Pagination.cs`, `Features/Certificados/Common/*`, `CertificateIssuer.cs`. Archivos del caso: `VerificarCertificadoContracts.cs`, `VerificarCertificadoData.cs`, `VerificarCertificadoEndpoint.cs`, `VerificarCertificadoHandler.cs`.

**BDD:** escenario `C05` en `behavior/lifecycle-endpoints.feature`; aplicar las fronteras comunes que corresponden a HTTP `200; 400,404,409,429,503`. No añadir códigos o transiciones fuera de esta fila.

**CDD:** `Query(Id,Hash) → VerificationResponse`. Firmas de records en los archivos indicados; frontera HTTP exacta:

```csharp
private static async Task<Results<Ok<VerificationResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct);
```

**TDD:** Red → BusinessFlowTests.PA05PA06PA07FullFlowClosesImmutablyAndDownloadsIdenticalSignedBytes; para rutas protegidas añadir `LifecycleBoundaryTests.EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding`. Los adaptadores `IData` son puertos exclusivos del caso, sustituibles por NSubstitute; no simular DbSet/IQueryable. Green conserva locks y transacción; Refactor comprueba proyecciones/cancelación y cero warnings.

**Supabase:** comprobar `Certificate, Inspection` y persistencia/respuesta de esta fila con JWT real; verificar filtros de propiedad/centro, auditoría `SEGURIDAD_RECHAZADA si integridad inválida`, y que anon/authenticated no reciben USAGE del esquema privado. Para mutaciones, verificar resultado con un DbContext nuevo; conservar evidencia, nunca DELETE/reset compartido. Revisar los triggers existentes de inmutabilidad si afecta historial y las funciones ejecutadas por el caso; no crear RPC/RLS ficticias. La ejecución hosted sigue pendiente de secretos.

## Ola 4: puerta de aceptación

1. Locked restore, build Release con warnings como errores y suite xUnit en PostgreSQL loopback. Fixtures destructivas aceptan solo prefijo `autocheck_tests` y loopback. MongoDB loopback es una dependencia separada de la prueba de proyección.
2. `--check-database`: seis migraciones, cero pendientes, conexión runtime limitada y CRUD reversible. Inspección MCP de grants, triggers y funciones existentes; no usar credencial runtime para migrar.
3. `SupabaseLifecycleTests`: Auth real con cuenta sintética preconfigurada, CRUD lógico de vehículo y auditoría persistida. `SupabaseFullLifecycleTests`: altas sintéticas con Auth Admin, roles/permisos desde PostgreSQL, perfil, vehículos, pago simulado/cierre, PDF firmado/QR, auditoría, refresh/reset/logout y archivos lógicos. Kestrel real, sin dobles de Auth ni esquema destruido. Solo despacha el aggregate de certificado creado en la ejecución; las cuentas nuevas se bloquean e inactivan al terminar y la evidencia se conserva.
4. Verificar byte a byte descarga repetida, SHA-256 y firma CMS con certificado académico confiable. Una firma autocontenida no sustituye la distribución de confianza académica.
5. PA-10b/PA-06d y objetivos de memoria/latencia requieren medición con volumen y entorno representativos. No inferir cumplimiento por el build o pruebas funcionales.
