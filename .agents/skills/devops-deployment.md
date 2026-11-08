# Build and deployment
1. Use SDK 10.0.403 with latest patch roll-forward and `net10.0`; build from the repository root with `docker build -f AutoCheck.Api/Dockerfile -t autocheck-api:local .`.
2. Maintain the multi-stage Docker build with `mcr.microsoft.com/dotnet/aspnet:10.0`; exclude secrets and unnecessary artifacts from build context and image.
3. Put GitHub Actions workflows under `.github/workflows/`, restore/build/test before publishing, pin actions appropriately and use least-privilege credentials. No workflow currently exists.
4. Deploy to authorized Azure App Service environments with protected settings and health checks. Review migration compatibility and recovery separately from image rollback; obtain appropriate deployment authorization.