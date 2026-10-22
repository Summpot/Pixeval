import 'dart:convert';
import 'dart:typed_data';

import 'package:test/test.dart';
import 'package:uniffi_dart_build/uniffi_dart_build.dart';

final metadata = Uint8List.fromList([
  6,
  4,
  ...ascii.encode('demo'),
  4,
  ...ascii.encode('demo'),
]);
const symbol = 'UNIFFI_META_DEMO_NAMESPACE';

Uint8List pe() {
  final b = Uint8List(1024), d = ByteData.sublistView(b);
  d.setUint16(0, 0x5a4d, Endian.little);
  d.setUint32(0x3c, 64, Endian.little);
  d.setUint32(64, 0x4550, Endian.little);
  d.setUint16(70, 1, Endian.little);
  d.setUint16(84, 240, Endian.little);
  d.setUint16(88, 0x20b, Endian.little);
  d.setUint32(200, 0x1000, Endian.little);
  b.setAll(328, ascii.encode('.rdata'));
  d.setUint32(340, 0x1000, Endian.little);
  d.setUint32(344, 512, Endian.little);
  d.setUint32(348, 512, Endian.little);
  d.setUint32(536, 1, Endian.little);
  d.setUint32(540, 0x1040, Endian.little);
  d.setUint32(544, 0x1044, Endian.little);
  d.setUint32(548, 0x1048, Endian.little);
  d.setUint32(576, 0x1100, Endian.little);
  d.setUint32(580, 0x1060, Endian.little);
  b.setAll(608, ascii.encode('$symbol\x00'));
  b.setAll(768, metadata);
  return b;
}

Uint8List elf(Endian e) {
  final b = Uint8List(768), d = ByteData.sublistView(b);
  b.setAll(0, [127, 69, 76, 70, 2, e == Endian.little ? 1 : 2]);
  d.setUint64(40, 64, e);
  d.setUint16(58, 64, e);
  d.setUint16(60, 4, e);
  d.setUint16(62, 1, e);
  void section(
    int index,
    int kind,
    int offset,
    int size, {
    int link = 0,
    int stride = 0,
  }) {
    final p = 64 + index * 64;
    d.setUint32(p + 4, kind, e);
    d.setUint64(p + 24, offset, e);
    d.setUint64(p + 32, size, e);
    d.setUint32(p + 40, link, e);
    d.setUint64(p + 56, stride, e);
  }

  section(1, 3, 320, 64);
  section(2, 1, 400, metadata.length);
  section(3, 2, 480, 24, link: 1, stride: 24);
  b.setAll(321, ascii.encode('$symbol\x00'));
  b.setAll(400, metadata);
  d.setUint32(480, 1, e);
  d.setUint16(486, 2, e);
  d.setUint64(496, metadata.length, e);
  return b;
}

Uint8List macho() {
  final b = Uint8List(512), d = ByteData.sublistView(b);
  d.setUint32(0, 0xfeedfacf, Endian.little);
  d.setUint32(16, 2, Endian.little);
  d.setUint32(32, 0x19, Endian.little);
  d.setUint32(36, 152, Endian.little);
  d.setUint32(96, 1, Endian.little);
  b.setAll(104, ascii.encode('__const'));
  d.setUint64(136, 0x1000, Endian.little);
  d.setUint64(144, metadata.length, Endian.little);
  d.setUint32(152, 256, Endian.little);
  b.setAll(256, metadata);
  d.setUint32(184, 2, Endian.little);
  d.setUint32(188, 24, Endian.little);
  d.setUint32(192, 300, Endian.little);
  d.setUint32(196, 1, Endian.little);
  d.setUint32(200, 320, Endian.little);
  d.setUint32(300, 1, Endian.little);
  b[305] = 1;
  d.setUint64(308, 0x1000, Endian.little);
  b.setAll(321, ascii.encode('_$symbol\x00'));
  return b;
}

void main() {
  for (final entry in {
    'PE64': pe(),
    'ELF64 little endian': elf(Endian.little),
    'ELF64 big endian': elf(Endian.big),
    'Mach-O64': macho(),
  }.entries) {
    test('${entry.key} resolves metadata through address tables', () {
      final value = NativeMetadata.read(
        NativeBinary(entry.value, origin: entry.key).parse(),
      );
      expect(value.uniffi.single, {
        'kind': 'namespace',
        'module': 'demo',
        'name': 'demo',
      });
    });
    test('${entry.key} rejects truncated address tables', () {
      expect(
        () => NativeBinary(Uint8List.sublistView(entry.value, 0, 80)).parse(),
        throwsFormatException,
      );
    });
  }
}
