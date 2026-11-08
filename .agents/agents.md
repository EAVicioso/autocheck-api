# AutoCheck API agent instructions

## System Architecture & Tech Stack Overview
The intended architecture is a modular monolith of vertical slices under `AutoCheck.Api/Features/{FeatureName}/`, using C# and .NET 10 Minimal APIs. `global.json` selects SDK 10.0.403 with latest patch roll-forward. Use EF Core 10/Npgsql with Supabase PostgreSQL, MongoDB Atlas for inspection documents, Supabase Auth for JWTs, SendGrid for email, Gemini for FAQ and a payment gateway sandbox. Docker, GitHub Actions and Azure App Service are the intended delivery stack. Use FluentValidation, xUnit, Moq and FluentAssertions where appropriate.

The current repository has a health endpoint, OpenAPI, validator scanning and EF/Npgsql, SendGrid and Google GenAI package references. The specified DbContext, MongoDB driver, JWT/RBAC, payment integration, CI workflow, Moq and FluentAssertions are not yet present. Implement these as needed; do not assume existing behavior. Inspect actual schema and configuration first.

## Vertical Slice Implementation Standard
Put each feature's endpoint, request/response DTO records, validator and feature-specific data access in `AutoCheck.Api/Features/{FeatureName}/`. Name files for their use case and responsibility, e.g. `CreateAppointmentEndpoint.cs`, `CreateAppointmentRequest.cs`, `CreateAppointmentResponse.cs`, `CreateAppointmentValidator.cs`. Map routes with named `IEndpointRouteBuilder` extensions registered in `AutoCheck.Api/Program.cs`; specify verb, route, name, response contracts and authorization. Keep entities out of responses. Explicitly run `ValidateAsync` through the handler or wired endpoint filter: assembly scanning alone does not validate requests. Return validation problems before side effects. Mirror tests under `AutoCheck.Tests/Features/{FeatureName}/`.

## Hybrid Data Persistence Protocol
Store raw inspection defect JSON in MongoDB database `autocheck_inspections_db`, collection `reportes_defectos`; save its canonical MongoDB ObjectId string in PostgreSQL `REPORTE_DEFECTO.DEF_REGISTRO`. Confirm schema and length. The databases cannot share an atomic transaction: implement durable idempotency, retry and reconciliation, with compensation only when safe. Account for uncertain outcomes and protect reads and writes with role and resource checks. See [hybrid persistence](skills/hybrid-inspection-persistence.md).

## Security & RBAC Enforcement
Configure ASP.NET Core JWT validation for the trusted Supabase issuer, audience, signature and lifetime, with key rotation. Register authentication and authorization and call `UseAuthentication()` before `UseAuthorization()`. Derive identity and roles from validated, trusted server-controlled claims, not request values or user-editable metadata. Apply explicit least-privilege policies for `Propietario`, `Inspector`, `AdminTaller`, `AdminINTRANT`, plus resource ownership/workshop checks. Return 401 for invalid or missing authentication and 403 for denied access. Never log tokens.

## Code Style & Formatting Guidelines
Use idiomatic nullable-aware C#; C# 13 features are permitted where supported by the .NET 10 SDK. Prefer file-scoped namespaces, immutable DTO records, meaningful names and top-level statements in the existing composition root. Use `async`/`await` for I/O, propagate cancellation tokens, avoid blocking waits and dispose owned resources. Inject dependencies, handle errors explicitly, and return consistent typed results or problem details without leaking exceptions, entities or secrets. Follow existing formatting.

## Build, Test & Migration Commands
Run from the repository root:
```powershell
dotnet restore AutoCheck.slnx
dotnet build AutoCheck.slnx --no-restore
dotnet test AutoCheck.slnx
dotnet run --project AutoCheck.Api/AutoCheck.Api.csproj
docker build -f AutoCheck.Api/Dockerfile -t autocheck-api:local .
```
After adding a DbContext and installing/restoring an EF Core 10-compatible `dotnet-ef` CLI, replace the placeholders and use an approved target connection:
```powershell
dotnet ef migrations add <MigrationName> --project AutoCheck.Api/AutoCheck.Api.csproj --startup-project AutoCheck.Api/AutoCheck.Api.csproj --context <DbContextName> --output-dir Data/Migrations
dotnet ef database update --project AutoCheck.Api/AutoCheck.Api.csproj --startup-project AutoCheck.Api/AutoCheck.Api.csproj --context <DbContextName>
```
Review migration SQL and target before applying. The migration folder is a proposed convention; use the established folder if one exists. Do not reset shared data. Keep credentials in protected configuration.

## Skills Registry Index
1. [Vertical slices](skills/vertical-slices.md)
2. [PostgreSQL and EF Core](skills/postgres-ef-core.md)
3. [Hybrid inspection persistence](skills/hybrid-inspection-persistence.md)
4. [Authentication and RBAC](skills/authentication-rbac.md)
5. [Validation and API errors](skills/validation-api-errors.md)
6. [External integrations](skills/external-integrations.md)
7. [Quality and testing](skills/quality-testing.md)
8. [DevOps and deployment](skills/devops-deployment.md)
9. [Configuration and observability](skills/configuration-observability.md)