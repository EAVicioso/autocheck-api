# Validación RS-08

Versión 1.0 · Equipo AutoCheck · 2026-11-11.

Ejecutar perfiles por separado con k6 y datos sintéticos. Estas pruebas requieren una API de validación configurada y evidencia real de emulación de red **por cliente virtual**; no basta con `sleep`, una limitación global del servidor o una declaración del operador sin mediciones.

```sh
k6 run -e PROFILE=history -e BASE_URL=https://api-validacion.example \
  -e TEST_DATA=/ruta/privada/datos.json -e NETWORK_EVIDENCE=/ruta/evidencia.json \
  --summary-export=resultado-historial.json tests/load/acceptance.js
```

Repetir con `PROFILE=pdf`, `assistant` y `contingency`, conservando resultados separados. Para contingencia inducir caída, salida inválida o timeout del proveedor en la infraestructura de prueba; el harness no agrega un endpoint de sabotaje a la API.

`datos.json`: `{"records":[{"token":"<sesion-activa>","vehiculoId":"<id>"},...]}` para historial. Debe haber 10,000 inspecciones sintéticas y consultas a distintos vehículos autorizados. Para PDF proporcionar 20 `inspeccionId` aprobados diferentes y token propietario; para IA proporcionar 20 consultas de uso/ITV y token activo. Los archivos con tokens quedan fuera del repositorio y de artefactos públicos.

`evidencia.json` identifica las mediciones y configuración del emulador:

```json
{
  "perClient": true,
  "downloadMbps": 1.6,
  "uploadKbps": 750,
  "rttMs": 150,
  "packetLossPercent": 0,
  "method": "identificador del emulador e infraestructura utilizados",
  "evidenceReference": "ruta o identificador de configuración y mediciones verificadas"
}
```

El harness valida estos parámetros, pero no instala ni verifica por sí mismo el emulador. El responsable de PA debe comprobar su aplicación individual a los 100 clientes y adjuntar mediciones. No declarar PA-10b aprobada sin esa evidencia.

Historial: rampa 60 segundos a 100 VU, sostenido 300 segundos, una consulta por usuario seguida de pausa de un segundo. Los umbrales se aplican únicamente al sostenido: p95 <3 s y fallos <1 %, páginas <=20 y JSON <=100 KiB. PDF y respuestas funcionales IA: 20 operaciones, cada una <10 s. Contingencias: 20 operaciones <3 s.

Registrar commit, fechas, recursos, servicios reales/dobles, caché, tamaño de datos, versión k6 y evidencia de red. Los rechazos esperados se validan por las pruebas negativas y no se mezclan con esta carga positiva.
