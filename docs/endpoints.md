# Contratos HTTP de AutoCheck

Versión: 1.3 · Fecha de emisión: 2026-11-22 · Autores: Equipo AutoCheck.
Historial de cambios: 1.0 índice inicial; 1.1 contratos Sección C; 1.2 cantidad predeterminada; 1.3 recuperación, confirmaciones de pago y contrato QR (IS-04, IC-03, RF-CER-01).

SRS 1.1. Los esquemas exactos de entrada y salida se publican en `/openapi/v1.json` en desarrollo o con `OpenApi__Enabled=true`.

- `POST /api/auth/restablecimiento` recibe `token` con el `TokenHash` del enlace de correo y `nuevaContrasena`. El servidor canjea la prueba con Supabase.
- La verificación QR satisfactoria devuelve `certificadoId`, `emisionUtc`, `centro` y `resultado`. El estado HTTP distingue éxito, ID inexistente (404) e integridad inválida (409). RF-CER-01 y PA-07a requieren retirar el campo público `integridad`. CI reconoce exclusivamente ese cambio previsto en el contrato 200.
- El webhook de pago conserva su respuesta original al repetir un evento. Una reversión sin cita puede devolver `siguienteAccion: SELECCIONAR_OTRO_HORARIO`.

| Método | Ruta | Operación / trazabilidad | Respuestas documentadas |
|---|---|---|---|
| GET | `/health` | HealthCheck | 200 |
| GET | `/health/ready` | Readiness | 200 |
| GET | `/api/catalogos/{tipo}` | IU-01 | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/vehiculos/{id}/historial` | CU-10 / PA-10 / RS-08 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/inspecciones/{id}/certificado` | CU-06 / PA-06b,c | 202, 400, 401, 403, 404, 409, 503, 200 |
| GET | `/api/auditorias` | CU-13 / PA-13a,b | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/asistencia` | RF-CHAT-01/02 / IS-05 / PA-09 | 200, 400, 503 |
| POST | `/api/auth/registro` | CU-01 / PA-01 / RS-09 | 201, 400, 401, 403, 404, 409, 503 |
| POST | `/api/auth/login` | CU-02 / PA-02 / RS-11 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/auth/refresh` | CU-02 / RS-10 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/auth/recuperacion` | IS-04 / RS-11 | 202, 400, 401, 403, 404, 409, 503 |
| POST | `/api/auth/restablecimiento` | RS-09 / RS-11 | 204, 400, 401, 403, 404, 409, 503 |
| POST | `/api/auth/logout` | CU-02 / RS-10 | 204, 400, 401, 403, 404, 409, 503 |
| POST | `/api/auth/actividad` | RS-10: acción explícita del usuario, nunca polling | 204, 400, 401, 403, 404, 409, 503 |
| GET | `/api/usuarios/me` | 6.2.1.1 / CU-11 | 200, 400, 401, 403, 404, 409, 503 |
| PATCH | `/api/usuarios/me` | 6.2.1.1 | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/usuarios` | CU-11 / PA-11 | 200, 400, 401, 403, 404, 409, 503 |
| PATCH | `/api/usuarios/{id}/acceso` | CU-11 / PA-11a,b | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/vehiculos` | IU-01 / PA-10a | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/vehiculos` | IU-01 | 201, 400, 401, 403, 404, 409, 503 |
| GET | `/api/vehiculos/{id}` | CU-10 / PA-10a | 200, 400, 401, 403, 404, 409, 503 |
| PATCH | `/api/vehiculos/{id}` | IU-01 / PA-05d | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/centros` | CU-12 / RAE-01 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/centros` | CU-12 / PA-12c | 201, 400, 401, 403, 404, 409, 503 |
| GET | `/api/centros/{id}` | CU-12 | 200, 400, 401, 403, 404, 409, 503 |
| PATCH | `/api/centros/{id}` | PA-12b,c | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/centros/{id}/rampas` | CU-12 / PA-12a | 201, 400, 401, 403, 404, 409, 503 |
| PATCH | `/api/centros/{id}/rampas/{rampaId}` | PA-12b,c | 200, 400, 401, 403, 404, 409, 503 |
| PUT | `/api/centros/{id}/horarios` | RAE-01 / PA-12b,c | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/centros/{id}/disponibilidad` | RF-CIT-01/03 / PA-12a | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/citas` | CU-03 / 6.2.1.1 | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/citas/{id}` | CU-03 | 200, 400, 401, 403, 404, 409, 503 |
| DELETE | `/api/citas/retenciones/{id}` | RF-CIT-02 / CU-03 | 204, 400, 401, 403, 404, 409, 503 |
| POST | `/api/citas/{id}/cancelacion` | RF-CIT-02 / PA-03e / PA-04d | 200, 400, 401, 403, 404, 409, 503 |
| PUT | `/api/citas/{id}/inspector` | CU-05 / 6.2.1.1 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/pagos/intentos` | RF-CIT-01/03 / CU-04 / PA-03a,d | 201, 400, 401, 403, 404, 409, 503 |
| GET | `/api/pagos/intentos/{id}` | CU-04 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/pagos/webhook` | IS-03 / PA-04a,c; X-Sandbox-Signature HMAC-SHA256 required | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/inspecciones` | CU-05 / CU-10 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/inspecciones` | RF-INS-01 / PA-05b | 201, 400, 401, 403, 404, 409, 503 |
| GET | `/api/inspecciones/{id}` | CU-05 / CU-10 | 200, 400, 401, 403, 404, 409, 503 |
| PUT | `/api/inspecciones/{id}/registro` | RF-INS-01 / PA-05c,d | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/inspecciones/{id}/cierre` | RF-INS-01 / PA-05a-e | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/inspecciones/{id}/reinspecciones` | Arquitectura DF-05 / CU-10 | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/configuracion/reglas-inspeccion` | RAE-02 | 200, 400, 401, 403, 404, 409, 503 |
| POST | `/api/configuracion/reglas-inspeccion` | RAE-02 / PA-05b | 201, 400, 401, 403, 404, 409, 503 |
| GET | `/api/certificados/{id}` | CU-06 | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/certificados/{id}/pdf` | CU-06 / PA-06a,c | 200, 401, 403, 404, 409 |
| GET | `/api/certificados/{id}/verificacion` | CU-07 / RF-CER-01 / PA-07a,b | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/reportes/inspecciones` | RF-DAS-01/02 / PA-08a,d | 200, 400, 401, 403, 404, 409, 503 |
| GET | `/api/reportes/alertas` | RF-DAS-03 / PA-08b,c,d | 200, 400, 401, 403, 404, 409, 503 |

## DEC-LIFECYCLE-01 additive operations

GET /api/roles; GET /api/permisos; GET /api/usuarios/{id}; GET /api/usuarios/{id}/acceso; DELETE /api/usuarios/{id}?version=N; DELETE /api/vehiculos/{id}?version=N; GET /api/certificados; GET /api/auditorias/{id}.

Los PATCH de perfil, acceso y vehículo aceptan `version` opcional. Cuando se envía, debe coincidir con la versión vigente. Una versión obsoleta devuelve 409. Los clientes anteriores pueden omitirla. DELETE recibe la versión en query, archiva y devuelve 204. Las respuestas de usuarios y vehículos incluyen `version` y `archivadoUtc`. Ver [estado y evidencia](lifecycle-implementation.md) y [matriz de las 30 slices](lifecycle-slices.md).

## Filtros de DEC-LIFECYCLE-01

`pagina` comienza en 1 y admite hasta 500000; `tamano` 1–20. `direccion` asc/desc y `orden` usan allowlist. Respuesta de estos listados: `{items,pagina,siguienteCursor}`. Un cursor se sigue con los mismos filtros/orden/dirección, sin pagina mayor a 1; no concede autorización y cada consulta reaplica sus filtros SQL.

| Listado | Filtros | Orden permitido (predeterminado) | Cursor |
|---|---|---|---|
| usuarios | rol, activo, incluirArchivados, nombre | id (asc), nombre | no |
| vehículos propios | matricula, incluirArchivados | id (asc), matricula | no |
| catálogos | padreId | nombre (asc) | no |
| certificados propios | vehiculoId, estado PENDIENTE/EMITIDO, desdeUtc, hastaUtc | emisionUtc (desc), desempate id | sí |
| auditorías globales | desdeUtc, hastaUtc, operacion, entidad, actorId | fechaUtc (desc), desempate id | sí |
| historial autorizado | estado ABIERTA/CERRADA, resultado Aprobado/Rechazado, desde/hasta locales de inicio | cierreUtc (desc), desempate id | sí |

La ausencia de `incluirArchivados=true` excluye usuarios/vehículos archivados de sus listados; los detalles ordinarios de usuario, acceso y vehículo devuelven 404 para archivados. Se conserva el historial autorizado. Los PATCH compatibles sin `version` usan la versión que lee la transacción. Ese modo conserva el token de concurrencia de EF y carece de la comprobación contra una lectura previa del cliente. El API no permite editar roles/permisos de catálogo ni modificar/borrar auditorías o certificados emitidos.

## Contratos Sección C: CU-05/08/09/10

Una sesión autenticada, incluida la de Inspector, puede consultar
`GET /api/catalogos/defectos-categorias-v1` y
`GET /api/catalogos/defectos-subcategorias-v1?padreId={uuid}`. Ambos devuelven
`{items:[{id,nombre,padreId}]}` y admiten `pagina` y `tamano`. El `nombre`
es el código que se envía en `categoria` o `subcategoria` al registrar defectos.
Un cierre con defectos heredados incompletos devuelve 400. El inspector asignado
puede reemplazar el registro mientras la inspección esté abierta.

Todas requieren `Authorization: Bearer <token>` con sesión activa registrada; roles,
permisos y centro se cargan de PostgreSQL. JSON usa Content-Type application/json,
UUID en formato textual y fechas UTC ISO 8601. `/api` devuelve Cache-Control: no-store.
POST de inicio y cierre requiere `Idempotency-Key` de 1–128 caracteres no vacíos;
actor+método+ruta delimitan la clave y SHA-256 del cuerpo tipado serializado identifica
la solicitud. Replay conserva status/cuerpo/Location; cuerpo diferente devuelve 409.

| Ruta (método) | Entrada | Salida y autorización |
|---|---|---|
| `/api/inspecciones` POST | `{ "citaId": "uuid" }` | 201 Inspection; inspector asignado en su centro y cita pagada confirmada |
| `/api/inspecciones` GET | pagina, tamano, estado, desde, hasta | 200 `{items:[Inspection]}`; propietario, inspector asignado o administrador con auditoria.consultar en su alcance |
| `/api/inspecciones/{id}` GET | id UUID | 200 Inspection; mismo alcance |
| `/api/inspecciones/{id}/registro` PUT | `{ "versionEsperada":0, "defectos":[Defecto] }` | 200 Inspection; solo inspector asignado, abierta y versión vigente |
| `/api/inspecciones/{id}/cierre` POST | `{ "versionEsperada":1 }` | 200 Inspection; mismo inspector; registro explícito previo obligatorio |
| `/api/inspecciones/{id}/reinspecciones` GET | pagina, tamano | 200 `{items:[Inspection]}`; mismo alcance, nueva cita de igual vehículo y rechazo anterior |
| `/api/vehiculos/{id}/historial` GET | pagina, tamano, estado, desde, hasta | 200 `{items:[History]}`; solo cierres autorizados del vehículo |
| `/api/configuracion/reglas-inspeccion` GET | pagina, tamano | 200 `{items:[Rule]}`; sesión activa |
| `/api/configuracion/reglas-inspeccion` POST | `{ "m":4,"vigenciaUtc":"2026-12-01T00:00:00Z" }` | 201 Rule; administrador global con reglas.gestionar; M>=0, vigencia futura |
| `/api/reportes/inspecciones` GET | pagina, tamano, desde, hasta, centroId, rampaId, inspectorId | 200 Metrics; administrador con auditoria.consultar y alcance de centro |
| `/api/reportes/alertas` GET | mismos filtros | 200 `{items:[Alert],ultimaActualizacionUtc:fecha|null}`; mismo permiso |
| `/api/asistencia` POST | `{ "consulta":"texto" }` | 200/400/503 Assistance; sesión activa, sin herramientas transaccionales |

`pagina` empieza en 1 (máximo operativo 500000); `tamano` 1–20, por defecto 20.
`estado` admite ABIERTA/CERRADA. `desde`/`hasta` son fechas ISO locales inclusivas
America/Santo_Domingo de inicio en inspecciones/historial y de cierre en reportes.
El día hasta se traduce a límite UTC exclusivo del día siguiente. Reportes acepta
un máximo de 366 días de diferencia y por defecto el día local actual.
El filtro rampa de alertas selecciona ventanas completas que contienen esa rampa;
siempre conserva el denominador de 21.

### Esquemas JSON

Los campos listados son obligatorios salvo los marcados null/opcionales; no se exponen entidades EF.

- Defecto: `{descripcion:string(1..300),severidad:Leve|Mediano|Grave,cantidad:int(1..100),categoria:string,subcategoria:string}`; cantidad es opcional y vale 1 cuando se omite, conforme al DTO existente. Las referencias del catálogo son obligatorias para registros nuevos. Registro admite 0–100 elementos, nunca null.
- Inspection: `{id,vehiculoId,centroId,rampaId,inspectorId:uuid,inspeccionAnteriorId:uuid|null,inicioUtc:fecha,cierreUtc:fecha|null,estado:ABIERTA|CERRADA,resultado:Aprobado|Rechazado|null,versionReglas:string,m:int,l:int,md:int,g:int,version:int,defectos:[Defecto]|null}`.
- History: `{id:uuid,cierreUtc:fecha|null,resultado:string|null,versionReglas:string,m:int,l:int,md:int,g:int,centroId:uuid,inspeccionAnteriorId:uuid|null}`.
- Rule: `{id:uuid,version:string,m:int,vigenciaUtc:fecha}`.
- Metrics: `{cerradas:int,aprobadas:int,rechazadas:int,porcentajeRechazo:number|null,estado:"Sin datos"|"Con datos",ultimaActualizacionUtc:fecha|null,items:[{dia:fecha,centroId:uuid,rampaId:uuid,inspectorId:uuid,aprobadas:int,rechazadas:int,cerradas:int,porcentajeRechazo:number|null}]}`.
- Alert: `{id:uuid,inspectorId:uuid,centroId:uuid,inspecciones:[uuid x21],desdeUtc:fecha,hastaUtc:fecha,aprobadas:int,total:21,porcentaje:number,fecha:fecha-local}`.

Errores generales: 400 entrada/filtro/registro incompleto; 401 identidad/sesión inválida;
403 acceso denegado; 404 recurso ausente; 409 estado inmutable, concurrencia,
idempotencia o restricción de negocio; 503 persistencia no disponible. 422 no se usa:
la convención de validación existente usa 400. Problem Details incluye `type`, `title`,
`status`, `codigo`, `traceId` y, cuando aplica, `errors` por campo.

### IS-05: asistencia

Entrada tras trim: 1–2000 caracteres. Respuesta contiene exactamente tres claves:
`respuesta` cadena no vacía, `categoria` USO_PLATAFORMA/ITV/FUERA_DE_ALCANCE/NO_DETERMINADA,
`codigo` OK/INFORMACION_INSUFICIENTE/FUERA_DE_ALCANCE/SOLICITUD_INVALIDA/SERVICIO_NO_DISPONIBLE.
200 para respuestas, insuficiencia o fuera de alcance; 400 para entrada inválida sin
llamar al proveedor. También los errores de autenticación conservan este esquema.
Proveedor sustituible, contexto aprobado, espera máxima 2 segundos y cero reintentos
síncronos. Ante error o JSON inválido, 503 antes de 3 segundos con el cuerpo exacto:

```json
{"respuesta":"El servicio de asistencia no está disponible temporalmente. Intenta de nuevo.","categoria":"NO_DETERMINADA","codigo":"SERVICIO_NO_DISPONIBLE"}
```
