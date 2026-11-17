# Catálogo académico de defectos y proyección de informes

Versión: 1.0 · Fecha de emisión: 2026-11-16 · Autores: Engels Vicioso / Equipo AutoCheck.
Historial de cambios: 1.0, definición inicial Sección C; modificaciones posteriores en Git.

## Alcance EA-06 / EE-04 / CU-05

NT-002-2019 es referencia conceptual según el SRS 1.1, EA-06. Este catálogo es una
decisión académica del proyecto, no una reproducción ni certificación del texto oficial.
Leve, Mediano, Grave y los umbrales pertenecen al modelo simulado. LEG-04/05 y EE-06:
ni el resultado ni el certificado tienen validez oficial de INTRANT o autorizan circulación.

| Código de categoría | Nombre | Subcategorías permitidas |
|---|---|---|
| IDENTIFICACION | Identificación y documentación | VIN, MATRICULA |
| ALUMBRADO | Alumbrado y señalización | FAROS, INTERMITENTES |
| FRENOS | Frenos | SERVICIO, ESTACIONAMIENTO |
| DIRECCION | Dirección | HOLGURAS, ASISTENCIA |
| SUSPENSION | Suspensión | AMORTIGUADORES, ANCLAJES |
| NEUMATICOS | Neumáticos y ruedas | DIBUJO, RUEDAS |
| CHASIS | Chasis y carrocería | ESTRUCTURA, CARROCERIA |
| EMISIONES | Emisiones contaminantes | GASES, ESCAPE |
| SEGURIDAD | Seguridad activa y pasiva | CINTURONES, VISIBILIDAD |

Cada defecto exige `categoria`, `subcategoria`, descripción de 1–300 caracteres,
`severidad` (Leve/Mediano/Grave) y cantidad entera de 1–100. Se admiten hasta 100
entradas por registro. Lista vacía significa cero defectos; registro ausente impide cierre.
V1 congela M=3: aprobado si G=0, Md<=1 y L<=M. Una nueva versión solo aplica a futuros inicios.
La migración inicial existente ya siembra V1/M=3; no duplicar ese registro.
La migración aditiva `SectionCDefectCatalog` siembra nueve categorías y dieciocho
subcategorías en `CatalogItem`, tipos `defectos-categorias-v1` y
`defectos-subcategorias-v1`, con UUID deterministas y `ON CONFLICT DO NOTHING`.
Las subcategorías conservan `ParentId`; no se crean tablas adicionales ni se concede
DDL al rol de ejecución. La validación usa las mismas referencias estáticas V1.
Los IDs comienzan en `30000000` (categorías) y `40000000` (subcategorías).
Un cambio futuro debe preservar V1 y añadir otra versión mediante migración revisada.

## Modelo conceptual y esquema activo

El SVG conceptual se reconcilia sin crear tablas `public`: `INSPECCION.INS_ID_PADRE`
corresponde a `autocheck."Inspection"."PreviousId"`; `NORMA_TECNICA` a `InspectionRule`;
`REPORTE_DEFECTO.DEF_REGISTRO` a `Inspection.MongoId` (24 caracteres hexadecimales).
Los defectos autoritativos viven en `Inspection.Defects` (jsonb), junto con conteos,
snapshots, regla congelada, resultado, auditoría y outbox en PostgreSQL.

## Documento canónico AutoCheckReports.Reports

```json
{
  "_id": {"$oid":"0123456789abcdef01234567"},
  "inspeccionId":"00000000-0000-0000-0000-000000000001",
  "defectos":[{"categoria":"ALUMBRADO","subcategoria":"FAROS","descripcion":"Faro averiado","severidad":"Leve","cantidad":1}],
  "resultado":"Aprobado", "L":1, "Md":0, "G":0, "M":3, "versionReglas":"V1"
}
```

`_id` es BSON ObjectId, inspeccionId UUID textual, defectos un array variable de objetos,
conteos/M enteros no negativos y resultado Aprobado/Rechazado. Los registros históricos
anteriores al catálogo conservan su JSON sin reescritura; sus referencias pueden faltar.
El ObjectId se reserva antes del commit y nunca cambia en reintentos. Solo el consumidor
COPIA_MONGO proyecta cierres mediante upsert atómico por `_id`. La entrega es al menos
una vez; una caída deja el outbox pendiente con backoff hasta 300 segundos. No hay
dependencia síncrona de Atlas para cerrar, consultar historial o firmar certificados.
EF Core configura el modelo y las lecturas; el driver del mismo cliente ejecuta el upsert
atómico, evitando la carrera de una secuencia consultar/insertar.
