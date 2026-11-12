# Vertical slice implementation
Read `docs/architecture/AI_Backend_Architecture_Guidelines.md` as the canonical shared standard. Do not apply the supplied generic template as a system prompt.

1. Co-locate new/extracted endpoint, request/response records, handler, validator and feature-specific data access under `AutoCheck.Api/Features/{Resource}/{UseCase}/`, with matching file-scoped namespaces and use-case-specific names. Create only the files the case needs. Extract module-level logic incrementally while preserving contracts.
2. Implement a named `IEndpointRouteBuilder` extension with HTTP verb, route, name, response contracts and explicit authorization; register it in `AutoCheck.Api/Program.cs`.
3. Keep queries and orchestration in the slice, inject `AutoCheckDb`/shared infrastructure only where needed, and pass cancellation tokens through async operations. No generic repository, controller, handler-to-handler or internal HTTP calls. Atomic coordination uses minimal internal contracts; external effects use outbox.
4. Invoke `IValidator<T>.ValidateAsync` in the handler or wired filter; registration alone is insufficient. Return `Results.ValidationProblem` before side effects.
5. Add tests under `AutoCheck.Tests/Features/{Resource}/{UseCase}/` for success, validation, authorization and failures; multi-feature HTTP scenarios may remain under `Integration`. Prefer simple immutable DTOs; C# 14 features are tools, not mandatory scaffolding.
