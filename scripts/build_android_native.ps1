param(
    [string[]]$Targets = @('arm64-v8a', 'armeabi-v7a', 'x86_64'),
    [string]$NdkPath = $env:ANDROID_NDK_HOME
)

# 1. Discover Android NDK
if (-not $NdkPath) {
    $searchRoots = @(
        $env:ANDROID_NDK_HOME,
        $env:ANDROID_HOME,
        $env:ANDROID_SDK_ROOT,
        "$env:LOCALAPPDATA\Android\Sdk",
        "D:\dev\android-sdk"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $searchRoots) {
        $cand = if (Test-Path "$root\ndk") { "$root\ndk" } elseif ((Split-Path -Leaf $root) -eq "ndk") { $root } else { $null }
        if ($cand -and (Test-Path $cand)) {
            $latestNdk = Get-ChildItem -Directory $cand | Sort-Object Name -Descending | Select-Object -First 1
            if ($latestNdk) {
                $NdkPath = $latestNdk.FullName
                break
            }
        }
    }
}

if (-not $NdkPath -or -not (Test-Path $NdkPath)) {
    Write-Error "Android NDK not found. Please set ANDROID_NDK_HOME or ANDROID_HOME."
    exit 1
}

$env:ANDROID_NDK_HOME = $NdkPath
$env:NDK_HOME = $NdkPath
Write-Host "==> Using Android NDK: $NdkPath" -ForegroundColor Green

# 2. Verify cargo-ndk is available
$cargoNdk = Get-Command cargo-ndk -ErrorAction SilentlyContinue
if (-not $cargoNdk) {
    Write-Host "==> cargo-ndk not found in PATH. Attempting automatic installation..." -ForegroundColor Yellow
    $binstall = Get-Command cargo-binstall -ErrorAction SilentlyContinue
    if ($binstall) {
        cargo binstall --no-confirm cargo-ndk
    } else {
        cargo install cargo-ndk
    }
    $cargoNdk = Get-Command cargo-ndk -ErrorAction SilentlyContinue
    if (-not $cargoNdk) {
        Write-Error "Failed to install cargo-ndk. Please run: cargo install cargo-ndk"
        exit 1
    }
}

# Target to Rust triple mapping
$targetToRust = @{
    'arm64-v8a'   = 'aarch64-linux-android'
    'armeabi-v7a' = 'armv7-linux-androideabi'
    'x86_64'      = 'x86_64-linux-android'
    'x86'         = 'i686-linux-android'
}

$installedTargets = rustup target list --installed

# 3. Build each requested architecture
$outDir = "android/app/src/main/jniLibs"

foreach ($target in $Targets) {
    $rustTarget = $targetToRust[$target]
    if ($rustTarget -and ($installedTargets -notcontains $rustTarget)) {
        Write-Host "==> Adding rustup target $rustTarget for $target..." -ForegroundColor Cyan
        rustup target add $rustTarget
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to add target $rustTarget via rustup"
            exit $LASTEXITCODE
        }
    }

    Write-Host "==> Building for Android target: $target ($rustTarget)..." -ForegroundColor Cyan
    cargo ndk -t $target -o $outDir build -p pixeval_flutter_bridge --release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $target"
        exit $LASTEXITCODE
    }

    # Clean intermediate dynamic deps if any
    Remove-Item -Force "$outDir/$target/libboltffi_core*.so" -ErrorAction SilentlyContinue

    # Strip symbols
    $llvmStrip = Get-ChildItem -Path "$NdkPath/toolchains/llvm/prebuilt" -Filter "llvm-strip.exe" -Recurse | Select-Object -First 1
    if ($llvmStrip) {
        Write-Host "==> Stripping symbols for $target..." -ForegroundColor Green
        & $llvmStrip.FullName --strip-all "$outDir/$target/libpixeval_flutter_bridge.so"
    }
}

Write-Host "==> Android native libraries successfully built and packaged in $outDir!" -ForegroundColor Green
