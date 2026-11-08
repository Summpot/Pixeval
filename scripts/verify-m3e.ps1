[CmdletBinding()]
param(
    [string] $Dotnet = 'dotnet',
    [string] $OutputDirectory = '',
    [string] $ExtensionFixture = '',
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$previousFixture = $env:PIXEVAL_TEST_EXTENSION_FIXTURE
$previousArtifacts = $env:PIXEVAL_TEST_ARTIFACTS
Push-Location $repoRoot
try {
    # Explicit opt-in only; do not inherit a fixture from an unrelated terminal session.
    $env:PIXEVAL_TEST_EXTENSION_FIXTURE = $null
    if ($ExtensionFixture) {
        $ExtensionFixture = (Resolve-Path -LiteralPath $ExtensionFixture).Path
        if (!(Test-Path -LiteralPath $ExtensionFixture -PathType Leaf)) {
            throw 'ExtensionFixture must be a native library file.'
        }
        $env:PIXEVAL_TEST_EXTENSION_FIXTURE = $ExtensionFixture
    }
    # Let the CLI apply global.json, including its rollForward policy.
    $actualSdk = & $Dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not resolve an SDK using global.json. Install an SDK allowed by its version and rollForward policy.'
    }
    Write-Host "Using SDK $actualSdk (resolved by global.json)."

    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = Join-Path $repoRoot ('artifacts/m3e/' + (Get-Date -Format 'yyyy-MM-dd-HHmmss'))
    }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if ((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory -Force)) {
        throw "Evidence directory must be empty: $OutputDirectory"
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $env:PIXEVAL_TEST_ARTIFACTS = $OutputDirectory
    if ($ExtensionFixture) {
        Get-FileHash -LiteralPath $ExtensionFixture -Algorithm SHA256 |
            ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'extension-fixture.json')
    }

    function Invoke-ValidationCommand([string] $Name, [string[]] $Arguments) {
        $logPath = Join-Path $OutputDirectory ($Name + '.log')
        Write-Host "Running $Name; evidence: $logPath"
        & $Dotnet @Arguments *> $logPath
        $code = $LASTEXITCODE
        Get-Content -LiteralPath $logPath -Tail 20 | Write-Host
        if ($code -ne 0) {
            throw "$Name failed with exit code $code. See $logPath"
        }
    }

    & git rev-parse HEAD | Set-Content (Join-Path $OutputDirectory 'commit.txt')
    & git status --short | Set-Content (Join-Path $OutputDirectory 'worktree.txt')
    & git submodule status --recursive | Set-Content (Join-Path $OutputDirectory 'submodules.txt')
    & $Dotnet --info *> (Join-Path $OutputDirectory 'dotnet-info.log')

    if (!$SkipBuild) {
        Invoke-ValidationCommand 'desktop-debug-build' @('build', 'src/Pixeval.Desktop/Pixeval.Desktop.csproj', '-c', 'Debug', '--nologo')
        Invoke-ValidationCommand 'tests-release-build' @('build', 'src/Pixeval.Tests/Pixeval.Tests.csproj', '-c', 'Release', '--nologo')
    }

    # project.assets distinguishes source projects from packages even when both have a PackageId.
    $assets = Get-Content src/Pixeval/obj/project.assets.json -Raw | ConvertFrom-Json -AsHashtable
    $dependencies = foreach ($name in ($assets.libraries.Keys | Sort-Object)) {
        [ordered]@{ name = $name; type = $assets.libraries[$name].type }
    }
    $dependencies | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $OutputDirectory 'dependencies.json')

    # Direct MTP execution avoids dotnet-test named-pipe orchestration, which is unavailable
    # in some Windows automation hosts. The same assembly and tests run on both platforms.
    Invoke-ValidationCommand 'tests-release' @(
        'src/Pixeval.Tests/bin/Release/net10.0/Pixeval.Tests.dll',
        '--no-ansi', '--progress', 'off', '--report-trx', '--results-directory', $OutputDirectory
    )
    Write-Host "Validation finished. Inspect TRX skipped tests and platform/UI gaps before marking a migration step accepted."
}
finally {
    $env:PIXEVAL_TEST_EXTENSION_FIXTURE = $previousFixture
    $env:PIXEVAL_TEST_ARTIFACTS = $previousArtifacts
    Pop-Location
}
