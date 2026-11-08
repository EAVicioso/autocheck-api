# Validation and API errors
1. Put request records and FluentValidation validators in `AutoCheck.Api/Features/{FeatureName}/`; validate required fields, lengths, ranges and business invariants before side effects.
2. Execute validators explicitly with `ValidateAsync` in the handler or a wired endpoint filter. Return validation-problem field errors.
3. Return appropriate 400, 401, 403, 404, 409 and dependency-related 5xx responses using consistent typed results or problem details; do not reveal provider exceptions or sensitive data.
4. When adding centralized exception handling in `Program.cs`, preserve cancellation and log safe correlation identifiers. Test boundaries and response shapes.