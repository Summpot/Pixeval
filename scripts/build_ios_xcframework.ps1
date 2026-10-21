param(
    [switch]$CheckOnly = $false
)

Write-Host "==> iOS XCFramework Packaging Pipeline" -ForegroundColor Cyan

# Check if running on macOS or cross-dispatch environment
$runningOnMac = $false
if ($PSVersionTable.PSEdition -and $PSVersionTable.OS -like "*Darwin*") {
    $runningOnMac = $true
}

if (-not $runningOnMac) {
    Write-Host "NOTE: Physical XCFramework generation (xcodebuild/lipo) requires a macOS host with Xcode installed." -ForegroundColor Yellow
    Write-Host "On Windows, this script validates pipeline assets and structural configuration." -ForegroundColor Yellow

    # Validate pipeline components
    $checks = @(
        "scripts/build_ios_xcframework.sh",
        "ios/Frameworks/PixevalFlutterBridge/include/PixevalFlutterBridge.h",
        "ios/Frameworks/PixevalFlutterBridge/include/module.modulemap",
        "ios/PixevalFlutterBridge.podspec",
        "ios/Podfile",
        "scripts/verify-ios-pipeline.py"
    )

    $allPresent = $true
    foreach ($chk in $checks) {
        if (Test-Path $chk) {
            Write-Host "  [OK] $chk" -ForegroundColor Green
        } else {
            Write-Host "  [MISSING] $chk" -ForegroundColor Red
            $allPresent = $false
        }
    }

    if (-not $allPresent) {
        Write-Error "iOS packaging pipeline is incomplete."
        exit 1
    }

    Write-Host "==> Running static pipeline verification..." -ForegroundColor Cyan
    python scripts/verify-ios-pipeline.py
    if ($LASTEXITCODE -ne 0) {
        Write-Error "iOS static pipeline verification failed."
        exit $LASTEXITCODE
    }

    Write-Host "==> iOS XCFramework packaging pipeline files verified successfully!" -ForegroundColor Green
    Write-Host "To execute actual build on macOS: bash scripts/build_ios_xcframework.sh" -ForegroundColor Cyan
    exit 0
}

# Execute bash build script if on macOS
& bash scripts/build_ios_xcframework.sh
exit $LASTEXITCODE
