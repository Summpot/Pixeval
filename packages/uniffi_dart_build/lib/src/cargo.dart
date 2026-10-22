import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:path/path.dart' as p;

final class CargoConfiguration {
  CargoConfiguration({
    required this.manifest,
    required this.package,
    this.features = const [],
    this.defaultFeatures = true,
    this.target,
    this.release = false,
    this.environment = const {},
    required this.targetDirectory,
  });
  final String manifest, package, targetDirectory;
  final List<String> features;
  final bool defaultFeatures, release;
  final String? target;
  final Map<String, String> environment;
  List<String> get arguments => [
    'build',
    '--locked',
    '--manifest-path',
    manifest,
    '--package',
    package,
    '--target-dir',
    targetDirectory,
    '--message-format=json-render-diagnostics',
    if (target != null) ...['--target', target!],
    if (release) '--release',
    if (!defaultFeatures) '--no-default-features',
    if (features.isNotEmpty) ...['--features', features.join(',')],
  ];
  String get key => sha256
      .convert(
        utf8.encode(
          jsonEncode([
            p.normalize(p.absolute(manifest)),
            arguments,
            environment,
          ]),
        ),
      )
      .toString();
}

final class CargoArtifact {
  CargoArtifact(this.library, this.files);
  final String library;
  final List<String> files;
}

/// Builds only the native library. Never invokes native generators or reads
/// Rust syntax. Cargo retains responsibility for native dependency resolution.
final class CargoBuilder {
  static final _running = <String, Future<CargoArtifact>>{};
  Future<CargoArtifact> build(CargoConfiguration config) {
    final existing = _running[config.key];
    if (existing != null) return existing;
    final task = _build(config);
    _running[config.key] = task;
    unawaited(
      task.then(
        (_) => _running.remove(config.key),
        onError: (Object _, StackTrace __) {
          _running.remove(config.key);
        },
      ),
    );
    return task;
  }

  Future<CargoArtifact> _build(CargoConfiguration config) async {
    await Directory(config.targetDirectory).create(recursive: true);
    final lock = await File(p.join(config.targetDirectory, '.dart-cargo.lock'))
        .open(mode: FileMode.append);
    try {
      await lock.lock(FileLock.blockingExclusive);
      final process = await Process.start(
        'cargo',
        config.arguments,
        workingDirectory: p.dirname(p.absolute(config.manifest)),
        environment: config.environment,
      );
      final diagnostics = StringBuffer();
      final err = process.stderr.transform(utf8.decoder).forEach((s) {
        diagnostics.write(s);
        stderr.write(s);
      });
      final files = <String>[];
      String? library;
      await for (final line
          in process.stdout
              .transform(utf8.decoder)
              .transform(const LineSplitter())) {
        final message = jsonDecode(line) as Map<String, dynamic>;
        if (message['reason'] == 'compiler-message') {
          final rendered = (message['message'] as Map)['rendered'];
          if (rendered != null) {
            diagnostics.write(rendered);
            stderr.write(rendered);
          }
        }
        if (message['reason'] == 'compiler-artifact' &&
            (message['target'] as Map)['name'] ==
                config.package.replaceAll('-', '_')) {
          for (final path in (message['filenames'] as List).cast<String>()) {
            files.add(path);
            if (path.endsWith('.dll') ||
                path.endsWith('.so') ||
                path.endsWith('.dylib'))
              library = path;
          }
        }
      }
      final exit = await process.exitCode;
      await err;
      if (exit != 0)
        throw ProcessException(
          'cargo',
          config.arguments,
          diagnostics.toString(),
          exit,
        );
      if (library == null)
        throw StateError(
          'Cargo did not produce a cdylib for ${config.package}; files: $files',
        );
      return CargoArtifact(library, files);
    } finally {
      await lock.unlock();
      await lock.close();
    }
  }
}
