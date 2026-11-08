# Vertical slice implementation
1. Create endpoint, request/response record, validator and feature-specific data access files under `AutoCheck.Api/Features/{FeatureName}/`, with matching file-scoped namespaces and use-case-specific names.
2. Implement a named `IEndpointRouteBuilder` extension with HTTP verb, route, name, response contracts and explicit authorization; register it in `AutoCheck.Api/Program.cs`.
3. Keep queries and orchestration in the slice, inject shared infrastructure only where needed, and pass cancellation tokens through async operations.
4. Invoke `IValidator<T>.ValidateAsync` in the handler or wired filter; registration alone is insufficient. Return `Results.ValidationProblem` before side effects.
5. Add tests under `AutoCheck.Tests/Features/{FeatureName}/` for success, validation, authorization and failures.