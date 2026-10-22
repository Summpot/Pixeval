import 'dart:convert';
import 'dart:io';

import 'package:code_assets/code_assets.dart';
import 'package:crypto/crypto.dart';
import 'package:hooks/hooks.dart';
import 'package:path/path.dart' as p;

import 'src/binary.dart';
import 'src/cargo.dart';
import 'src/metadata.dart';

export 'package:hooks/hooks.dart';

/// Build hooks consume committed bindings. They never generate Dart sources.
Future<void> buildNativeFfi({
  required BuildInput input,
  required BuildOutputBuilder output,
  required String contractPath,
}) async {
  if (!input.config.buildCodeAssets) return;
  final root = p.fromUri(input.packageRoot),
      contractFile = File(p.join(root, contractPath));
  if (!contractFile.existsSync())
    throw StateError(
      'Missing $contractPath. Run dart run build_runner build first.',
    );
  final contract = jsonDecode(await contractFile.readAsString()) as Json;
  final config = contract['configuration'] as Json, code = input.config.code;
  final target = switch ((code.targetOS, code.targetArchitecture)) {
    (OS.windows, Architecture.x64) => 'x86_64-pc-windows-msvc',
    (OS.android, Architecture.arm64) => 'aarch64-linux-android',
    _ => throw UnsupportedError(
      'Native FFI has not been validated for ${code.targetOS}/${code.targetArchitecture}',
    ),
  };
  final manifest = p.join(root, config['manifest'] as String);
  final environment = <String, String>{
    'BOLTFFI_BINDING_EXPANSION_ROOT': p.dirname(manifest),
    'BOLTFFI_BINDING_EXPANSION_SOURCE': p.join(
      p.dirname(manifest),
      'src',
      'lib.rs',
    ),
    'BOLTFFI_BINDING_EXPANSION': '1',
    'BOLTFFI_BINDING_EXPANSION_SURFACE': 'native',
  };
  if (code.targetOS == OS.android) {
    final compiler = code.cCompiler;
    if (compiler == null)
      throw StateError('Android NDK compiler is not configured');
    final directory = p.dirname(p.fromUri(compiler.compiler));
    final clang = p.join(
      directory,
      'aarch64-linux-android${code.android.targetNdkApi}-clang${Platform.isWindows ? '.cmd' : ''}',
    );
    final clangCpp = p.join(
      directory,
      'aarch64-linux-android${code.android.targetNdkApi}-clang++${Platform.isWindows ? '.cmd' : ''}',
    );
    environment.addAll({
      'CC_aarch64_linux_android': clang,
      'CXX_aarch64_linux_android': clangCpp,
      'CARGO_TARGET_AARCH64_LINUX_ANDROID_LINKER': clang,
    });
  }
  // NDK clang creates temporary assembly files beside its output. Deep Flutter
  // and Cargo target paths exceed Win32 path limits even when Cargo supports
  // long paths, so Android builds on Windows use a short, workspace-keyed cache.
  final cacheKey = sha256
      .convert(utf8.encode(root))
      .toString()
      .substring(0, 12);
  final targetDir = Platform.isWindows && code.targetOS == OS.android
      ? p.join(Directory.systemTemp.path, 'nffi_$cacheKey', 'android')
      : p.join(root, '.dart_tool', 'native_ffi', 'assets', target);
  if (Platform.isWindows && code.targetOS == OS.android) {
    final temporary = Directory(
      p.join(Directory.systemTemp.path, 'nffi_$cacheKey', 'tmp'),
    );
    await temporary.create(recursive: true);
    environment.addAll({
      'TEMP': temporary.path,
      'TMP': temporary.path,
      'TMPDIR': temporary.path,
    });
  }
  final artifact = await CargoBuilder().build(
    CargoConfiguration(
      manifest: manifest,
      package: config['package'] as String,
      target: target,
      targetDirectory: targetDir,
      release: config['release_native'] as bool? ?? true,
      environment: environment,
      features: (config['features'] as List? ?? []).cast<String>(),
      defaultFeatures: config['default_features'] as bool? ?? true,
    ),
  );
  final metadata = NativeMetadata.read(
    NativeBinary(
      await File(artifact.library).readAsBytes(),
      origin: artifact.library,
    ).parse(),
  );
  dynamic canonical(dynamic v) {
    if (v is Map)
      return {
        for (final key in v.keys.cast<String>().toList()..sort())
          key: canonical(v[key]),
      };
    if (v is List) return v.map(canonical).toList();
    return v;
  }

  final hash = sha256
      .convert(
        utf8.encode(
          jsonEncode(
            canonical({'uniffi': metadata.uniffi, 'boltffi': metadata.boltffi}),
          ),
        ),
      )
      .toString();
  if (hash != contract['contract_hash'])
    throw StateError(
      'Native contract changed for $target. Regenerate bindings with dart run build_runner build.',
    );
  final name = (config['asset_id'] as String).split('/').skip(1).join('/');
  final destination = File(
    p.join(p.fromUri(input.outputDirectory), p.basename(artifact.library)),
  );
  await destination.parent.create(recursive: true);
  await File(artifact.library).copy(destination.path);
  output.assets.code.add(
    CodeAsset(
      package: input.packageName,
      name: name,
      linkMode: DynamicLoadingBundled(),
      file: destination.uri,
    ),
  );
  output.dependencies.add(contractFile.uri);
  final crates = Directory(p.join(root, 'crates'));
  if (crates.existsSync()) {
    await for (final entity in crates.list(
      recursive: true,
      followLinks: false,
    )) {
      if (entity is File &&
          !entity.path.contains('${p.separator}target${p.separator}'))
        output.dependencies.add(entity.uri);
    }
  }
  for (final filename in ['Cargo.toml', 'Cargo.lock', '.cargo/config.toml']) {
    final file = File(p.join(root, filename));
    if (file.existsSync()) output.dependencies.add(file.uri);
  }
}
