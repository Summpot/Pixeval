import 'dart:convert';
import 'dart:io';

import 'package:build/build.dart';
import 'package:crypto/crypto.dart';
import 'package:dart_style/dart_style.dart';
import 'package:glob/glob.dart';
import 'package:path/path.dart' as p;
import 'package:uniffi_dart_build/uniffi_dart_build.dart';
import 'package:yaml/yaml.dart';

import 'src/emitter.dart';

Builder nativeFfiBuilder(BuilderOptions options) => NativeFfiBuilder();

final class NativeFfiBuilder implements Builder {
  @override
  final buildExtensions = const {
    '.ffi.yaml': [
      '.g.dart',
      '.uniffi.g.dart',
      '.boltffi.g.dart',
      '.contract.json',
    ],
  };

  @override
  Future<void> build(BuildStep step) async {
    final config = jsonDecode(
      jsonEncode(loadYaml(await step.readAsString(step.inputId))),
    ) as Json;
    // build_runner rewrites package roots to asset: URIs. This builder is
    // root-package-only, so Cargo's working root is the runner's directory.
    final root = Directory.current.absolute.path;
    final inputs =
        (config['inputs'] as List? ??
                [
                  'Cargo.toml',
                  'Cargo.lock',
                  'crates/**/Cargo.toml',
                  'crates/**/build.rs',
                  'crates/**/src/**',
                  'crates/**/rust-toolchain.toml',
                  '.cargo/**',
                ])
            .cast<String>();
    for (final pattern in inputs) {
      await for (final id in step.findAssets(Glob(pattern))) {
        if (!id.path.contains('/target/') &&
            !id.path.contains('/.dart_tool/')) {
          await step.readAsBytes(id);
        }
      }
    }
    final manifest = p.normalize(p.join(root, config['manifest'] as String));
    final artifact = await CargoBuilder().build(
      CargoConfiguration(
        manifest: manifest,
        package: config['package'] as String,
        targetDirectory: p.join(root, '.dart_tool', 'native_ffi', 'metadata'),
        features: (config['features'] as List? ?? []).cast<String>(),
        defaultFeatures: config['default_features'] as bool? ?? true,
        environment: {
          'CARGO_PROFILE_DEV_LTO': 'false',
          'CARGO_PROFILE_DEV_STRIP': 'none',
          'BOLTFFI_BINDING_EXPANSION_ROOT': p.dirname(manifest),
          'BOLTFFI_BINDING_EXPANSION_SOURCE': p.join(
            p.dirname(manifest),
            'src',
            'lib.rs',
          ),
          'BOLTFFI_BINDING_EXPANSION': '1',
          'BOLTFFI_BINDING_EXPANSION_SURFACE': 'native',
        },
      ),
    );
    final metadata = NativeMetadata.read(
      NativeBinary(
        await File(artifact.library).readAsBytes(),
        origin: artifact.library,
      ).parse(),
    );
    if (metadata.uniffi.isEmpty && metadata.boltffi.isEmpty) {
      throw StateError('Native library contains no supported metadata');
    }
    final stem = p
        .basename(step.inputId.path)
        .replaceFirst(RegExp(r'\.ffi\.yaml$'), '');
    final generated = DartBindingsEmitter(metadata, config, stem).generate();
    final formatter = DartFormatter(
      languageVersion: DartFormatter.latestLanguageVersion,
    );
    for (final entry in generated.entries) {
      final output = AssetId(
        step.inputId.package,
        p.join(p.dirname(step.inputId.path), entry.key).replaceAll('\\', '/'),
      );
      await step.writeAsString(output, formatter.format(entry.value));
    }
    final canonical = canonicalJson({
      'uniffi': metadata.uniffi,
      'boltffi': metadata.boltffi,
    });
    final contractId = AssetId(
      step.inputId.package,
      step.inputId.path.replaceFirst(RegExp(r'\.ffi\.yaml$'), '.contract.json'),
    );
    final contractContent = const JsonEncoder.withIndent('  ').convert({
      'format': 1,
      'uniffi': '0.32',
      'boltffi': '0.31.1',
      'contract_hash': sha256.convert(utf8.encode(canonical)).toString(),
      'configuration': config,
      'components':
          metadata.uniffi
              .where((i) => i['kind'] == 'namespace')
              .map((i) => i['module'])
              .toList()
            ..sort(),
    });
    await step.writeAsString(contractId, '$contractContent\n');
  }
}

String canonicalJson(dynamic value) {
  dynamic canonical(dynamic v) {
    if (v is Map) {
      return {
        for (final key in v.keys.cast<String>().toList()..sort())
          key: canonical(v[key]),
      };
    }
    if (v is List) {
      return v.map(canonical).toList();
    }
    return v;
  }

  return jsonEncode(canonical(value));
}
