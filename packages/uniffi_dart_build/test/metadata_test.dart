import 'dart:convert';
import 'dart:typed_data';

import 'package:test/test.dart';
import 'package:uniffi_dart_build/uniffi_dart_build.dart';

Uint8List namespace() => Uint8List.fromList([
  6,
  4,
  ...ascii.encode('demo'),
  4,
  ...ascii.encode('demo'),
]);

Uint8List coff(Uint8List metadata) {
  final b = Uint8List(256 + metadata.length), d = ByteData.sublistView(b);
  d.setUint16(0, 0x8664, Endian.little);
  d.setUint16(2, 1, Endian.little);
  d.setUint32(8, 80, Endian.little);
  d.setUint32(12, 1, Endian.little);
  b.setAll(20, ascii.encode('.rdata'));
  d.setUint32(36, metadata.length, Endian.little);
  d.setUint32(40, 256, Endian.little);
  d.setUint32(84, 4, Endian.little);
  d.setInt16(92, 1, Endian.little);
  d.setUint32(98, 36, Endian.little);
  b.setAll(102, ascii.encode('UNIFFI_META_DEMO_NAMESPACE\x00'));
  b.setAll(256, metadata);
  return b;
}

Uint8List archive(Uint8List object) {
  final header =
      'object.o/       '
      '${'0'.padRight(12)}'
      '${'0'.padRight(6)}'
      '${'0'.padRight(6)}'
      '${'100644'.padRight(8)}'
      '${object.length.toString().padRight(10)}'
      '`\n';
  return Uint8List.fromList([
    ...ascii.encode('!<arch>\n$header'),
    ...object,
    if (object.length.isOdd) 10,
  ]);
}

void main() {
  test('COFF and archive locate addressable UniFFI symbols offline', () {
    for (final bytes in [coff(namespace()), archive(coff(namespace()))]) {
      final metadata = NativeMetadata.read(NativeBinary(bytes).parse());
      expect(metadata.uniffi.single, {
        'kind': 'namespace',
        'module': 'demo',
        'name': 'demo',
      });
    }
  });
  test('truncation and invalid archive lengths fail explicitly', () {
    expect(() => NativeBinary(Uint8List(4)).parse(), throwsFormatException);
    final bytes = archive(coff(namespace()));
    expect(
      () =>
          NativeBinary(Uint8List.sublistView(bytes, 0, bytes.length - 10))
              .parse(),
      throwsFormatException,
    );
  });
  test('metadata rejects unknown types and excessive nesting', () {
    expect(
      () => MetadataCursor(Uint8List.fromList([200])).type(),
      throwsFormatException,
    );
    expect(
      () =>
          MetadataCursor(Uint8List.fromList([...List.filled(66, 12), 0]))
              .type(),
      throwsFormatException,
    );
    expect(
      () => MetadataCursor(Uint8List.fromList([6, 255])).item(),
      throwsFormatException,
    );
  });
  test('BoltFFI envelope framing, strict version and bounds checks', () {
    Uint8List frame(Json json) {
      final payload = utf8.encode(jsonEncode(json)),
          b = Uint8List(16 + payload.length);
      b.setAll(0, ascii.encode('BFFIMD01'));
      ByteData.sublistView(b).setUint64(8, payload.length, Endian.little);
      b.setAll(16, payload);
      return b;
    }

    final envelope = <String, dynamic>{
      'magic': 'boltffi.bindings',
      'format': {'major': 0, 'minor': 1},
      'package': {'name': 'demo'},
      'surface': 'native',
      'bindings': {
        'Native': {'decls': []},
      },
    };
    final image = NativeBinary(Uint8List(0));
    image.sections['.boltffi'] = [frame(envelope)];
    expect(NativeMetadata.read(image).boltffi, hasLength(1));
    image.sections['.boltffi'] = [
      frame({
        ...envelope,
        'format': {'major': 9, 'minor': 0},
      }),
    ];
    expect(() => NativeMetadata.read(image), throwsFormatException);
    image.sections['.boltffi'] = [
      Uint8List.fromList([...ascii.encode('BFFIMD01'), ...List.filled(8, 255)]),
    ];
    expect(() => NativeMetadata.read(image), throwsFormatException);
  });
  test('Cargo commands retain spaces and metacharacters as arguments', () {
    final config = CargoConfiguration(
      manifest: r'C:/a b/$(value)/Cargo.toml',
      package: 'demo',
      targetDirectory: 'C:/cache',
      features: ['a', 'b'],
      environment: {},
    );
    expect(
      config.arguments[config.arguments.indexOf('--manifest-path') + 1],
      config.manifest,
    );
    expect(config.arguments.last, 'a,b');
    expect(config.key, hasLength(64));
  });
}
