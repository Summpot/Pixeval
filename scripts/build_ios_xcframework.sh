#!/usr/bin/env bash
set -euo pipefail

# Build iOS XCFramework for pixeval_flutter_bridge
# Target triples:
# - aarch64-apple-ios (Physical device, arm64)
# - aarch64-apple-ios-sim (Simulator, arm64 - Apple Silicon)
# - x86_64-apple-ios (Simulator, x86_64 - Intel)

echo "==> Ensuring iOS rust targets are installed..."
rustup target add aarch64-apple-ios aarch64-apple-ios-sim x86_64-apple-ios

CRATE_NAME="pixeval_flutter_bridge"
LIB_NAME="lib${CRATE_NAME}.a"

echo "==> Building for iOS device (aarch64-apple-ios)..."
cargo build -p "$CRATE_NAME" --target aarch64-apple-ios --release

echo "==> Building for iOS simulator arm64 (aarch64-apple-ios-sim)..."
cargo build -p "$CRATE_NAME" --target aarch64-apple-ios-sim --release

echo "==> Building for iOS simulator x86_64 (x86_64-apple-ios)..."
cargo build -p "$CRATE_NAME" --target x86_64-apple-ios --release

SIM_FAT_DIR="target/universal-ios-sim/release"
mkdir -p "$SIM_FAT_DIR"

echo "==> Assembling universal simulator library with lipo..."
lipo -create \
    "target/aarch64-apple-ios-sim/release/${LIB_NAME}" \
    "target/x86_64-apple-ios/release/${LIB_NAME}" \
    -output "${SIM_FAT_DIR}/${LIB_NAME}"

HEADERS_DIR="ios/Frameworks/PixevalFlutterBridge/include"
FRAMEWORK_OUTPUT="ios/Frameworks/PixevalFlutterBridge.xcframework"

rm -rf "$FRAMEWORK_OUTPUT"

echo "==> Creating XCFramework via xcodebuild..."
xcodebuild -create-xcframework \
    -library "target/aarch64-apple-ios/release/${LIB_NAME}" \
    -headers "$HEADERS_DIR" \
    -library "${SIM_FAT_DIR}/${LIB_NAME}" \
    -headers "$HEADERS_DIR" \
    -output "$FRAMEWORK_OUTPUT"

echo "==> Successfully packaged XCFramework at $FRAMEWORK_OUTPUT!"
