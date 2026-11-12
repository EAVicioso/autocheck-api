# Hybrid inspection persistence (approved SRS 1.1 decision)
1. Keep the complete closure (defects, counts, frozen M/version, verdict, audit and outbox) in one PostgreSQL transaction. Never use MongoDB as the only authoritative defect store.
2. Generate and persist a canonical MongoDB ObjectId in Inspection.MongoId. The outbox projects the immutable closure to autocheck_inspections_db.reportes_defectos with upsert using that same ID.
3. Atlas failures leave retryable delivery state; history and certificate issuance read PostgreSQL and remain independent.
4. Test rollback, uncertain delivery outcomes, retry deduplication and recovery using isolated stores. Do not compensate by deleting authoritative history.
