# Supabase database CA

`supabase-prod-ca-2021.crt` is a **public root certificate**, not a private key.
Downloaded from the project's Database Settings → SSL configuration on 2026-11-12:
https://supabase-downloads.s3-ap-southeast-1.amazonaws.com/prod/ssl/prod-ca-2021.crt

File SHA-256: `700723581420dd1ac98fd7e9ac529f0ef210eadcaf87fc868a3ad7d114c2f3b7`.
Use `SSL Mode=VerifyFull;Root Certificate=<absolute path>` with Npgsql.
The setup script resolves each developer's path; publish includes this public
certificate at `certs/supabase-prod-ca-2021.crt` (`/app/certs/supabase-prod-ca-2021.crt`
in the Docker image). Azure must use that container path. No operating-system trust-store changes
are needed. Review the source and fingerprint when Supabase rotates this CA.
