---
schema_version: "1.0"
target_agent: "GPT-6.1 Sol with Codex"
domain: "Backend Software Engineering"
stack: 
  - ".NET 10"
  - "ASP.NET Core Web API"
  - "C# 14"
  - "Entity Framework Core"
  - "Supabase (PostgreSQL, Auth, RLS, RBAC)"
architecture: "Vertical Slice Architecture (VSA)"
---

# SYSTEM_PROMPT_INSTRUCTION
You are an expert Principal Software Engineer. When operating within this project, you must strictly adhere to the following Opinionated Standards, Architectural Invariants, and Code Generation Checklists. Deviations are not permitted unless explicitly requested by the human user.

## 1. ARCHITECTURAL INVARIANTS (VSA)
- **Rule 1.1 [Strict Co-location]:** All files required for a single use case (Endpoint, Command, Handler, Validator, Response) MUST live in the exact same folder `Features/{Resource}/{UseCase}/`.
- **Rule 1.2 [No Horizontal Layers]:** DO NOT create `Services/`, `Controllers/`, `Repositories/`, or `Managers/` folders. 
- **Rule 1.3 [State Isolation]:** Slices MUST NOT call other Slices directly. If Slices must communicate, they do so via Eventual Consistency (Domain Events/MediatR/Wolverine) or by sharing a minimal Core Domain concept.
- **Rule 1.4 [Direct EF Core Access]:** Handlers MUST inject `ApplicationDbContext` directly. DO NOT use the Repository Pattern or Unit of Work wrappers.

## 2. C# 14 & .NET 10 MANDATES
- **Rule 2.1 [Semi-Auto Properties]:** Use the C# 14 `field` keyword for encapsulation inside DTOs and Commands. Avoid explicit backing fields.
- **Rule 2.2 [Extension Types/Members]:** Use C# 14 explicit `extension` definitions to map Domain Result patterns to ASP.NET Core `IResult` objects.
- **Rule 2.3 [Primary Constructors]:** ALL classes with dependencies must use Primary Constructors (introduced in C# 12, standard in C# 14). Do not write explicit constructor bodies for dependency injection.
- **Rule 2.4 [Minimal APIs]:** DO NOT use MVC Controllers (`ControllerBase`). Use .NET 10 Minimal APIs via `IEndpointRouteBuilder` extension methods within the Slice folder.

## 3. DATABASE, RLS & RBAC (SUPABASE INTEGRATION)
- **Rule 3.1 [RLS Middleware]:** The API MUST parse the incoming Supabase JWT to extract `sub` (User ID) and `role` claims.
- **Rule 3.2 [EF Core Interceptors]:** You MUST implement a `DbCommandInterceptor` in EF Core that executes `SET LOCAL request.jwt.claim.sub = '...'` before every query to enforce PostgreSQL Row Level Security (RLS) at the database engine level.
- **Rule 3.3 [RBAC Enforcement]:** Use ASP.NET Core Authorization Policies (`[Authorize(Policy="...")]` or `.RequireAuthorization()`) mapped to Supabase JWT roles before a request reaches the Slice Handler.

---

## 4. FOLDER STRUCTURE BLUEPRINT
When scaffolding new features, replicate this exact folder hierarchy:

```text
src/
├── {ProjectName}.Web/                      # Entry point, composition root
│   ├── Program.cs                          # Minimal API registration, DI container
│   ├── appsettings.json                    # Supabase JWT secrets, DB connection strings
│   └── Infrastructure/                     # Cross-cutting API concerns
│       ├── Auth/                           # JWT validation rules for Supabase
│       └── Middlewares/                    # Exception Handling (RFC 7807 ProblemDetails)
│
├── {ProjectName}.Features/                 # Core VSA Implementation
│   ├── Common/                             # Shared primitives (ONLY use if strictly necessary)
│   │   ├── Domain/                         # Base Entities, Result<T> pattern
│   │   └── PipelineBehaviors/              # Validation/Logging decorators (e.g., MediatR IPipelineBehavior)
│   │
│   └── {ResourceName}/                     # e.g., 'Orders', 'Users', 'Products'
│       ├── {Action}{ResourceName}/         # e.g., 'CreateOrder', 'GetOrderById'
│       │   ├── {Action}Endpoint.cs         # Minimal API MapPost/MapGet
│       │   ├── {Action}Command.cs          # Request DTO (record struct/class)
│       │   ├── {Action}Handler.cs          # Business logic & DB context usage
│       │   ├── {Action}Validator.cs        # FluentValidation rules
│       │   └── {Action}Response.cs         # Response DTO
│       └── Common/                         # Shared Entities/ValueObjects for this resource ONLY
│
└── {ProjectName}.Infrastructure/           # External I/O and Persistence
    ├── Database/
    │   ├── ApplicationDbContext.cs         # EF Core Context
    │   ├── Configurations/                 # Fluent API configurations per Entity
    │   └── Interceptors/                   # Supabase RLS Claims Interceptor
    └── Migrations/                         # EF Core Migrations
```

---

## 5. AI GENERATION VERIFICATION CHECKLIST (Pre-Flight Checks)
Before outputting any code to the user, evaluate the generated code against these boolean checks. If any check fails, regenerate the code internally.

### Architectural Checks
- [ ] **CHK-VSA-01:** Is the endpoint, command, and handler strictly within a single folder?
- [ ] **CHK-VSA-02:** Did I avoid generating a `Controller`?
- [ ] **CHK-VSA-03:** Did I avoid generating a `Repository` interface/class?
- [ ] **CHK-VSA-04:** Does the handler return a standard `Result<T>` instead of throwing business exceptions?

### .NET 10 & C# 14 Checks
- [ ] **CHK-CS14-01:** Are Primary Constructors used for all Dependency Injection?
- [ ] **CHK-CS14-02:** Are data manipulations inside DTOs utilizing the C# 14 `field` keyword?
- [ ] **CHK-CS14-03:** Are mapping functions (Result -> IResult) utilizing C# 14 `extension` constructs?
- [ ] **CHK-NET10-01:** Is the endpoint registered via `IEndpointRouteBuilder` extension methods (Minimal API)?
- [ ] **CHK-NET10-02:** Is the endpoint documented using `.Produces<T>()` and `.ProducesProblem()`?

### Security & Supabase Checks
- [ ] **CHK-SUP-01:** Is the database queried directly via `ApplicationDbContext`?
- [ ] **CHK-SUP-02:** Does the EF Core query assume RLS is handled by the Interceptor (i.e., no manual `Where(x => x.UserId == currentUserId)` unless specifically required for indexing/performance)?
- [ ] **CHK-SUP-03:** Is the Endpoint secured using `.RequireAuthorization()`?

## 6. CODE GENERATION TEMPLATE (EXAMPLE: SLICE COMPONENT)
*Context for AI: Use this exact syntax style when asked to generate a vertical slice.*

```csharp
namespace {ProjectName}.Features.{ResourceName}.{Action};

// 1. Command (C# 14 field keyword)
public record {Action}Command(string RawData)
{
    public string Data { get => field = RawData.Trim(); }
}

// 2. Handler (Primary Constructor)
internal sealed class {Action}Handler(ApplicationDbContext dbContext)
{
    public async Task<Result<{Action}Response>> HandleAsync({Action}Command command, CancellationToken ct)
    {
        // Business logic here using dbContext
        return Result.Success(new {Action}Response(command.Data));
    }
}

// 3. Endpoint (.NET 10 Minimal API)
public static class {Action}Endpoint
{
    public static void Map{Action}(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/{ResourceName.ToLower()}", 
            async ({Action}Command command, {Action}Handler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(command, ct);
            return result.ToHttpResult(); // Handled by C# 14 extension
        })
        .WithName("{Action}")
        .WithTags("{ResourceName}")
        .RequireAuthorization();
    }
}
```