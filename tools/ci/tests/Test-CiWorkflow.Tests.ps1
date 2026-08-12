[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$workflowPath = Join-Path $repoRoot '.github\workflows\ci.yml'

if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) {
    'CI_WORKFLOW_MISSING'
    exit 1
}

$text = [IO.File]::ReadAllText($workflowPath)
$required = @(
    'pull_request:',
    'workflow_dispatch:',
    'permissions:',
    'contents: read',
    'runs-on: windows-latest',
    'actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803 # v6.1.0',
    'actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5.4.0',
    "dotnet-version: '10.0.302'",
    'Test-PublicReadiness.Tests.ps1',
    'Test-PublicReadiness.ps1',
    'Invoke-RegressionAudit.Tests.ps1',
    'Invoke-V25-90-RegressionAudit.ps1',
    '--locked-mode',
    'dotnet build',
    'dotnet run'
)
foreach ($literal in $required) {
    if (-not $text.Contains($literal, [StringComparison]::Ordinal)) {
        throw "CI required literal missing: $literal"
    }
}

$forbidden = @('permissions: write-all', 'contents: write', 'actions/upload-artifact', 'gh release', 'dotnet publish')
foreach ($literal in $forbidden) {
    if ($text.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
        throw "CI forbidden literal present: $literal"
    }
}

if ([regex]::Matches($text, 'runs-on:\s*windows-latest').Count -ne 2) {
    throw 'CI must contain exactly two Windows jobs'
}

'CI_WORKFLOW_TESTS_PASS'
