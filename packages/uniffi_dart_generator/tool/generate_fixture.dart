import 'dart:io';

import 'package:uniffi_dart_build/uniffi_dart_build.dart';

import '../lib/src/emitter.dart';

void main(List<String> args) {
  if (args.length < 2 || args.length > 3)
    throw ArgumentError(
      'generate_fixture <library> <directory> [bolt-only|uni-only]',
    );
  final meta = NativeMetadata.read(
    NativeBinary(File(args[0]).readAsBytesSync()).parse(),
  );
  final input = switch (args.length == 3 ? args[2] : 'dual') {
    'bolt-only' => NativeMetadata(
      [],
      meta.boltffi,
      meta.exports
          .where((s) => !s.startsWith('uniffi_') && !s.startsWith('ffi_'))
          .toSet(),
    ),
    'uni-only' => NativeMetadata(
      meta.uniffi,
      [],
      meta.exports.where((s) => !s.startsWith('boltffi_')).toSet(),
    ),
    'dual' => meta,
    final mode => throw ArgumentError('Unknown fixture mode $mode'),
  };
  final result = DartBindingsEmitter(input, {
    'components': ['native_ffi_fixture'],
    'prefixes': {'native_ffi_fixture': ''},
  }, 'fixture').generate();
  Directory(args[1]).createSync(recursive: true);
  for (final entry in result.entries) {
    File('${args[1]}/${entry.key}').writeAsStringSync(entry.value);
  }
}
