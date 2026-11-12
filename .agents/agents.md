# AutoCheck API agent instructions

Read the shared [VSA/.NET 10/C# 14 guide](../docs/architecture/AI_Backend_Architecture_Guidelines.md) and root `AGENTS.md` first. The adapted guide takes precedence over older scaffold conventions in this registry. Owners: A Sebastián Tavares, B Manuel Polo, C Engels Vicioso. Track review follow-ups in `docs/reviews/pr-1-follow-up.md`.

## System Architecture & Tech Stack Overview
The architecture is a modular monolith of vertical slices under `AutoCheck.Api/Features/{Resource}/{UseCase}/`, using C# 14 and .NET 10 Minimal APIs. `global.json` selects the SDK. Use EF Core 10/Npgsql with Supabase PostgreSQL, MongoDB Atlas for asynchronous inspection snapshots, Supabase Auth for JWTs, SendGrid for email, Gemini for FAQ and the project's academic payment sandbox. Docker, GitHub Actions and Azure App Service are the delivery stack. Use FluentValidation and xUnit; add no test library without a concrete need.

PR #1 added business endpoints, `AutoCheckDb`, migrations, JWT/session/RBAC enforcement, sandbox payments, outbox, MongoDB delivery and CI. Inspect the current branch before making assumptions. Existing module routers/partial handlers are an incremental starting point, not evidence of complete per-use-case co-location. Provider configuration, protection activation and formal load acceptance require separate evidence.

## Vertical Slice Implementation Standard
Co-locate new/extracted cases in `AutoCheck.Api/Features/{Resource}/{UseCase}/`, including endpoint, request/response, handler and validator when needed. Do not create empty files or horizontal layers to satisfy a template. Map routes with named `IEndpointRouteBuilder` extensions composed from `Program.cs`; specify verb, route, name, response contracts and authorization. Keep entities out of responses. Explicitly run `ValidateAsync` through the handler or wired endpoint filter: assembly scanning alone does not validate requests. Return validation problems before side effects. Mirror unit tests under `AutoCheck.Tests/Features/{Resource}/{UseCase}/`; cross-feature HTTP scenarios may remain under `Integration`.

## Hybrid Data Persistence Protocol
PostgreSQL schema `autocheck` stores the complete authoritative closure atomically, including defects, counts, rule snapshot, verdict, audit and outbox. MongoDB is an asynchronous projection, never the sole defect source. Copy raw inspection defect JSON to MongoDB database `autocheck_inspections_db`, collection `reportes_defectos`; save its canonical MongoDB ObjectId string in PostgreSQL `autocheck."Inspection"."MongoId"`. Confirm schema and length. The databases cannot share an atomic transaction: implement durable idempotency, retry and reconciliation, with compensation only when safe. Account for uncertain outcomes and protect reads and writes with role and resource checks. See [hybrid persistence](skills/hybrid-inspection-persistence.md).

## Security & RBAC Enforcement
Configure ASP.NET Core JWT validation for the trusted Supabase issuer, audience, signature and lifetime, with key rotation. Call `UseAuthentication()` before `UseAuthorization()`. Derive identity from validated `sub`/`session_id`; load functional roles, permissions and center scope from PostgreSQL on each protected request through `SessionGuard`/`AppActor`. Discard provider business-role claims and user-editable metadata. Retain resource filters; do not replace them with an assumed RLS interceptor. Return 401 for invalid or missing authentication and 403 for denied access. Never log tokens.

## Code Style & Formatting Guidelines
Use idiomatic nullable-aware C# 14 supported by the pinned .NET 10 SDK, without preview features. Prefer file-scoped namespaces, immutable DTO records, meaningful names and top-level statements in the composition root. Prefer primary constructors for simple DI; use `field` and `extension` only where useful, as described in the shared guide. Use `async`/`await` for I/O, propagate cancellation tokens, avoid blocking waits and dispose owned resources. Keep the existing `ApiReply`/`ApiContract` error contract instead of introducing a competing Result abstraction. Never leak exceptions, entities or secrets.

## Build, Test & Migration Commands
Run from the repository root:
```powershell
dotnet restore AutoCheck.slnx --locked-mode
dotnet build AutoCheck.slnx -c Release --no-restore -warnaserror
dotnet test AutoCheck.slnx -c Release --no-restore
dotnet run --project AutoCheck.Api/AutoCheck.Api.csproj
docker build -f AutoCheck.Api/Dockerfile -t autocheck-api:local .
```
Use an EF Core 10-compatible `dotnet-ef` CLI and the established context/migration folder. Review the target before applying any schema changes:
```powershell
dotnet ef migrations add <MigrationName> --project AutoCheck.Api/AutoCheck.Api.csproj --startup-project AutoCheck.Api/AutoCheck.Api.csproj --context AutoCheckDb --output-dir Infrastructure/Data/Migrations
dotnet ef database update --project AutoCheck.Api/AutoCheck.Api.csproj --startup-project AutoCheck.Api/AutoCheck.Api.csproj --context AutoCheckDb
```
Review migration SQL and target before applying. Keep the established migration folder and model snapshot coherent. Do not reset shared data. Keep credentials in protected configuration.

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
