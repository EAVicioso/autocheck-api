# Supabase authentication and RBAC
1. Configure JWT bearer validation for trusted Supabase issuer, audience, signing keys and lifetime; support rotation and deployed HTTPS metadata.
2. Register authentication and authorization; call `UseAuthentication()` before `UseAuthorization()` and require policies on protected routes.
3. Map trusted server-controlled claims to `Propietario`, `Inspector`, `AdminTaller`, and `AdminINTRANT`. Verify actual Supabase claim configuration; never trust user-editable metadata or request roles.
4. Extract the validated subject and enforce ownership and workshop scope in feature queries in addition to role checks. Return 401 for invalid/missing identity and 403 for denied access.
5. Test all roles, invalid/expired tokens and ownership boundaries under `AutoCheck.Tests/Features/{FeatureName}/`. Never log tokens.