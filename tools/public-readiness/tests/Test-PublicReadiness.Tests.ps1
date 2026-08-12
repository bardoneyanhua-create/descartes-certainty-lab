$ErrorActionPreference = 'Stop'

$entry = Join-Path (Split-Path -Parent $PSScriptRoot) 'Test-PublicReadiness.ps1'
$work = Join-Path $env:TEMP ('philosophy-public-readiness-' + [guid]::NewGuid().ToString('N'))

function New-Fixture([string]$Name) {
    $root = Join-Path $work $Name
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    foreach ($path in @('README.md','LICENSE','LICENSE-CONTENT.md','THIRD-PARTY-NOTICES.md','CONTRIBUTING.md','SECURITY.md','docs\ACCESSIBILITY.md','docs\RIGHTS-AND-LICENSING.md')) {
        $full = Join-Path $root $path
        New-Item -ItemType Directory -Path (Split-Path -Parent $full) -Force | Out-Null
        [IO.File]::WriteAllText($full, "safe public text`n", [Text.UTF8Encoding]::new($false))
    }
    git -C $root init -q
    git -C $root config user.name fixture
    git -C $root config user.email fixture@example.invalid
    git -C $root add --all
    return $root
}

function Invoke-Check([string]$Root) {
    $output = & pwsh -NoProfile -File $entry -RepositoryRoot $Root 2>&1
    [pscustomobject]@{ ExitCode=$LASTEXITCODE; Output=($output -join "`n") }
}

try {
    $clean = New-Fixture 'clean'
    $cleanResult = Invoke-Check $clean
    if ($cleanResult.ExitCode -ne 0 -or $cleanResult.Output -notmatch 'PUBLIC_READINESS_PASS') { throw "clean fixture did not pass: $($cleanResult.Output)" }

    $absolute = New-Fixture 'absolute'
    [IO.File]::WriteAllText((Join-Path $absolute 'local.txt'), 'C:\Users\Example\private', [Text.UTF8Encoding]::new($false))
    git -C $absolute add --all
    $absoluteResult = Invoke-Check $absolute
    if ($absoluteResult.ExitCode -eq 0 -or $absoluteResult.Output -notmatch 'LOCAL_ABSOLUTE_PATH') { throw 'absolute path fixture did not fail safely' }

    $archive = New-Fixture 'archive'
    [IO.File]::WriteAllBytes((Join-Path $archive 'release.zip'), [byte[]](1,2,3))
    git -C $archive add --all
    $archiveResult = Invoke-Check $archive
    if ($archiveResult.ExitCode -eq 0 -or $archiveResult.Output -notmatch 'FORBIDDEN_TRACKED_ARTIFACT') { throw 'archive fixture did not fail safely' }

    $secret = New-Fixture 'secret'
    [IO.File]::WriteAllText((Join-Path $secret 'settings.txt'), 'api_key = sk-example-not-a-real-key-1234567890', [Text.UTF8Encoding]::new($false))
    git -C $secret add --all
    $secretResult = Invoke-Check $secret
    if ($secretResult.ExitCode -eq 0 -or $secretResult.Output -notmatch 'POSSIBLE_SECRET') { throw 'secret fixture did not fail safely' }

    $missing = New-Fixture 'missing'
    Remove-Item -LiteralPath (Join-Path $missing 'SECURITY.md')
    git -C $missing add --all
    $missingResult = Invoke-Check $missing
    if ($missingResult.ExitCode -eq 0 -or $missingResult.Output -notmatch 'REQUIRED_PUBLIC_FILE_MISSING') { throw 'missing governance fixture did not fail safely' }

    $missingLicense = New-Fixture 'missing-license'
    Remove-Item -LiteralPath (Join-Path $missingLicense 'LICENSE')
    git -C $missingLicense add --all
    $missingLicenseResult = Invoke-Check $missingLicense
    if ($missingLicenseResult.ExitCode -eq 0 -or $missingLicenseResult.Output -notmatch 'REQUIRED_PUBLIC_FILE_MISSING') { throw 'missing license fixture did not fail safely' }

    'PUBLIC_READINESS_TESTS_PASS'
    exit 0
}
finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
