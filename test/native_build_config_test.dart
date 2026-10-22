import 'dart:io';

import 'package:code_assets/code_assets.dart';
import 'package:flutter_test/flutter_test.dart';

import '../hook/src/build_config.dart';

void main() {
  test('Android linking and C compilation use the application minimum API', () {
    final environment = androidCompilerEnvironment(
      architecture: Architecture.arm,
      api: 24,
      compiler: Directory.systemTemp.uri.resolve(
        'ndk/toolchains/llvm/bin/clang',
      ),
      windowsHost: false,
    );
    final linker = environment['CARGO_TARGET_ARMV7_LINUX_ANDROIDEABI_LINKER']!;
    expect(linker, endsWith('armv7a-linux-androideabi24-clang'));
    expect(environment['CC_armv7_linux_androideabi'], linker);
    expect(
      environment['CXX_armv7_linux_androideabi'],
      endsWith('armv7a-linux-androideabi24-clang++'),
    );
  });

  test(
    'Windows NDK wrappers preserve spaces and the requested Android API',
    () {
      final environment = androidCompilerEnvironment(
        architecture: Architecture.arm64,
        api: 28,
        compiler: Directory.systemTemp.uri.resolve(
          'Android%20NDK/bin/clang.exe',
        ),
        windowsHost: true,
      );
      expect(environment['CC_aarch64_linux_android'], contains('Android NDK'));
      expect(
        environment['CARGO_TARGET_AARCH64_LINUX_ANDROID_LINKER'],
        endsWith('aarch64-linux-android28-clang.cmd'),
      );
    },
  );

  test(
    'transitive crate manifests and Cargo configuration invalidate hooks',
    () {
      final root = Directory.systemTemp.createTempSync('pixeval-hook-config-');
      addTearDown(() => root.deleteSync(recursive: true));
      const inputs = [
        'Cargo.toml',
        'Cargo.lock',
        '.cargo/config.toml',
        'crates/pixeval_native/Cargo.toml',
        'crates/pixeval_native/rust-toolchain.toml',
        'crates/pixeval_media/Cargo.toml',
      ];
      for (final path in inputs) {
        File.fromUri(root.uri.resolve(path))
          ..createSync(recursive: true)
          ..writeAsStringSync('');
      }
      expect(
        workspaceConfigurationFiles(root.uri).toSet(),
        inputs.map(root.uri.resolve).toSet(),
      );
    },
  );
}
