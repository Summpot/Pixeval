import 'package:uniffi_dart_build/uniffi_dart_build.dart';

String _spelling(dynamic value) => value is String
    ? value
    : value['source_spelling'] as String? ??
          value['name'] as String? ??
          (value['parts'] as List).join('_');

/// Add declarations that only exist in BoltFFI. Semantic information is derived
/// from the binding contract; native signatures remain in the original IR.
NativeMetadata withBoltSemantics(NativeMetadata metadata) {
  final items = metadata.uniffi
      .map((i) => <String, dynamic>{...i, 'has_uniffi': true})
      .toList();
  final envelopes = <Json>[];
  final components =
      items
          .map((i) => (i['module'] as String).split('::').first)
          .toSet()
          .toList()
        ..sort((a, b) => b.length.compareTo(a.length));
  for (final envelope in metadata.boltffi) {
    final component = _spelling(envelope['package']['name']);
    String module(String symbol) {
      for (final c in components) {
        if (symbol.contains('_${c}_')) return c;
      }
      return component;
    }

    final decls = (envelope['bindings']['Native']['decls'] as List)
        .cast<Json>();
    final ids = <String, Json>{};
    final raw = <String, Json>{};
    for (final entry in decls) {
      final kind = entry.keys.single;
      if (!{'Record', 'Enum', 'Class', 'Callback'}.contains(kind)) continue;
      var value = entry[kind] as Json;
      String? shape;
      if (kind == 'Record' || kind == 'Enum') {
        shape = value.keys.single;
        value = value[shape] as Json;
      }
      final n = _spelling(value['name']);
      var existing = items
          .where(
            (i) =>
                i['name'] == n &&
                {'record', 'enum', 'object', 'callback'}.contains(i['kind']),
          )
          .toList();
      if (existing.length > 1 && kind == 'Record') {
        final fields = (value['fields'] as List)
            .cast<Json>()
            .map((f) => _spelling(f['key']['Named']))
            .join(',');
        existing = existing
            .where(
              (i) =>
                  (i['fields'] as List? ?? [])
                      .map((f) => f['name'])
                      .join(',') ==
                  fields,
            )
            .toList();
      }
      if (existing.length > 1 && kind == 'Enum') {
        final variants = (value['variants'] as List)
            .cast<Json>()
            .map((v) => _spelling(v['name']))
            .join(',');
        existing = existing
            .where(
              (i) =>
                  (i['variants'] as List? ?? [])
                      .map((v) => v['name'])
                      .join(',') ==
                  variants,
            )
            .toList();
      }
      if (existing.length > 1)
        throw FormatException('Ambiguous BoltFFI type $n across components');
      final semantic = existing.isEmpty
          ? <String, dynamic>{
              'kind': {
                'Record': 'record',
                'Enum': 'enum',
                'Class': 'object',
                'Callback': 'callback',
              }[kind],
              'module': kind == 'Class'
                  ? module(_spelling(value['release']))
                  : component,
              'name': n,
              'has_uniffi': false,
              if (kind == 'Enum')
                'shape': value['role'] == 'ErrorPayload' ? 1 : 0,
              if (kind == 'Record') 'fields': <Json>[],
              if (kind == 'Enum') 'variants': <Json>[],
            }
          : existing.single;
      if (existing.isEmpty) items.add(semantic);
      ids['$kind:${value['id']}'] = semantic;
      raw['$kind:${value['id']}'] = {...value, 'shape': shape};
    }
    Json type(dynamic value) {
      if (value is String) {
        return {
          'kind': switch (value) {
            'String' => 'string',
            'Bytes' => 'bytes',
            'Void' => 'void',
            _ => value.toLowerCase(),
          },
        };
      }
      final map = value as Json, key = map.keys.single, v = map[key];
      if (key == 'Primitive') return type(v);
      if ({'Record', 'Enum', 'Class', 'Callback'}.contains(key)) {
        final target = ids['$key:$v'];
        if (target == null)
          throw FormatException('Missing BoltFFI type $value');
        return {
          'kind': target['kind'],
          'module': target['module'],
          'name': target['name'],
        };
      }
      if ({'Optional', 'Sequence', 'Set'}.contains(key)) {
        final inner = type(v);
        return key == 'Sequence' && inner['kind'] == 'u8'
            ? {'kind': 'bytes'}
            : {
                'kind': {
                  'Optional': 'option',
                  'Sequence': 'sequence',
                  'Set': 'set',
                }[key],
                'inner': inner,
              };
      }
      if (key == 'Map')
        return {
          'kind': 'map',
          'key': type(v['key']),
          'value': type(v['value']),
        };
      throw FormatException('Unsupported BoltFFI semantic type $value');
    }

    Json field(Json f) {
      final key = f['key'];
      final name = key is Map && key['Named'] != null
          ? _spelling(key['Named'])
          : 'field${key['Unnamed']}';
      return {'name': name, 'type': type(f['ty'])};
    }

    for (final key in ids.keys) {
      final semantic = ids[key]!;
      if (semantic['has_uniffi'] == true) continue;
      final value = raw[key]!;
      if (semantic['kind'] == 'record')
        semantic['fields'] = (value['fields'] as List)
            .cast<Json>()
            .map(field)
            .toList();
      if (semantic['kind'] == 'enum') {
        semantic['variants'] = (value['variants'] as List).cast<Json>().map((
          variant,
        ) {
          final payload = variant['payload'];
          final fields = payload is Map
              ? (payload['Struct'] ?? payload['Tuple'] ?? <Json>[])
              : <Json>[];
          return {
            'name': _spelling(variant['name']),
            'fields': (fields as List).cast<Json>().map(field).toList(),
          };
        }).toList();
      }
    }
    Json parameter(Json param) {
      final payload = param['payload']['Value'] as Json?;
      if (payload == null)
        throw FormatException('Unsupported callback parameter: $param');
      Json t;
      if (payload['Direct'] != null) {
        t = type(payload['Direct']['ty']);
      } else if (payload['Encoded'] != null) {
        t = type(payload['Encoded']['ty']);
      } else if (payload['DirectVec'] != null) {
        final inner = type(payload['DirectVec']['element']);
        t = inner['kind'] == 'u8'
            ? {'kind': 'bytes'}
            : {'kind': 'sequence', 'inner': inner};
      } else if (payload['ScalarOption'] != null) {
        t = {
          'kind': 'option',
          'inner': type({'Primitive': payload['ScalarOption']['primitive']}),
        };
      } else if (payload['Handle'] != null) {
        t = type(payload['Handle']['target']);
      } else {
        throw FormatException('Unsupported BoltFFI parameter: $param');
      }
      return {
        'name': _spelling(param['name']),
        'type': t,
        'byRef': false,
        'default': null,
      };
    }

    Json returns(dynamic plan) {
      if (plan == 'Void') return {'kind': 'void'};
      final p = plan as Json, kind = p.keys.single, v = p.values.single;
      if (kind.startsWith('Handle')) return type(v['target']);
      if (kind.startsWith('DirectVec'))
        return {'kind': 'sequence', 'inner': type(v['element'])};
      if (kind.startsWith('ScalarOption'))
        return {
          'kind': 'option',
          'inner': type({'Primitive': v['primitive']}),
        };
      return type(v['ty']);
    }

    void function(Json value, {Json? owner, bool constructor = false}) {
      final symbol = _spelling(value['symbol'] ?? value['target']);
      final n = _spelling(value['name']);
      final c = owner == null
          ? module(symbol)
          : (owner['module'] as String).split('::').first;
      if (items.any(
        (i) =>
            {'function', 'constructor', 'method'}.contains(i['kind']) &&
            (i['module'] as String).split('::').first == c &&
            i['name'] == n &&
            i['owner'] == owner?['name'],
      ))
        return;
      final call = value['callable'] as Json;
      final error = call['error'];
      items.add({
        'kind': owner == null
            ? 'function'
            : constructor
            ? 'constructor'
            : 'method',
        'module': c,
        'name': n,
        if (owner != null) 'owner': owner['name'],
        'async': call['execution'] is Map,
        'inputs': (call['params'] as List).cast<Json>().map(parameter).toList(),
        'return': returns(call['returns']['plan']),
        if (error is Map) 'throws': type((error.values.single as Json)['ty']),
        'has_uniffi': false,
        'bolt_symbol': symbol,
      });
    }

    for (final entry in decls) {
      if (entry['Function'] != null) function(entry['Function'] as Json);
      if (entry['Class'] != null) {
        final value = entry['Class'] as Json,
            owner = ids['Class:${value['id']}']!;
        for (final init in (value['initializers'] as List).cast<Json>()) {
          function(init, owner: owner, constructor: true);
        }
        for (final method in (value['methods'] as List).cast<Json>()) {
          function(method, owner: owner);
        }
      }
    }
    envelopes.add({
      ...envelope,
      'dart_type_map': {
        for (final entry in ids.entries)
          entry.key: {
            'module': entry.value['module'],
            'name': entry.value['name'],
            'kind': entry.value['kind'],
          },
      },
    });
  }
  return NativeMetadata(items, envelopes, metadata.exports);
}
