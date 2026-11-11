# AutoCheck API — SRS 1.1

Read `.agents/agents.md` and `docs/implementation.md`. User-approved decisions in this file supersede older notes describing four independent roles or MongoDB as the only defect store.

## Architecture

Use .NET 10 Minimal APIs and vertical slices under `AutoCheck.Api/Features/`. Each use case owns its DTOs, validation, authorization, queries and tests. Shared infrastructure is limited to persistence, identity, errors, clock, outbox and external adapters. Avoid generic repositories or HTTP calls between slices.

PostgreSQL schema `autocheck` is private and authoritative. A closed inspection contains defects, counts, frozen rule version/M, verdict and audit atomically. MongoDB is an asynchronous, idempotent projection via the transactional outbox. Never require Atlas to read authoritative history or issue a certificate.

Canonical roles: Ciudadano, Inspector, Administrador. Server database permissions and center scope are authoritative; JWT role/user_metadata fields are not. Every protected query checks ownership, assignment or administrative scope.

## Code Review Rules

### Transactions and concurrency
- Flag any reservation that does not lock/recheck the block in PostgreSQL, extends its 300-second lifetime on retry, accepts approval at or after expiry, or overwrites another request's booking (RF-CIT-01–03 / PA-03–04).
- Flag a split inspection close, changed frozen rules, edits to closed history, or synchronous dependence on MongoDB (RF-INS-01 / PA-05).
- Treat outbox delivery as at-least-once. Preserve operation/document/certificate identity across uncertain outcomes and retries.

### Access and certificates
- Flag user-controlled permissions, stale authorization, cross-owner/center access, and JWT-only inactivity enforcement (PA-02/10/11).
- Certificates require approval, canonical manifest hash, embedded verifiable PDF signature, immutable final bytes and owner-only download. QR verification exposes only approved public fields (PA-06–07).

### Requirement contracts
- Preserve the exact `/api/asistencia` contract and two-second provider/three-second contingency budgets. The chatbot must never execute transactional actions (PA-09).
- Daily metrics use Santo Domingo close dates; alerts use 21 consecutive inspections per inspector/center, including across dates and ramps (PA-08).
- Link changes to RF/CU/PA. Mechanical checks belong in CI. Never disable or skip a required check to obtain a green merge.

## Checks

Run `dotnet restore AutoCheck.slnx --locked-mode`, `dotnet build AutoCheck.slnx -c Release --no-restore -warnaserror` and `dotnet test AutoCheck.slnx -c Release --no-restore`.
Integration tests require isolated loopback PostgreSQL; the CI integration job also supplies MongoDB. Never run destructive tests against shared Supabase/Atlas. No secrets or private signing keys in source, test artifacts or logs.
