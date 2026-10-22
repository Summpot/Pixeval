import 'dart:convert';
import 'dart:typed_data';

import 'binary.dart';

typedef Json = Map<String, dynamic>;

/// UniFFI metadata serialized type codes.
abstract final class UniFfiTypeCode {
  static const u8 = 0;
  static const u16 = 1;
  static const u32 = 2;
  static const u64 = 3;
  static const i8 = 4;
  static const i16 = 5;
  static const i32 = 6;
  static const i64 = 7;
  static const f32 = 8;
  static const f64 = 9;
  static const boolean = 10;
  static const string = 11;
  static const option = 12;
  static const record = 13;
  static const enumeration = 14;
  static const object = 16;
  static const sequence = 17;
  static const map = 18;
  static const timestamp = 19;
  static const duration = 20;
  static const callback = 21;
  static const custom = 22;
  static const result = 23;
  static const traitObject = 24;
  static const box = 26;
  static const set = 27;
  static const voidType = 255;
}

/// UniFFI metadata declaration item kinds.
abstract final class UniFfiItemKind {
  static const function = 0;
  static const method = 1;
  static const record = 2;
  static const enumeration = 3;
  static const object = 4;
  static const namespace = 6;
  static const constructor = 7;
  static const trait = 8;
  static const callback = 9;
  static const traitMethod = 10;
  static const externalTrait = 12;
}

final class MetadataCursor {
  MetadataCursor(this.bytes);

  final Uint8List bytes;
  int position = 0;

  int u8() {
    if (position >= bytes.length) {
      throw FormatException('Truncated metadata at $position');
    }
    return bytes[position++];
  }

  int u16() => u8() | (u8() << 8);
  int u32() => u16() | (u16() << 16);

  bool boolean() => switch (u8()) {
    0 => false,
    1 => true,
    final code => throw FormatException('Invalid metadata bool $code'),
  };

  String string([bool long = false]) {
    final length = long ? u16() : u8();
    final start = position;
    if (length > bytes.length - start) {
      throw FormatException('Truncated metadata string at $start');
    }
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
    if (depth > 64) {
      throw const FormatException('Metadata type nesting exceeds 64');
    }
    final code = u8();
    const primitives = {
      UniFfiTypeCode.u8: 'u8',
      UniFfiTypeCode.u16: 'u16',
      UniFfiTypeCode.u32: 'u32',
      UniFfiTypeCode.u64: 'u64',
      UniFfiTypeCode.i8: 'i8',
      UniFfiTypeCode.i16: 'i16',
      UniFfiTypeCode.i32: 'i32',
      UniFfiTypeCode.i64: 'i64',
      UniFfiTypeCode.f32: 'f32',
      UniFfiTypeCode.f64: 'f64',
      UniFfiTypeCode.boolean: 'bool',
      UniFfiTypeCode.string: 'string',
      UniFfiTypeCode.timestamp: 'timestamp',
      UniFfiTypeCode.duration: 'duration',
      UniFfiTypeCode.voidType: 'void',
    };
    if (primitives.containsKey(code)) {
      return {'kind': primitives[code]};
    }
    if ({
      UniFfiTypeCode.record,
      UniFfiTypeCode.enumeration,
      UniFfiTypeCode.object,
      UniFfiTypeCode.callback,
      UniFfiTypeCode.traitObject,
    }.contains(code)) {
      final kindName = {
        UniFfiTypeCode.record: 'record',
        UniFfiTypeCode.enumeration: 'enum',
        UniFfiTypeCode.object: 'object',
        UniFfiTypeCode.callback: 'callback',
        UniFfiTypeCode.traitObject: 'object',
      }[code];
      final result = <String, dynamic>{
        'kind': kindName,
        'module': string(),
        'name': string(),
      };
      if (code == UniFfiTypeCode.traitObject) {
        result['traitKind'] = u8();
      }
      return result;
    }
    if (code == UniFfiTypeCode.option ||
        code == UniFfiTypeCode.sequence ||
        code == UniFfiTypeCode.box ||
        code == UniFfiTypeCode.set) {
      final inner = type(depth + 1);
      return code == UniFfiTypeCode.sequence && inner['kind'] == 'u8'
          ? {'kind': 'bytes'}
          : {
              'kind': {
                UniFfiTypeCode.option: 'option',
                UniFfiTypeCode.sequence: 'sequence',
                UniFfiTypeCode.box: 'box',
                UniFfiTypeCode.set: 'set',
              }[code],
              'inner': inner,
            };
    }
    if (code == UniFfiTypeCode.map) {
      return {
        'kind': 'map',
        'key': type(depth + 1),
        'value': type(depth + 1),
      };
    }
    if (code == UniFfiTypeCode.custom) {
      return {
        'kind': 'custom',
        'module': string(),
        'name': string(),
        'inner': type(depth + 1),
      };
    }
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
    if (!boolean()) {
      return null;
    }
    final kind = u8();
    if (kind == 0) {
      return {'default': true};
    }
    if (kind == 1) {
      return {'literal': literal()};
    }
    throw FormatException('Unsupported default kind $kind');
  }

  List<Json> inputs() => List.generate(u8(), (_) {
    final name = string();
    final paramType = type();
    final byRef = boolean();
    final def = defaultValue();
    return {
      'name': name,
      'type': paramType,
      'byRef': byRef,
      'default': def,
    };
  });

  List<Json> fields() => List.generate(u8(), (_) {
    final name = string();
    final original = optionalString();
    final fieldType = type();
    final def = defaultValue();
    final documentation = doc();
    return {
      'name': name,
      'original': original,
      'type': fieldType,
      'default': def,
      'doc': documentation,
    };
  });

  Json item() {
    final kind = u8();
    if (kind == UniFfiItemKind.namespace) {
      return {
        'kind': 'namespace',
        'module': string(),
        'name': string(),
      };
    }
    if (kind == UniFfiItemKind.callback) {
      return {
        'kind': 'callback',
        'module': string(),
        'name': string(),
        'doc': doc(),
      };
    }

    final module = string();
    String? owner;
    int? index;
    if (kind == UniFfiItemKind.method ||
        kind == UniFfiItemKind.constructor ||
        kind == UniFfiItemKind.traitMethod) {
      owner = string();
    }
    if (kind == UniFfiItemKind.traitMethod) {
      index = u32();
    }

    final name = string();
    final original = optionalString();
    final kindName = {
      UniFfiItemKind.function: 'function',
      UniFfiItemKind.method: 'method',
      UniFfiItemKind.record: 'record',
      UniFfiItemKind.enumeration: 'enum',
      UniFfiItemKind.object: 'object',
      UniFfiItemKind.constructor: 'constructor',
      UniFfiItemKind.trait: 'trait',
      UniFfiItemKind.traitMethod: 'traitMethod',
      UniFfiItemKind.externalTrait: 'trait',
    }[kind];
    if (kindName == null) {
      throw FormatException('Unsupported metadata item $kind');
    }

    final result = <String, dynamic>{
      'kind': kindName,
      'module': module,
      'name': name,
      'original': original,
    };
    if (owner != null) {
      result['owner'] = owner;
    }
    if (index != null) {
      result['index'] = index;
    }
    if ({
      UniFfiItemKind.function,
      UniFfiItemKind.method,
      UniFfiItemKind.constructor,
      UniFfiItemKind.traitMethod,
    }.contains(kind)) {
      result['async'] = boolean();
      result['inputs'] = inputs();
      if (bytes[position] == UniFfiTypeCode.result) {
        u8();
        result['return'] = type();
        result['throws'] = type();
      } else {
        result['return'] = type();
      }
    }
    if (kind == UniFfiItemKind.record) {
      result['fields'] = fields();
    }
    if (kind == UniFfiItemKind.enumeration) {
      final shape = u8();
      result['shape'] = shape;
      result['discriminantType'] = boolean() ? type() : null;
      result['variants'] = List.generate(u8(), (_) {
        final variantName = string();
        final originalVariantName = optionalString();
        if (shape != 2 && boolean()) {
          literal();
        }
        final variantFields = shape == 2 ? <Json>[] : fields();
        return {
          'name': variantName,
          'original': originalVariantName,
          'fields': variantFields,
          'doc': doc(),
        };
      });
      result['nonExhaustive'] = boolean();
    }
    result['doc'] = doc();
    if (kind == UniFfiItemKind.trait || kind == UniFfiItemKind.externalTrait) {
      result['traitKind'] = u8();
    }
    if ({
      UniFfiItemKind.function,
      UniFfiItemKind.method,
      UniFfiItemKind.constructor,
      UniFfiItemKind.traitMethod,
    }.contains(kind)) {
      result['checksum'] = checksum(
        Uint8List.sublistView(bytes, 0, position),
      );
    }
    return result;
  }

  static int checksum(Uint8List bytes) {
    // Explicit BigInt arithmetic preserves unsigned 64-bit FNV overflow in Dart.
    var hash = BigInt.parse('cbf29ce484222325', radix: 16);
    final prime = BigInt.parse('100000001b3', radix: 16);
    final mask = (BigInt.one << 64) - BigInt.one;
    for (final byte in bytes) {
      hash = ((hash ^ BigInt.from(byte)) * prime) & mask;
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
    final items = <Json>[];
    final envelopes = <Json>[];
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
        var offset = 0;
        while (offset < section.length) {
          if (section[offset] == 0) {
            offset++;
            continue;
          }
          if (section.length - offset < 16 ||
              ascii.decode(
                    Uint8List.sublistView(section, offset, offset + 8),
                    allowInvalid: true,
                  ) !=
                  'BFFIMD01') {
            throw FormatException(
              '${image.origin}: invalid BoltFFI metadata frame at $offset',
            );
          }
          final length = ByteData.sublistView(section)
              .getUint64(offset + 8, Endian.little);
          if (length <= 0 || length > section.length - offset - 16) {
            throw FormatException('Invalid BoltFFI metadata length at $offset');
          }
          final value = jsonDecode(
            utf8.decode(
              Uint8List.sublistView(section, offset + 16, offset + 16 + length),
            ),
          ) as Json;
          if (value['magic'] != 'boltffi.bindings' || value['format'] is! Map) {
            throw const FormatException('Invalid BoltFFI envelope');
          }
          final format = value['format'] as Map;
          if (format['major'] != 0 || format['minor'] != 1) {
            throw FormatException(
              'Unsupported BoltFFI metadata version: $format',
            );
          }
          final key = jsonEncode([value['package'], value['surface']]);
          final canonical = jsonEncode(value);
          if (seen.containsKey(key) && seen[key] != canonical) {
            throw FormatException('Conflicting BoltFFI envelope: $key');
          }
          if (!seen.containsKey(key)) {
            envelopes.add(value);
            seen[key] = canonical;
          }
          offset += 16 + length;
        }
      }
    }
    items.sort(
      (a, b) => jsonEncode([
        a['module'],
        a['kind'],
        a['owner'],
        a['name'],
      ]).compareTo(
        jsonEncode([b['module'], b['kind'], b['owner'], b['name']]),
      ),
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
