[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [string] $Dotnet = 'dotnet',
    [string] $Generator = '',
    [string] $Architecture = ''
)

$ErrorActionPreference = 'Stop'
$revision = '20c900a76cb427ad1dac38ed3f64463be2fd496b'
$repository = 'https://github.com/Pixeval/Pixeval.Extensions.git'
$repoRoot = Split-Path $PSScriptRoot -Parent
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Use a new fixture directory: $OutputDirectory"
}
$dotnetPath = (Get-Command $Dotnet -CommandType Application | Select-Object -First 1).Source
$previousPath = $env:PATH
$previousRoot = $env:DOTNET_ROOT
$previousRollForward = $env:DOTNET_ROLL_FORWARD
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Push-Location $repoRoot
try {
    # Match verify-m3e.ps1: global.json chooses the SDK, including rollForward.
    $actualSdk = & $dotnetPath --version
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not resolve an SDK using global.json. Install an SDK allowed by its version and rollForward policy.'
    }
    $env:DOTNET_ROOT = Split-Path $dotnetPath -Parent
    $env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
    # The fixture's IDL generator targets net8.0; permit it to use an installed
    # compatible runtime without installing another global runtime.
    $env:DOTNET_ROLL_FORWARD = 'Major'

    function Invoke-FixtureCommand([string] $Name, [string] $Command, [string[]] $Arguments) {
        $log = Join-Path $OutputDirectory ($Name + '.log')
        & $Command @Arguments *> $log
        if ($LASTEXITCODE -ne 0) {
            Get-Content -LiteralPath $log -Tail 25 | Write-Host
            throw "$Name failed; see $log"
        }
    }

    $source = Join-Path $OutputDirectory 'source'
    Invoke-FixtureCommand 'clone' 'git' @('clone', '--no-checkout', $repository, $source)
    Invoke-FixtureCommand 'checkout' 'git' @('-C', $source, 'checkout', '--detach', $revision)
    $resolvedRevision = & git -C $source rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $resolvedRevision -ne $revision) {
        throw 'The fixture source revision does not match the pin.'
    }

    $build = Join-Path $OutputDirectory 'build'
    $configureArguments = @('-S', (Join-Path $source 'src/cpp'), '-B', $build, '-DCMAKE_BUILD_TYPE=Release')
    if ($Generator) { $configureArguments += @('-G', $Generator) }
    if ($Architecture) { $configureArguments += @('-A', $Architecture) }
    Invoke-FixtureCommand 'configure' 'cmake' $configureArguments
    Invoke-FixtureCommand 'build' 'cmake' @('--build', $build, '--config', 'Release', '--target', 'Pixeval.Extensions.CppDemo')

    $libraryName = if ($IsWindows) { 'Pixeval.Extensions.CppDemo.dll' }
        elseif ($IsMacOS) { 'libPixeval.Extensions.CppDemo.dylib' }
        else { 'libPixeval.Extensions.CppDemo.so' }
    $libraries = @(Get-ChildItem -LiteralPath (Join-Path $source 'src/cpp/Pixeval.Extensions.Cpp.Demo/bin') -Recurse -File |
        Where-Object Name -EQ $libraryName)
    if ($libraries.Count -ne 1) { throw "Expected exactly one $libraryName; found $($libraries.Count)." }
    $manifest = [ordered]@{
        repository = $repository
        revision = $revision
        sdk = $actualSdk
        generator = $Generator
        architecture = $Architecture
        library = $libraries[0].FullName
        sha256 = (Get-FileHash -LiteralPath $libraries[0].FullName -Algorithm SHA256).Hash
    }
    $manifest | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'fixture.json')
    Write-Host "Native fixture ready: $($manifest.library)"
}
finally {
    $env:PATH = $previousPath
    $env:DOTNET_ROOT = $previousRoot
    $env:DOTNET_ROLL_FORWARD = $previousRollForward
    Pop-Location
}
