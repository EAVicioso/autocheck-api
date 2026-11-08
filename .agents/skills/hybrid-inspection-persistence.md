# Hybrid inspection defect persistence
1. Validate authorization and JSON shape/size before storing raw JSON via `MongoDB.Driver` in Atlas database `autocheck_inspections_db`, collection `reportes_defectos`.
2. Generate or obtain the ObjectId; save its canonical string to PostgreSQL `REPORTE_DEFECTO.DEF_REGISTRO` with EF Core. Verify mapping and length.
3. No transaction is atomic across both stores. Design durable operation state and idempotency so retries reuse document identity; implement reconciliation or an outbox where needed.
4. If MongoDB succeeds but PostgreSQL fails, retain non-sensitive recovery information; compensate only for documents known to belong to the operation and unreferenced. Handle uncertain outcomes explicitly.
5. Check ownership and role before reads, handle invalid or missing references, and test success, failure, retry and orphan recovery with isolated stores under `AutoCheck.Tests/Features/Inspections/`.