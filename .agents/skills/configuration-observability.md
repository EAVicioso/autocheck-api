# Configuration and observability
1. Define typed settings and validate configuration for implemented integrations in shared `AutoCheck.Api/` infrastructure. Use environment settings, local user secrets and protected Azure configuration.
2. Never commit or log credentials, JWTs, connection strings, raw defect JSON, payment details or unnecessary personal data. Keep `appsettings*.json` limited to non-secret defaults.
3. Use structured logs with safe correlation identifiers. Keep `/health` useful for liveness; add bounded, non-sensitive dependency readiness checks only when needed.
4. Test missing or invalid settings and safe failure behavior; avoid requiring configuration for integrations not yet implemented.