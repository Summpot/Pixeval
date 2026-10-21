# pixeval_flutter

A new Flutter project.

## Native builds

Install Flutter with Dart 3.13.4 or newer, Rustup, and the platform's native build tools.
Android requires the NDK version configured in `android/app/build.gradle.kts`;
iOS requires macOS and Xcode; Windows requires the Visual Studio C++ toolchain
and Clang.

Flutter Build Hooks compile and bundle the Rust bridge automatically:

```sh
flutter pub get
flutter build apk --release --split-per-abi
```

The same hook runs for `flutter run`, `flutter test`, `flutter build windows`,
and `flutter build ios`. Rustup installs the toolchain and targets pinned in
`crates/pixeval_flutter_bridge/rust-toolchain.toml` on first use. Native outputs
are cached by the Hooks tooling and are not committed to Git.

The bridge uses release builds with Thin LTO, one codegen unit per crate, and
stripped symbols, including during Flutter debug builds. Android C dependencies
and linking use the minimum Android API supplied by Flutter. Changes to Rust
sources, workspace manifests, `Cargo.lock`, and `.cargo/config.toml` invalidate
the native build cache.

Regenerate the Dart/Rust bindings with `flutter_rust_bridge_codegen generate`
after changing Rust API signatures. Build Hooks compile the native library from
the Rust sources and generated bindings.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Learn Flutter](https://docs.flutter.dev/get-started/learn-flutter)
- [Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Flutter learning resources](https://docs.flutter.dev/reference/learning-resources)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.
