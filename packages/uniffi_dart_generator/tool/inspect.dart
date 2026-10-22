import 'dart:convert';
import 'dart:io';

import 'package:uniffi_dart_build/uniffi_dart_build.dart';

void main(List<String> args) {
  if (args.length != 2)
    throw ArgumentError('Usage: inspect <native binary> <output JSON>');
  final metadata = NativeMetadata.read(
    NativeBinary(File(args[0]).readAsBytesSync(), origin: args[0]).parse(),
  );
  File(args[1]).writeAsStringSync(
    const JsonEncoder.withIndent('  ').convert(metadata.toJson()),
  );
  stdout.writeln(
    '${metadata.uniffi.length} UniFFI items; ${metadata.boltffi.length} BoltFFI envelopes; ${metadata.exports.length} exports',
  );
}
