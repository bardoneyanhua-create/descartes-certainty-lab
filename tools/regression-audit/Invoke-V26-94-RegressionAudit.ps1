[CmdletBinding()]
param(
    [string]$CandidateRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$EvidenceRoot,
    [string]$ReportPath = (Join-Path $PSScriptRoot 'REPORT.md'),
    [string]$CheckpointPath = (Join-Path $PSScriptRoot 'checkpoint.json'),
    [switch]$NoReport
)

$ErrorActionPreference = 'Stop'
$failures = [Collections.Generic.List[string]]::new()
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { $script:failures.Add($Message) } }

if (-not (Test-Path -LiteralPath $CandidateRoot -PathType Container)) {
    'V26_94_REGRESSION_AUDIT_FAIL CANDIDATE_ROOT_REQUIRED'
    exit 1
}
$CandidateRoot = (Resolve-Path -LiteralPath $CandidateRoot).Path

$appRoot = Join-Path $CandidateRoot 'application\Descartes.CertaintyLab'
$contentRoot = Join-Path $appRoot 'Content'
$learningPackPath = Join-Path $appRoot 'LearningPack.cs'
$programPath = Join-Path $CandidateRoot 'tests\Program.cs'
$catalogPath = Join-Path $contentRoot 'knowledge-reader-catalog.json'
foreach ($path in @($contentRoot,$learningPackPath,$programPath,$catalogPath)) {
    Require (Test-Path -LiteralPath $path) "required source input missing: $path"
}
if ($failures.Count) { $failures | ForEach-Object { "V26_94_REGRESSION_AUDIT_FAIL $_" }; exit 1 }

# Source checks available to every clean clone.
$jsonFailures = 0
foreach ($file in Get-ChildItem -LiteralPath $contentRoot -Filter '*.json' -File) {
    try { Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json -Depth 100 | Out-Null }
    catch { $jsonFailures++ }
}
Require ($jsonFailures -eq 0) "content JSON parse failures expected 0; actual $jsonFailures"

$routes83to94 = @(Get-ChildItem -LiteralPath $contentRoot -Filter '*-learning-route.json' -File | ForEach-Object {
    $json = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json -Depth 100
    if ($null -ne $json.integrationProjection -and [int]$json.integrationProjection.ordinal -ge 83 -and [int]$json.integrationProjection.ordinal -le 94) {
        [pscustomobject]@{ File=$_.FullName; Ordinal=[int]$json.integrationProjection.ordinal; Json=$json; RouteId=[string]$json.routes[0].id }
    }
} | Sort-Object Ordinal)
Require ($routes83to94.Count -eq 12) "routes #83-94 expected 12; actual $($routes83to94.Count)"

$baselineEvidenceFields = @('id','workId','edition','locator','locatorVerified','identity','title','stableUrl','workStage','publicationState','quotationMode','boundaryZh','author','country','locatorLimit','locatorStatus','objectKey','translator','url','voice','voiceClass','voiceLayer','year','lessonId')
$expectedExtensions = @('boundary','claim','claimNodeId','genre','locatorAuditStatus','locatorPendingReason','object','objectRole','period','source','sourceType','locatorAuditable','intendedClaimIds','editionIdentity','locatorEvidence')
$actualFields = @($routes83to94 | ForEach-Object { $_.Json.evidenceLinks | ForEach-Object { $_.PSObject.Properties.Name } } | Sort-Object -Unique)
$actualExtensions = @($actualFields | Where-Object { $_ -notin $baselineEvidenceFields })
$learningText = [IO.File]::ReadAllText($learningPackPath)
foreach ($field in $actualExtensions) {
    Require ($expectedExtensions -contains $field) "unexpected evidence extension field: $field"
    Require ($learningText.Contains("JsonPropertyName(`"$field`")",[StringComparison]::Ordinal)) "explicit evidenceLinks mapping missing: $field"
}
Require ($learningText.Contains('JsonPropertyName("integrationProjection")',[StringComparison]::Ordinal)) 'explicit integrationProjection mapping missing'
Require ($learningText.Contains('UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow',[StringComparison]::Ordinal)) 'UnmappedMemberHandling.Disallow missing'

$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json -Depth 100
$derived = @($catalog.entries | Where-Object { [string]$_.id -like 'route-card:*' })
Require ($derived.Count -eq 53) "v94 derived route-card count expected 53; actual $($derived.Count)"
$programText = [IO.File]::ReadAllText($programPath)
$canonicalBlock = [regex]::Match($programText,'string\[\]\s+expectedCanonicalIds\s*=\s*\[(?<body>[\s\S]*?)\];')
$canonicalIds = @([regex]::Matches($canonicalBlock.Groups['body'].Value,'"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
Require ($canonicalIds.Count -eq 22) "canonical expected IDs expected 22; actual $($canonicalIds.Count)"
Require ($programText.Contains('derivedCards.Length == 53',[StringComparison]::Ordinal)) 'v94 static derived-card assertion 53 missing'

$lockFiles = @(
    (Join-Path $appRoot 'packages.lock.json'),
    (Join-Path $CandidateRoot 'tests\packages.lock.json')
)
foreach ($lock in $lockFiles) { Require (Test-Path -LiteralPath $lock -PathType Leaf) "locked dependency file missing: $lock" }

# Optional historical artifact checks. These run only when the caller explicitly supplies evidence.
$evidenceChecked = $false
if ($EvidenceRoot) {
    if (-not (Test-Path -LiteralPath $EvidenceRoot -PathType Container)) {
        'V26_94_REGRESSION_AUDIT_FAIL EVIDENCE_ROOT_REQUIRED'
        exit 1
    }
    $EvidenceRoot = (Resolve-Path -LiteralPath $EvidenceRoot).Path
    $gatePath = Join-Path $EvidenceRoot 'run-remediated-portable-94-gate.ps1'
    $portableRoot = Join-Path $EvidenceRoot 'p\single-app-v94-win-x64'
    $zipPath = Join-Path $EvidenceRoot 'Descartes-CertaintyLab-v94-win-x64-portable.zip'
    $manifestPath = Join-Path $EvidenceRoot 'e\SHA256SUMS.txt'
    foreach ($path in @($gatePath,$portableRoot,$zipPath,$manifestPath)) { Require (Test-Path -LiteralPath $path) "explicit evidence input missing: $path" }
    if ($failures.Count -eq 0) {
        $gateText = [IO.File]::ReadAllText($gatePath)
        Require ([regex]::Matches($gateText,[regex]::Escape('PASS single-app-wiring routes=94 catalogMappings=94 canonicalMappings=22')).Count -eq 1) 'active canonicalMappings=22 signature mismatch'
        Require ([regex]::Matches($gateText,[regex]::Escape('$publishManifest.fileCount -eq 505')).Count -eq 1) 'active publish tree=505 signature mismatch'
        $treeFiles = @(Get-ChildItem -LiteralPath $portableRoot -Recurse -File)
        $manifestLines = @(Get-Content -LiteralPath $manifestPath | Where-Object { $_ -match '^[0-9A-Fa-f]{64}\s{2}.+' })
        Require ($treeFiles.Count -eq 505) "portable tree count expected 505; actual $($treeFiles.Count)"
        Require ($manifestLines.Count -eq 505) "manifest count expected 505; actual $($manifestLines.Count)"
        Add-Type -AssemblyName System.IO.Compression
        $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try { $zipFiles = @($zip.Entries | Where-Object Name) } finally { $zip.Dispose() }
        Require ($zipFiles.Count -eq 505) "ZIP entry count expected 505; actual $($zipFiles.Count)"
        $evidenceChecked = $true
    }
}

$status = if ($failures.Count -eq 0) { 'V26_94_REGRESSION_AUDIT_PASS' } else { 'V26_94_REGRESSION_AUDIT_FAIL' }
$checkpoint = [ordered]@{
    status=$status
    readOnly=$true
    source=[ordered]@{ jsonFailures=$jsonFailures; routes83to94=$routes83to94.Count; evidenceExtensionMappings=$actualExtensions.Count; derivedCards=$derived.Count; canonicalIds=$canonicalIds.Count; lockFiles=$lockFiles.Count }
    historicalEvidence=[ordered]@{ requested=[bool]$EvidenceRoot; checked=$evidenceChecked }
    failures=@($failures)
}
if (-not $NoReport) {
    $checkpoint | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $CheckpointPath -Encoding utf8
    @(
        '# v26/94 回归审计报告','',"状态：``$status``",'',
        "- Source：JSON failures=$jsonFailures；routes #83–94=$($routes83to94.Count)；derived/canonical=$($derived.Count)/$($canonicalIds.Count)。",
        "- Historical artifact evidence：requested=$([bool]$EvidenceRoot)，checked=$evidenceChecked。",
        '- 默认模式可在干净 clone 中运行；portable 历史证据只在显式提供时检查。','',
        '## Failures','',$(if ($failures.Count) { $failures | ForEach-Object { "- $_" } } else { '- None' })
    ) | Set-Content -LiteralPath $ReportPath -Encoding utf8
}
if ($failures.Count) { $failures | ForEach-Object { "V26_94_REGRESSION_AUDIT_FAIL $_" }; exit 1 }
$status
