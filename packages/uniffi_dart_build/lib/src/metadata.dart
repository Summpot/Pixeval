import 'dart:convert';
import 'dart:typed_data';

import 'binary.dart';

typedef Json = Map<String, dynamic>;

final class MetadataCursor {
  MetadataCursor(this.bytes);
  final Uint8List bytes;
  int position = 0;
  int u8() {
    if (position >= bytes.length)
      throw FormatException('Truncated metadata at $position');
    return bytes[position++];
  }

  int u16() => u8() | (u8() << 8);
  int u32() => u16() | (u16() << 16);
  bool boolean() => switch (u8()) {
    0 => false,
    1 => true,
    final n => throw FormatException('Invalid metadata bool $n'),
  };
  String string([bool long = false]) {
    final length = long ? u16() : u8(), start = position;
    if (length > bytes.length - start)
      throw FormatException('Truncated metadata string at $start');
    position += length;
    return utf8.decode(
      Uint8List.sublistView(bytes, start, position),
      allowMalformed: false,
    );
  }

  String? optionalString() => boolean() ? string() : null;
  String? doc() {
    final value = string(true);
    return value.isEmpty ? null : value;
  }

  Json type([int depth = 0]) {
    if (depth > 64)
      throw const FormatException('Metadata type nesting exceeds 64');
    final code = u8();
    const primitives = {
      0: 'u8',
      1: 'u16',
      2: 'u32',
      3: 'u64',
      4: 'i8',
      5: 'i16',
      6: 'i32',
      7: 'i64',
      8: 'f32',
      9: 'f64',
      10: 'bool',
      11: 'string',
      19: 'timestamp',
      20: 'duration',
      255: 'void',
    };
    if (primitives.containsKey(code)) return {'kind': primitives[code]};
    if ({13, 14, 16, 21, 24}.contains(code)) {
      final result = <String, dynamic>{
        'kind': {
          13: 'record',
          14: 'enum',
          16: 'object',
          21: 'callback',
          24: 'object',
        }[code],
        'module': string(),
        'name': string(),
      };
      if (code == 24) result['traitKind'] = u8();
      return result;
    }
    if (code == 12 || code == 17 || code == 26 || code == 27) {
      final inner = type(depth + 1);
      return code == 17 && inner['kind'] == 'u8'
          ? {'kind': 'bytes'}
          : {
              'kind': {
                12: 'option',
                17: 'sequence',
                26: 'box',
                27: 'set',
              }[code],
              'inner': inner,
            };
    }
    if (code == 18)
      return {'kind': 'map', 'key': type(depth + 1), 'value': type(depth + 1)};
    if (code == 22)
      return {
        'kind': 'custom',
        'module': string(),
        'name': string(),
        'inner': type(depth + 1),
      };
    throw FormatException(
      'Unsupported UniFFI metadata type code $code at ${position - 1}',
    );
  }

  dynamic literal() => switch (u8()) {
    0 => string(),
    1 => {'integer': string()},
    2 => {'float': string()},
    3 => boolean(),
    4 => null,
    5 => literal(),
    6 => <dynamic>[],
    7 => <String, dynamic>{},
    8 => <dynamic>[],
    final code => throw FormatException('Unknown literal code $code'),
  };
  dynamic defaultValue() {
    if (!boolean()) return null;
    final kind = u8();
    if (kind == 0) return {'default': true};
    if (kind == 1) return {'literal': literal()};
    throw FormatException('Unsupported default kind $kind');
  }

  List<Json> inputs() => List.generate(u8(), (_) {
    final name = string(), t = type(), ref = boolean(), def = defaultValue();
    return {'name': name, 'type': t, 'byRef': ref, 'default': def};
  });
  List<Json> fields() => List.generate(u8(), (_) {
    final name = string(),
        original = optionalString(),
        t = type(),
        def = defaultValue(),
        documentation = doc();
    return {
      'name': name,
      'original': original,
      'type': t,
      'default': def,
      'doc': documentation,
    };
  });
  Json item() {
    final kind = u8();
    if (kind == 6)
      return {'kind': 'namespace', 'module': string(), 'name': string()};
    if (kind == 9)
      return {
        'kind': 'callback',
        'module': string(),
        'name': string(),
        'doc': doc(),
      };
    final module = string();
    String? owner;
    int? index;
    if (kind == 1 || kind == 7 || kind == 10) owner = string();
    if (kind == 10) index = u32();
    final name = string(), original = optionalString();
    final result = <String, dynamic>{
      'kind':
          {
            0: 'function',
            1: 'method',
            2: 'record',
            3: 'enum',
            4: 'object',
            7: 'constructor',
            8: 'trait',
            10: 'traitMethod',
            12: 'trait',
          }[kind] ??
          (throw FormatException('Unsupported metadata item $kind')),
      'module': module,
      'name': name,
      'original': original,
    };
    if (owner != null) result['owner'] = owner;
    if (index != null) result['index'] = index;
    if ({0, 1, 7, 10}.contains(kind)) {
      result['async'] = boolean();
      result['inputs'] = inputs();
      if (bytes[position] == 23) {
        u8();
        result['return'] = type();
        result['throws'] = type();
      } else {
        result['return'] = type();
      }
    }
    if (kind == 2) result['fields'] = fields();
    if (kind == 3) {
      final shape = u8();
      result['shape'] = shape;
      result['discriminantType'] = boolean() ? type() : null;
      result['variants'] = List.generate(u8(), (_) {
        final n = string(), original = optionalString();
        if (shape != 2 && boolean()) literal();
        final f = shape == 2 ? <Json>[] : fields();
        return {'name': n, 'original': original, 'fields': f, 'doc': doc()};
      });
      result['nonExhaustive'] = boolean();
    }
    result['doc'] = doc();
    if (kind == 8 || kind == 12) result['traitKind'] = u8();
    if ({0, 1, 7, 10}.contains(kind))
      result['checksum'] = checksum(Uint8List.sublistView(bytes, 0, position));
    return result;
  }

  static int checksum(Uint8List bytes) {
    // Explicit BigInt arithmetic preserves unsigned 64-bit FNV overflow in Dart.
    var hash = BigInt.parse('cbf29ce484222325', radix: 16);
    final prime = BigInt.parse('100000001b3', radix: 16),
        mask = (BigInt.one << 64) - BigInt.one;
    for (final b in bytes) {
      hash = ((hash ^ BigInt.from(b)) * prime) & mask;
    }
    return ((hash ^ (hash >> 16) ^ (hash >> 32) ^ (hash >> 48)) &
            BigInt.from(0xffff))
        .toInt();
  }
}

final class NativeMetadata {
  NativeMetadata(this.uniffi, this.boltffi, this.exports);
  final List<Json> uniffi;
  final List<Json> boltffi;
  final Set<String> exports;
  factory NativeMetadata.read(NativeBinary image) {
    final items = <Json>[], envelopes = <Json>[];
    for (final entry in image.symbols.entries) {
      try {
        items.add(MetadataCursor(entry.value).item());
      } catch (error) {
        throw FormatException('${image.origin}: ${entry.key}: $error');
      }
    }
    final seen = <String, String>{};
    for (final sections in image.sections.values) {
      for (final section in sections) {
        var p = 0;
        while (p < section.length) {
          if (section[p] == 0) {
            p++;
            continue;
          }
          if (section.length - p < 16 ||
              ascii.decode(
                    Uint8List.sublistView(section, p, p + 8),
                    allowInvalid: true,
                  ) !=
                  'BFFIMD01') {
            throw FormatException(
              '${image.origin}: invalid BoltFFI metadata frame at $p',
            );
          }
          final length = ByteData.sublistView(section)
              .getUint64(p + 8, Endian.little);
          if (length <= 0 || length > section.length - p - 16)
            throw FormatException('Invalid BoltFFI metadata length at $p');
          final value = jsonDecode(
            utf8.decode(
              Uint8List.sublistView(section, p + 16, p + 16 + length),
            ),
          ) as Json;
          if (value['magic'] != 'boltffi.bindings' || value['format'] is! Map)
            throw const FormatException('Invalid BoltFFI envelope');
          final format = value['format'] as Map;
          if (format['major'] != 0 || format['minor'] != 1)
            throw FormatException(
              'Unsupported BoltFFI metadata version: $format',
            );
          final key = jsonEncode([value['package'], value['surface']]);
          final canonical = jsonEncode(value);
          if (seen.containsKey(key) && seen[key] != canonical)
            throw FormatException('Conflicting BoltFFI envelope: $key');
          if (!seen.containsKey(key)) {
            envelopes.add(value);
            seen[key] = canonical;
          }
          p += 16 + length;
        }
      }
    }
    items.sort(
      (a, b) => jsonEncode([
        a['module'],
        a['kind'],
        a['owner'],
        a['name'],
      ]).compareTo(jsonEncode([b['module'], b['kind'], b['owner'], b['name']])),
    );
    envelopes.sort(
      (a, b) => jsonEncode(a['package']).compareTo(jsonEncode(b['package'])),
    );
    return NativeMetadata(items, envelopes, image.exports);
  }
  Json toJson() => {
    'uniffi': uniffi,
    'boltffi': boltffi,
    'exports': exports.toList()..sort(),
  };
}
