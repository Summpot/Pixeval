param(
    [string]$Device = "",
    [switch]$TraceStartupOnly = $true
)

Write-Host "==> Pixeval Mobile Profile-Mode Benchmark Suite" -ForegroundColor Cyan

# 1. Run unit performance benchmark test suite
Write-Host "==> Running Dart performance & memory benchmarks..." -ForegroundColor Cyan
flutter test test/performance_benchmark_test.dart
if ($LASTEXITCODE -ne 0) {
    Write-Error "Dart performance benchmarks failed."
    exit $LASTEXITCODE
}

Write-Host "==> Dart performance & memory benchmarks passed!" -ForegroundColor Green

# 2. Check for physical device or emulator for profile run
$deviceArgs = @()
if ($Device) {
    $deviceArgs = @("-d", $Device)
}

$flutterDevices = flutter devices
Write-Host "Connected devices:"
Write-Host $flutterDevices

$hasDevice = $flutterDevices -match "(android|ios|emulator)"
if (-not $hasDevice) {
    Write-Host "NOTE: No active Android/iOS device or emulator connected." -ForegroundColor Yellow
    Write-Host "Skipping on-device profile mode trace run. Benchmark unit tests completed successfully." -ForegroundColor Yellow
    exit 0
}

Write-Host "==> Launching Profile Mode with startup tracing..." -ForegroundColor Cyan
$traceFile = "build/start_up_info.json"
Remove-Item -Force $traceFile -ErrorAction SilentlyContinue

flutter run --profile --trace-startup @deviceArgs

if (Test-Path $traceFile) {
    Write-Host "==> Startup trace generated at ${traceFile}:" -ForegroundColor Green
    Get-Content $traceFile | Write-Host
} else {
    Write-Host "Startup trace complete." -ForegroundColor Green
}
