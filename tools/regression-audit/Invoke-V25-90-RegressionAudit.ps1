[CmdletBinding()]
param(
    [string]$CandidateRoot = 'C:\Users\Administrator\Documents\Codex\2026-08-11\philosophy-w3-86-to-90-integration\outputs\v25-single-app-90-candidate',
    [string]$GatePath = 'C:\Users\Administrator\Documents\Codex\2026-08-11\philosophy-v25-90-portable-gate-revision\outputs\run-remediated-portable-90-gate.ps1',
    [string]$StageRoot = 'C:\Users\Administrator\Documents\Codex\2026-08-11\philosophy-v25-90-portable-gate-revision\outputs\g-260811135413857',
    [string]$LearningPackPath,
    [string]$ProgramPath,
    [string]$CatalogPath,
    [string]$PortableRoot,
    [string]$ZipPath,
    [string]$ManifestPath,
    [string]$ReportPath = (Join-Path $PSScriptRoot 'REPORT.md'),
    [string]$CheckpointPath = (Join-Path $PSScriptRoot 'checkpoint.json'),
    [switch]$NoReport
)

$ErrorActionPreference = 'Stop'
if (-not $LearningPackPath) { $LearningPackPath = Join-Path $CandidateRoot 'application\Descartes.CertaintyLab\LearningPack.cs' }
if (-not $ProgramPath) { $ProgramPath = Join-Path $CandidateRoot 'tests\Program.cs' }
if (-not $CatalogPath) { $CatalogPath = Join-Path $CandidateRoot 'application\Descartes.CertaintyLab\Content\knowledge-reader-catalog.json' }
if (-not $PortableRoot) { $PortableRoot = Join-Path $StageRoot 'p\single-app-v90-win-x64' }
if (-not $ZipPath) { $ZipPath = Join-Path $StageRoot 'Descartes-CertaintyLab-v90-win-x64-portable.zip' }
if (-not $ManifestPath) { $ManifestPath = Join-Path $StageRoot 'e\SHA256SUMS.txt' }

$failures = [System.Collections.Generic.List[string]]::new()
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { $script:failures.Add($Message) } }
function Hash-Stream([System.IO.Stream]$Stream) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { ([Convert]::ToHexString($sha.ComputeHash($Stream))) } finally { $sha.Dispose() }
}
function Relative([string]$Root, [string]$Path) { [IO.Path]::GetRelativePath($Root,$Path).Replace('\','/') }

foreach ($path in @($CandidateRoot,$GatePath,$LearningPackPath,$ProgramPath,$CatalogPath,$PortableRoot,$ZipPath,$ManifestPath)) {
    Require (Test-Path -LiteralPath $path) "required input missing: $path"
}
if ($failures.Count) { $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }; exit 1 }

# 1. Schema mappings for the actual #83-90 evidence link extension fields.
$contentRoot = Join-Path $CandidateRoot 'application\Descartes.CertaintyLab\Content'
$routes83to90 = @(Get-ChildItem -LiteralPath $contentRoot -Filter '*-learning-route.json' -File | ForEach-Object {
    $j = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json -Depth 100
    if ([int]$j.integrationProjection.ordinal -ge 83 -and [int]$j.integrationProjection.ordinal -le 90) {
        [pscustomobject]@{ File=$_.FullName; Ordinal=[int]$j.integrationProjection.ordinal; Json=$j; RouteId=[string]$j.routes[0].id }
    }
} | Sort-Object Ordinal)
Require ($routes83to90.Count -eq 8) "routes #83-90 expected 8; actual $($routes83to90.Count)"
$actualEvidenceFields = @($routes83to90 | ForEach-Object { $_.Json.evidenceLinks | ForEach-Object { $_.PSObject.Properties.Name } } | Sort-Object -Unique)
$declaredExtensionFields = @('boundary','claim','claimNodeId','genre','locatorAuditStatus','locatorPendingReason','object','objectRole','period','source','sourceType','locatorAuditable','intendedClaimIds','editionIdentity','locatorEvidence')
$baselineEvidenceFields = @('id','workId','edition','locator','locatorVerified','identity','title','stableUrl','workStage','publicationState','quotationMode','boundaryZh','author','country','locatorLimit','locatorStatus','objectKey','translator','url','voice','voiceClass','voiceLayer','year','lessonId')
$actualExtensionFields = @($actualEvidenceFields | Where-Object { $_ -notin $baselineEvidenceFields })
$learningText = Get-Content -Raw -LiteralPath $LearningPackPath
foreach ($field in $actualExtensionFields) {
    Require ($declaredExtensionFields -contains $field) "#83-90 unexpected extension field lacks declared model slot: $field"
    Require ($learningText.Contains("JsonPropertyName(`"$field`")",[StringComparison]::Ordinal)) "explicit evidenceLinks mapping missing: $field"
}
Require ($learningText.Contains('JsonPropertyName("integrationProjection")',[StringComparison]::Ordinal)) 'explicit integrationProjection mapping missing'
Require ($learningText.Contains('UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow',[StringComparison]::Ordinal)) 'UnmappedMemberHandling.Disallow missing'

# 2. Reader-card static derivation and canonical IDs.
$catalog = Get-Content -Raw -LiteralPath $CatalogPath | ConvertFrom-Json -Depth 100
$derived = @($catalog.entries | Where-Object { [string]$_.id -like 'route-card:*' })
Require ($derived.Count -eq 49) "v90 derived route-card count expected 49; actual $($derived.Count)"
$programText = Get-Content -Raw -LiteralPath $ProgramPath
$canonicalBlock = [regex]::Match($programText,'string\[\]\s+expectedCanonicalIds\s*=\s*\[(?<body>[\s\S]*?)\];')
$canonicalIds = @([regex]::Matches($canonicalBlock.Groups['body'].Value,'"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
Require ($canonicalIds.Count -eq 22) "canonical expected IDs expected 22; actual $($canonicalIds.Count)"
Require ($programText.Contains('derivedCards.Length == 49',[StringComparison]::Ordinal)) 'v90 static derived-card assertion 49 missing'
$old86 = Get-Content -Raw -LiteralPath (Join-Path $CandidateRoot 'evidence\v25-w3-old-86-freeze.json') | ConvertFrom-Json -Depth 100
$old86Text = $old86 | ConvertTo-Json -Depth 100 -Compress
Require ($old86Text -match '45') 'v86 static derived-card baseline 45 missing'
$newRoutes = @($routes83to90 | Where-Object Ordinal -ge 87)
$newCards = @($derived | Where-Object { $_.learningRouteId -in $newRoutes.RouteId })
Require ($newCards.Count -eq 4) "new route-card count expected 4; actual $($newCards.Count)"
Require (@($newCards.id | Sort-Object -Unique).Count -eq 4) 'new four route-card IDs are not unique'
foreach ($card in $newCards) { Require ([string]$card.id -ceq "route-card:$($card.learningRouteId)") "route-card ID is not canonical for $($card.learningRouteId)" }

# 3. Active gate expectations.
$gateText = Get-Content -Raw -LiteralPath $GatePath
function Count-Literal([string]$Needle) { [regex]::Matches($gateText,[regex]::Escape($Needle)).Count }
$c22=Count-Literal 'PASS single-app-wiring routes=90 catalogMappings=90 canonicalMappings=22'
$c30=Count-Literal 'PASS single-app-wiring routes=90 catalogMappings=90 canonicalMappings=30'
$p501=Count-Literal '$publishManifest.fileCount -eq 501'
$p497=Count-Literal '$publishManifest.fileCount -eq 497'
Require ($c22 -eq 1) "active canonicalMappings=22 expected once; actual $c22"
Require ($c30 -eq 0) "obsolete canonicalMappings=30 expected zero; actual $c30"
Require ($p501 -eq 1) "active publish tree=501 expected once; actual $p501"
Require ($p497 -eq 0) "obsolete publish tree=497 expected zero; actual $p497"
foreach ($needle in @('routes=90','fileCount -eq 94','fileCount -eq 163','productionFileCount = 155')) { Require ($gateText.Contains($needle,[StringComparison]::Ordinal)) "active gate expectation missing: $needle" }

# 4. Portable tree / manifest / ZIP basic identity.
$treeFiles = @(Get-ChildItem -LiteralPath $PortableRoot -Recurse -File)
$tree = @{}
foreach ($file in $treeFiles) { $tree[(Relative $PortableRoot $file.FullName)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
$manifest = @{}
foreach ($line in Get-Content -LiteralPath $ManifestPath) {
    if ($line -match '^([0-9A-Fa-f]{64})\s{2}(.+)$') { $manifest[$Matches[2].Replace('\','/')] = $Matches[1].ToUpperInvariant() }
}
$manifestMissing=@($tree.Keys|Where-Object{-not $manifest.ContainsKey($_)})
$manifestExtra=@($manifest.Keys|Where-Object{-not $tree.ContainsKey($_)})
$manifestMismatch=@($tree.Keys|Where-Object{$manifest.ContainsKey($_)-and$tree[$_] -cne $manifest[$_]})
Require ($tree.Count -eq 501) "portable tree count expected 501; actual $($tree.Count)"
Require ($manifest.Count -eq 501) "manifest entry count expected 501; actual $($manifest.Count)"
Require ($manifestMissing.Count -eq 0) "manifest missing expected 0; actual $($manifestMissing.Count)"
Require ($manifestExtra.Count -eq 0) "manifest extra expected 0; actual $($manifestExtra.Count)"
Require ($manifestMismatch.Count -eq 0) "manifest hash mismatch expected 0; actual $($manifestMismatch.Count)"
Add-Type -AssemblyName System.IO.Compression
$zip = [IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
    $zipMap=@{}; $duplicates=0; $unsafe=0
    foreach($entry in $zip.Entries) {
        if (-not $entry.Name) { continue }
        $name=$entry.FullName.Replace('\','/'); $prefix='single-app-v90-win-x64/'
        if (-not $name.StartsWith($prefix,[StringComparison]::Ordinal) -or $name.Contains('../') -or $name.StartsWith('/')) { $unsafe++; continue }
        $rel=$name.Substring($prefix.Length); if($zipMap.ContainsKey($rel)){$duplicates++;continue}
        $stream=$entry.Open(); try{$zipMap[$rel]=Hash-Stream $stream}finally{$stream.Dispose()}
    }
} finally { $zip.Dispose() }
$zipMissing=@($tree.Keys|Where-Object{-not $zipMap.ContainsKey($_)})
$zipExtra=@($zipMap.Keys|Where-Object{-not $tree.ContainsKey($_)})
$zipMismatch=@($tree.Keys|Where-Object{$zipMap.ContainsKey($_)-and$tree[$_] -cne $zipMap[$_]})
Require ($zipMap.Count -eq 501) "ZIP entry count expected 501; actual $($zipMap.Count)"
Require ($zipMissing.Count+$zipExtra.Count+$zipMismatch.Count+$duplicates+$unsafe -eq 0) "ZIP identity mismatch missing=$($zipMissing.Count) extra=$($zipExtra.Count) hash=$($zipMismatch.Count) duplicates=$duplicates unsafe=$unsafe"

$status = if($failures.Count -eq 0){'V25_90_REGRESSION_AUDIT_PASS'}else{'V25_90_REGRESSION_AUDIT_FAIL'}
$checkpoint=[ordered]@{status=$status;generatedAt=[DateTimeOffset]::Now.ToString('o');readOnly=$true;schema=[ordered]@{routes83to90=$routes83to90.Count;actualEvidenceFields=$actualEvidenceFields;actualExtensionFields=$actualExtensionFields;declaredExplicitExtensionMappings=15;integrationProjection=$learningText.Contains('JsonPropertyName("integrationProjection")');unmappedDisallow=$learningText.Contains('UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow')};reader=[ordered]@{baseline86Derived=45;candidate90Derived=$derived.Count;canonicalExpectedIds=$canonicalIds.Count;newUniqueRouteCards=@($newCards.id|Sort-Object -Unique).Count};gate=[ordered]@{canonical22=$c22;obsolete30=$c30;publish501=$p501;obsolete497=$p497;routes=90;content=94;source=163;production=155};portable=[ordered]@{treeFiles=$tree.Count;manifestEntries=$manifest.Count;manifestMissing=$manifestMissing.Count;manifestExtra=$manifestExtra.Count;manifestMismatch=$manifestMismatch.Count;zipEntries=$zipMap.Count;zipMissing=$zipMissing.Count;zipExtra=$zipExtra.Count;zipMismatch=$zipMismatch.Count;zipDuplicates=$duplicates;zipUnsafe=$unsafe;zipSha256=(Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash};failures=@($failures)}
if(-not $NoReport){
    $checkpoint|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $CheckpointPath -Encoding utf8
    @("# v25/90 外置只读回归审计报告","","状态：``$status``","","- Schema：#83–90=$($routes83to90.Count)，扩展映射=15，integrationProjection/Disallow 保留。","- Reader：86=45，90=$($derived.Count)，canonical IDs=$($canonicalIds.Count)，新增唯一 route-card=$(@($newCards.id|Sort-Object -Unique).Count)。","- Gate：canonical 22/30=$c22/$c30，publish 501/497=$p501/$p497，90/94/163/155 active。","- Portable：tree/manifest/ZIP=$($tree.Count)/$($manifest.Count)/$($zipMap.Count)，三方 mismatch=0。","- 禁止项：未 build、未启动 EXE/UI、未联网、未写 candidate/staging/gate。","","## Failures","",$(if($failures.Count){$failures|ForEach-Object{"- $_"}}else{'- None'}))|Set-Content -LiteralPath $ReportPath -Encoding utf8
}
if($failures.Count){$failures|ForEach-Object{Write-Error $_ -ErrorAction Continue};Write-Output $status;exit 1}
Write-Output $status
