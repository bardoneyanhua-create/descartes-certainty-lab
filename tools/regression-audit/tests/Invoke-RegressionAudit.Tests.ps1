[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$audit = Join-Path $root 'Invoke-V25-90-RegressionAudit.ps1'
$work = Join-Path (Split-Path -Parent $root) 'work\fixtures'
$candidate = 'C:\Users\Administrator\Documents\Codex\2026-08-11\philosophy-w3-86-to-90-integration\outputs\v25-single-app-90-candidate'
$gate = 'C:\Users\Administrator\Documents\Codex\2026-08-11\philosophy-v25-90-portable-gate-revision\outputs\run-remediated-portable-90-gate.ps1'
$stage = 'C:\Users\Administrator\Documents\Codex\2026-08-11\philosophy-v25-90-portable-gate-revision\outputs\g-260811135413857'

New-Item -ItemType Directory -Force -Path $work | Out-Null
$evidence = [System.Collections.Generic.List[string]]::new()

function Invoke-ExpectedRed([string]$Name, [string]$Expected, [hashtable]$Overrides) {
    $log = Join-Path $work "$Name.log.txt"
    $args = @('-NoProfile','-File',$audit,'-NoReport')
    foreach ($key in $Overrides.Keys) { $args += "-$key"; $args += $Overrides[$key] }
    $text = (& pwsh @args 2>&1 | Out-String).Trim()
    $code = $LASTEXITCODE
    Set-Content -LiteralPath $log -Value $text -Encoding utf8
    if ($code -eq 0 -or $text -notmatch [regex]::Escape($Expected)) {
        throw "Expected RED '$Name' containing '$Expected'; exit=$code output=$text"
    }
    $evidence.Add("RED $Name exit=$code expected=$Expected")
}

# RED 1: remove an actually required #83-90 evidenceLinks mapping.
$learning = Join-Path $candidate 'application\Descartes.CertaintyLab\LearningPack.cs'
$badLearning = Join-Path $work 'LearningPack.missing-genre.cs'
(Get-Content -Raw $learning).Replace('[property: JsonPropertyName("genre")]', '[property: JsonPropertyName("genre_old")]') |
    Set-Content -LiteralPath $badLearning -Encoding utf8
Invoke-ExpectedRed 'schema-mapping' 'explicit evidenceLinks mapping missing: genre' @{ LearningPackPath = $badLearning }

# RED 2: restore the old reader-card count by removing the four new derived cards.
$catalog = Join-Path $candidate 'application\Descartes.CertaintyLab\Content\knowledge-reader-catalog.json'
$badCatalog = Join-Path $work 'knowledge-reader-catalog.derived45.json'
$json = Get-Content -Raw $catalog | ConvertFrom-Json -Depth 100
$newRouteIds = @('emilie-du-chatelet-hypotheses-force-happiness','judith-butler-performativity-recognition-precarity','enrique-dussel-exteriority-liberation-transmodernity','kwasi-wiredu-conceptual-decolonization-consensus')
$json.entries = @($json.entries | Where-Object { $_.learningRouteId -notin $newRouteIds })
$json | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $badCatalog -Encoding utf8
Invoke-ExpectedRed 'reader-card-45' 'v90 derived route-card count expected 49; actual 45' @{ CatalogPath = $badCatalog }

# RED 3: restore both obsolete active gate expectations.
$badGate = Join-Path $work 'run-gate.old-30-497.ps1'
(Get-Content -Raw $gate).Replace('canonicalMappings=22','canonicalMappings=30').Replace('fileCount -eq 501','fileCount -eq 497') |
    Set-Content -LiteralPath $badGate -Encoding utf8
Invoke-ExpectedRed 'gate-old-expectations' 'active canonicalMappings=22 expected once; actual 0' @{ GatePath = $badGate }

# RED 4: corrupt one manifest digest while leaving the portable tree and ZIP untouched.
$manifest = Join-Path $stage 'e\SHA256SUMS.txt'
$badManifest = Join-Path $work 'SHA256SUMS.bad.txt'
$lines = @(Get-Content $manifest)
$lines[0] = ('0' * 64) + $lines[0].Substring(64)
$lines | Set-Content -LiteralPath $badManifest -Encoding utf8
Invoke-ExpectedRed 'portable-identity' 'manifest hash mismatch expected 0; actual 1' @{ ManifestPath = $badManifest }

$greenLog = Join-Path $work 'final-green.log.txt'
$greenText = (& pwsh -NoProfile -File $audit -NoReport 2>&1 | Out-String).Trim()
$greenCode = $LASTEXITCODE
Set-Content -LiteralPath $greenLog -Value $greenText -Encoding utf8
if ($greenCode -ne 0 -or $greenText -notmatch 'V25_90_REGRESSION_AUDIT_PASS') {
    throw "Expected final GREEN; exit=$greenCode output=$greenText"
}
$evidence.Add("GREEN final-state exit=0 status=V25_90_REGRESSION_AUDIT_PASS")
$evidence | Set-Content -LiteralPath (Join-Path $root 'RED-GREEN-EVIDENCE.txt') -Encoding utf8
$evidence
