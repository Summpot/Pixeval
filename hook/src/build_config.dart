import 'dart:io';

import 'package:code_assets/code_assets.dart';

/// Uses the application's minimum Android API for C dependencies and linking.
Map<String, String> androidCompilerEnvironment({
  required Architecture architecture,
  required int api,
  required Uri compiler,
  required bool windowsHost,
}) {
  final (rustTriple, ndkTriple) = switch (architecture) {
    Architecture.arm => ('armv7-linux-androideabi', 'armv7a-linux-androideabi'),
    Architecture.arm64 => ('aarch64-linux-android', 'aarch64-linux-android'),
    Architecture.x64 => ('x86_64-linux-android', 'x86_64-linux-android'),
    _ => throw UnsupportedError(
      'Unsupported Android architecture: $architecture',
    ),
  };
  final target = rustTriple.replaceAll('-', '_');
  final suffix = windowsHost ? '.cmd' : '';
  final clang = compiler.resolve('$ndkTriple$api-clang$suffix').toFilePath();
  final clangCpp = compiler
      .resolve('$ndkTriple$api-clang++$suffix')
      .toFilePath();
  return {
    'CC_$target': clang,
    'CXX_$target': clangCpp,
    'CARGO_TARGET_${target.toUpperCase()}_LINKER': clang,
  };
}

/// Cargo dep-info covers sources; manifests and build configuration also affect
/// feature resolution, dependencies, and compiler options.
Iterable<Uri> workspaceConfigurationFiles(Uri root) sync* {
  for (final path in [
    'Cargo.toml',
    'Cargo.lock',
    '.cargo/config.toml',
    'crates/pixeval_flutter_bridge/rust-toolchain.toml',
  ]) {
    final file = File.fromUri(root.resolve(path));
    if (file.existsSync()) yield file.uri;
  }
  final crates = Directory.fromUri(root.resolve('crates/'));
  for (final crate
      in crates.listSync(followLinks: false).whereType<Directory>()) {
    final manifest = File.fromUri(crate.uri.resolve('Cargo.toml'));
    if (manifest.existsSync()) yield manifest.uri;
  }
}
