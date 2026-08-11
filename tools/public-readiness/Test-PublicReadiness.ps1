[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$failures = [Collections.Generic.List[string]]::new()

function Add-Failure([string]$Reason, [string]$RelativePath) {
    $failures.Add("${Reason}:$($RelativePath.Replace('\','/'))")
}

$required = @(
    'README.md',
    'CONTRIBUTING.md',
    'SECURITY.md',
    'docs\ACCESSIBILITY.md',
    'docs\RIGHTS-AND-LICENSING.md'
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        Add-Failure 'REQUIRED_PUBLIC_FILE_MISSING' $relative
    }
}

$tracked = @(& git -C $root ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate Git tracked files.' }

$forbiddenExtensions = @('.zip','.binlog','.pfx','.p12','.pem','.key')
$forbiddenSegments = @('/bin/','/obj/','/outputs/','/work/','/evidence/')
$textExtensions = @('.md','.txt','.ps1','.psm1','.cs','.csproj','.json','.yml','.yaml','.xml','.props','.targets','.gitignore','.gitattributes')

foreach ($relativeNative in $tracked) {
    $relative = $relativeNative.Replace('\','/')
    $lower = $relative.ToLowerInvariant()
    $extension = [IO.Path]::GetExtension($relative).ToLowerInvariant()
    if ($forbiddenExtensions -contains $extension -or @($forbiddenSegments | Where-Object { "/$lower" -like "*$_*" }).Count -gt 0) {
        Add-Failure 'FORBIDDEN_TRACKED_ARTIFACT' $relative
        continue
    }

    if ($relative -like 'tools/public-readiness/tests/*') { continue }
    if ($extension -notin $textExtensions -and [IO.Path]::GetFileName($relative) -notin @('.gitignore','.gitattributes')) { continue }

    $full = Join-Path $root $relativeNative
    $text = [IO.File]::ReadAllText($full)
    if ($text -match '(?i)[A-Z]:\\Users\\[^\\\r\n]+') { Add-Failure 'LOCAL_ABSOLUTE_PATH' $relative }
    if ($text -match '(?i)(api[_-]?key|secret|token|password)\s*[:=]\s*["'']?sk-[A-Za-z0-9_-]{16,}') {
        Add-Failure 'POSSIBLE_SECRET' $relative
    }
}

if ($failures.Count -gt 0) {
    $failures | Sort-Object -Unique | ForEach-Object { "PUBLIC_READINESS_FAIL $_" }
    exit 1
}

'PUBLIC_READINESS_PASS'
