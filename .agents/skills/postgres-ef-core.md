# PostgreSQL and EF Core
Read `docs/architecture/AI_Backend_Architecture_Guidelines.md` for the shared persistence/security decisions.

1. Use `AutoCheck.Api/Infrastructure/Data/AutoCheckDb.cs`; map the verified `autocheck` schema, tables and constraints. Old SQL names such as `REPORTE_DEFECTO.DEF_REGISTRO` are legacy references, not evidence of the deployed schema. Do not remap current entities without a reviewed migration.
2. Register Npgsql using protected configuration; never commit credentials or log connection strings.
3. Keep feature queries in `AutoCheck.Api/Features/{Resource}/{UseCase}/`, use async EF APIs, cancellation, projections and `AsNoTracking` for read-only queries. Retain explicit ownership/center filters and enforce integrity in the database as well as validation. Do not assume EF inherits PostgREST identity or an RLS interceptor replaces authorization.
4. Use relational transactions for related PostgreSQL writes; handle expected concurrency and constraint failures without exposing provider errors.
5. Review migrations for mapping and data safety in `Infrastructure/Data/Migrations`; follow the master commands with an EF Core 10-compatible CLI. Test provider behavior and migrations against isolated PostgreSQL. Choose indexes for actual predicates and validate query plans with representative data.
