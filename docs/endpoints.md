# Contratos HTTP de AutoCheck

SRS 1.1. Los esquemas exactos de entrada y salida se publican en `/openapi/v1.json` en desarrollo o con `OpenApi__Enabled=true`.

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
| POST | `/api/pagos/webhook` | IS-03 / PA-04a,c | 200, 400, 401, 403, 404, 409, 503 |
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
