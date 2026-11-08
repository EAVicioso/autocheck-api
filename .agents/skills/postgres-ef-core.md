# PostgreSQL and EF Core
1. Define the DbContext in shared infrastructure under `AutoCheck.Api/`; explicitly map verified schema, tables and columns including `REPORTE_DEFECTO.DEF_REGISTRO` and constraints.
2. Register Npgsql using protected configuration; never commit credentials or log connection strings.
3. Keep feature queries in `AutoCheck.Api/Features/{FeatureName}/`, use async EF APIs, cancellation, projections and `AsNoTracking` for read-only queries. Enforce integrity in the database as well as validation.
4. Use relational transactions for related PostgreSQL writes; handle expected concurrency and constraint failures without exposing provider errors.
5. Review migrations for mapping and data safety. Follow the master commands after the DbContext and EF CLI exist. Test provider behavior and migrations against isolated PostgreSQL.