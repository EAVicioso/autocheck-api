# AutoCheck / SRS 1.1. Requires the repository owner's GitHub CLI session.
[CmdletBinding()]
param([string] $Repository = 'EAVicioso/autocheck-api')
$ErrorActionPreference = 'Stop'
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository name.' }
$repoMetadata = gh api "repos/$Repository" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or -not $repoMetadata.permissions.admin) { throw 'GitHub administration permission is required to activate the reviewed ruleset.' }
$ruleFile = Join-Path $PSScriptRoot '../.github/rulesets/main.json'
$ruleName = (Get-Content -LiteralPath $ruleFile -Raw | ConvertFrom-Json).name
$existing = @(gh api "repos/$Repository/rulesets" | ConvertFrom-Json) | Where-Object name -eq $ruleName
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect current repository rulesets.' }
if (@($existing).Count -gt 1) { throw 'Multiple matching rulesets found; inspect them before changing protection.' }
if ($existing) { gh api --method PUT "repos/$Repository/rulesets/$($existing.id)" --input $ruleFile }
else { gh api --method POST "repos/$Repository/rulesets" --input $ruleFile }
if ($LASTEXITCODE -ne 0) { throw 'GitHub did not activate the ruleset.' }
gh api "repos/$Repository/rules/branches/main"
