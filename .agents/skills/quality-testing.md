# Testing standards
1. Mirror feature paths under `AutoCheck.Tests/Features/{FeatureName}/` and use xUnit to test observable business behavior with deterministic clocks/IDs where needed.
2. Cover validator boundaries, success, errors, roles, ownership, side effects and idempotency; avoid tests that only mirror implementation.
3. Add Moq and FluentAssertions to `AutoCheck.Tests/AutoCheck.Tests.csproj` when useful; they are not currently referenced.
4. Use isolated PostgreSQL/MongoDB integration tests for mappings, migrations and cross-store failures; do not run destructive tests on shared Supabase or Atlas databases.
5. Run `dotnet test AutoCheck.slnx` and the appropriate build; report unavailable infrastructure and skipped verification.