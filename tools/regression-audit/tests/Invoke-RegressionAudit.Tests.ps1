[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$auditRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = (Resolve-Path (Join-Path $auditRoot '..\..')).Path
$entry = Join-Path $auditRoot 'Invoke-V26-94-RegressionAudit.ps1'
$source = [IO.File]::ReadAllText($entry)

if ($source -match '(?i)[A-Z]:\\Users\\') { throw 'audit entry contains a local user path' }

$green = & pwsh -NoProfile -File $entry -CandidateRoot $repositoryRoot -NoReport 2>&1
if ($LASTEXITCODE -ne 0 -or ($green -join "`n") -notmatch 'V26_94_REGRESSION_AUDIT_PASS') {
    throw "portable source audit did not pass: $($green -join "`n")"
}

$badEvidence = Join-Path $env:TEMP ('missing-v26-evidence-' + [guid]::NewGuid().ToString('N'))
$red = & pwsh -NoProfile -File $entry -CandidateRoot $repositoryRoot -EvidenceRoot $badEvidence -NoReport 2>&1
if ($LASTEXITCODE -eq 0 -or ($red -join "`n") -notmatch 'EVIDENCE_ROOT_REQUIRED') {
    throw "invalid explicit evidence root did not fail safely: $($red -join "`n")"
}

'REGRESSION_AUDIT_TESTS_PASS'
exit 0
