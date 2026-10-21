#!/usr/bin/env bash
set -euo pipefail

# Discover NDK
NDK_PATH="${ANDROID_NDK_HOME:-${NDK_HOME:-}}"

if [ -z "$NDK_PATH" ]; then
    for candidate in "${ANDROID_HOME:-}/ndk" "${ANDROID_SDK_ROOT:-}/ndk" "$HOME/Android/Sdk/ndk"; do
        if [ -d "$candidate" ]; then
            NDK_PATH=$(find "$candidate" -maxdepth 1 -mindepth 1 -type d | sort -V | tail -n 1)
            break
        fi
    done
fi

if [ -z "$NDK_PATH" ] || [ ! -d "$NDK_PATH" ]; then
    echo "Error: Android NDK not found. Please set ANDROID_NDK_HOME or ANDROID_HOME." >&2
    exit 1
fi

export ANDROID_NDK_HOME="$NDK_PATH"
export NDK_HOME="$NDK_PATH"
echo "==> Using Android NDK: $NDK_PATH"

if ! command -v cargo-ndk &> /dev/null; then
    echo "==> cargo-ndk not found, installing..."
    cargo install cargo-ndk
fi

TARGETS=("arm64-v8a" "armeabi-v7a" "x86_64")
OUT_DIR="android/app/src/main/jniLibs"
mkdir -p "$OUT_DIR"

declare -A RUST_TRIPLES=(
    ["arm64-v8a"]="aarch64-linux-android"
    ["armeabi-v7a"]="armv7-linux-androideabi"
    ["x86_64"]="x86_64-linux-android"
)

for target in "${TARGETS[@]}"; do
    rust_triple="${RUST_TRIPLES[$target]}"
    echo "==> Ensuring rustup target $rust_triple is installed..."
    rustup target add "$rust_triple"

    echo "==> Building for Android target: $target ($rust_triple)..."
    cargo ndk -t "$target" -o "$OUT_DIR" build -p pixeval_flutter_bridge --release

    # Remove temporary bridge deps if any
    rm -f "$OUT_DIR/$target/libboltffi_core"*.so

    # Strip symbols
    LLVM_STRIP=$(find "$NDK_PATH/toolchains/llvm/prebuilt" -name "llvm-strip" -type f | head -n 1)
    if [ -n "$LLVM_STRIP" ] && [ -x "$LLVM_STRIP" ]; then
        echo "==> Stripping symbols for $target..."
        "$LLVM_STRIP" --strip-all "$OUT_DIR/$target/libpixeval_flutter_bridge.so"
    fi
done

echo "==> Android native libraries successfully built and packaged in $OUT_DIR!"
