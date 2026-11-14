[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$From,
    [string]$HostName = 'smtp.sendgrid.net',
    [ValidateRange(1,65535)][int]$Port = 587,
    [string]$Username = 'apikey',
    [ValidateSet('StartTls','SslOnConnect')][string]$Transport = 'StartTls',
    [switch]$ConfigureSupabase,
    [ValidatePattern('^[a-z0-9]{20}$')][string]$ProjectRef = 'odrsvktbllsfdqyazxsg'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
function Read-SmtpSecret([string]$Variable, [string]$Prompt) {
    $taskValue = [Environment]::GetEnvironmentVariable($Variable)
    if ([string]::IsNullOrWhiteSpace($taskValue)) {
        $taskSecure = Read-Host $Prompt -AsSecureString
        $taskValue = [Net.NetworkCredential]::new('', $taskSecure).Password
    }
    if ([string]::IsNullOrWhiteSpace($taskValue)) { throw 'A required secret was not supplied.' }
    return $taskValue
}
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this script with PowerShell 7 (pwsh).' }
if ([Uri]::CheckHostName($HostName) -eq 'Unknown') { throw 'Use an SMTP hostname, without a URL scheme or path.' }
$taskAddress = [Net.Mail.MailAddress]::new($From)
if ($taskAddress.Address -ne $From -or $From -match '[\r\n]') { throw 'From must be a single verified email address.' }
Push-Location $taskRoot
try {
    $taskPassword = Read-SmtpSecret 'AUTOCHECK_SMTP_PASSWORD' 'API notification SMTP password (SendGrid Mail Send key)'
    $taskSettings = @{
        'Smtp:Host'=$HostName; 'Smtp:Port'=$Port; 'Smtp:Transport'=$Transport
        'Smtp:Username'=$Username; 'Smtp:Password'=$taskPassword; 'Smtp:From'=$From; 'Smtp:SenderName'='AutoCheck'
    }
    $taskSettings | ConvertTo-Json -Compress | dotnet user-secrets set --project AutoCheck.Api
    if ($LASTEXITCODE -ne 0) { throw 'Could not save SMTP user-secrets.' }
    dotnet run --project AutoCheck.Api -c Release -- --check-smtp
    if ($LASTEXITCODE -ne 0) { throw 'SMTP TLS/authentication verification failed; Supabase was not changed.' }
    if ($ConfigureSupabase) {
        # A separate key limits the impact of either application credential being compromised.
        $taskAuthPassword = Read-SmtpSecret 'AUTOCHECK_AUTH_SMTP_PASSWORD' 'Supabase Auth SMTP password (separate Mail Send key)'
        $taskPreviousPasswordEnvironment = $env:Smtp__Password
        try {
            $env:Smtp__Password = $taskAuthPassword
            dotnet run --project AutoCheck.Api -c Release --no-build -- --check-smtp
            if ($LASTEXITCODE -ne 0) { throw 'Auth SMTP TLS/authentication verification failed; Supabase was not changed.' }
        } finally { $env:Smtp__Password = $taskPreviousPasswordEnvironment }
        $taskToken = Read-SmtpSecret 'SUPABASE_ACCESS_TOKEN' 'Supabase Management access token for this project'
        $taskHeaders = @{Authorization='Bearer '+$taskToken}
        $taskUrl = 'https://api.supabase.com/v1/projects/'+$ProjectRef+'/config/auth'
        try {
            $taskPrevious = Invoke-RestMethod -Uri $taskUrl -Headers $taskHeaders -TimeoutSec 15
            if ($taskPrevious.mailer_autoconfirm) { throw 'Email confirmation must remain enabled before configuring SMTP.' }
            $taskPayload = @{
                smtp_host=$HostName; smtp_port=$Port; smtp_user=$Username; smtp_pass=$taskAuthPassword
                smtp_admin_email=$From; smtp_sender_name='AutoCheck'; mailer_autoconfirm=$false
            } | ConvertTo-Json -Compress
            $null = Invoke-RestMethod -Uri $taskUrl -Method Patch -Headers $taskHeaders -ContentType 'application/json' -Body $taskPayload -TimeoutSec 15
            $taskVerified = Invoke-RestMethod -Uri $taskUrl -Headers $taskHeaders -TimeoutSec 15
            if ($taskVerified.smtp_host -ne $HostName -or [int]$taskVerified.smtp_port -ne $Port -or
                $taskVerified.smtp_user -ne $Username -or $taskVerified.smtp_admin_email -ne $From -or $taskVerified.mailer_autoconfirm) {
                throw 'SMTP configuration verification did not match the requested settings.'
            }
            Write-Host 'Supabase SMTP configured and read back. Email confirmation and redirect settings are preserved.'
        } catch {
            # Never include provider responses, tokens or request bodies in console errors.
            throw 'Supabase SMTP configuration failed. Check project access and the SMTP settings in the dashboard.'
        }
    }
    Write-Host 'SMTP TLS and authentication verified. No email was sent by this script.'
} finally {
    $taskPassword=$null; $taskAuthPassword=$null; $taskToken=$null; $taskSettings=$null; $taskPayload=$null
    $taskPrevious=$null; $taskVerified=$null
    if ($taskHeaders) { $taskHeaders.Clear() }
    Pop-Location
}
