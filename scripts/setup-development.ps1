[CmdletBinding()]
param(
    [switch]$UseExistingSecrets,
    [switch]$ConfigureMigrations
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
Push-Location $taskRoot
try {
    if (-not $UseExistingSecrets) {
        $taskCertificate = Join-Path $taskRoot 'certs/supabase-prod-ca-2021.crt'
        $taskHash = (Get-FileHash -LiteralPath $taskCertificate -Algorithm SHA256).Hash
        if ($taskHash -ne '700723581420DD1AC98FD7E9AC529F0EF210EADCAF87FC868A3AD7D114C2F3B7') { throw 'Supabase CA certificate differs from the reviewed certificate.' }
        $taskKey = Read-Host 'Supabase publishable key (sb_publishable_...)'
        if (-not $taskKey.StartsWith('sb_publishable_')) { throw 'Use a publishable key, never a secret/service_role key.' }
        $taskPassword = Read-Host 'Password for autocheck_api (provided securely by the maintainer)' -AsSecureString
        $taskPlain = [Net.NetworkCredential]::new('', $taskPassword).Password
        if ([string]::IsNullOrWhiteSpace($taskPlain)) { throw 'Database password is required.' }
        # Quote connection-string values so special characters are preserved.
        $taskQuoted = '"' + $taskPlain.Replace('"', '""') + '"'
        $taskBase = 'Host=aws-1-us-west-2.pooler.supabase.com;Port=5432;Database=postgres;SSL Mode=VerifyFull;Root Certificate="' + $taskCertificate.Replace('"','""') + '";'
        $taskSettings = @{
            'ConnectionStrings:Postgres' = $taskBase + 'Username=autocheck_api.odrsvktbllsfdqyazxsg;Password=' + $taskQuoted + ';Maximum Pool Size=4'
            'Supabase:Url' = 'https://odrsvktbllsfdqyazxsg.supabase.co'
            'Supabase:PublishableKey' = $taskKey
            'Sandbox:WebhookSecret' = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(36))
        }
        if ($ConfigureMigrations) {
            $taskMigrationPassword = Read-Host 'Password for autocheck_migrations (maintainers only)' -AsSecureString
            $taskMigrationPlain = [Net.NetworkCredential]::new('', $taskMigrationPassword).Password
            if ([string]::IsNullOrWhiteSpace($taskMigrationPlain)) { throw 'Migration password is required.' }
            $taskSettings['ConnectionStrings:PostgresMigrations'] = $taskBase + 'Username=autocheck_migrations.odrsvktbllsfdqyazxsg;Password="' + $taskMigrationPlain.Replace('"','""') + '";Maximum Pool Size=2'
        }
        $taskSettings | ConvertTo-Json -Compress | dotnet user-secrets set --project AutoCheck.Api
        if ($LASTEXITCODE -ne 0) { throw 'Could not save local user-secrets.' }
        $taskPlain = $null; $taskQuoted = $null; $taskMigrationPlain = $null; $taskSettings = $null
    }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    dotnet restore AutoCheck.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet build AutoCheck.slnx -c Release --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $taskOldEnvironment = $env:DOTNET_ENVIRONMENT
    try {
        $env:DOTNET_ENVIRONMENT = 'Development'
        dotnet run --project AutoCheck.Api -c Release --no-build -- --check-database
        if ($LASTEXITCODE -ne 0) { throw 'EF connectivity verification failed.' }
    } finally { $env:DOTNET_ENVIRONMENT = $taskOldEnvironment }
    Write-Host 'Ready. Run: dotnet run --project AutoCheck.Api'
    Write-Host 'Scalar: http://localhost:5004/scalar/'
} finally { Pop-Location }
