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
      .map((item) => <String, dynamic>{...item, 'has_uniffi': true})
      .toList();
  final envelopes = <Json>[];
  final components =
      items
          .map((item) => (item['module'] as String).split('::').first)
          .toSet()
          .toList()
        ..sort((a, b) => b.length.compareTo(a.length));

  for (final envelope in metadata.boltffi) {
    final component = _spelling(envelope['package']['name']);
    String module(String symbol) {
      for (final candidate in components) {
        if (symbol.contains('_${candidate}_')) {
          return candidate;
        }
      }
      return component;
    }

    final decls = (envelope['bindings']['Native']['decls'] as List)
        .cast<Json>();
    final ids = <String, Json>{};
    final raw = <String, Json>{};
    for (final entry in decls) {
      final kind = entry.keys.single;
      if (!{'Record', 'Enum', 'Class', 'Callback'}.contains(kind)) {
        continue;
      }
      var value = entry[kind] as Json;
      String? shape;
      if (kind == 'Record' || kind == 'Enum') {
        shape = value.keys.single;
        value = value[shape] as Json;
      }
      final typeName = _spelling(value['name']);
      var existing = items
          .where(
            (item) =>
                item['name'] == typeName &&
                {'record', 'enum', 'object', 'callback'}.contains(item['kind']),
          )
          .toList();
      if (existing.length > 1 && kind == 'Record') {
        final fieldNames = (value['fields'] as List)
            .cast<Json>()
            .map((field) => _spelling(field['key']['Named']))
            .join(',');
        existing = existing
            .where(
              (item) =>
                  (item['fields'] as List? ?? [])
                      .map((field) => field['name'])
                      .join(',') ==
                  fieldNames,
            )
            .toList();
      }
      if (existing.length > 1 && kind == 'Enum') {
        final variantNames = (value['variants'] as List)
            .cast<Json>()
            .map((variant) => _spelling(variant['name']))
            .join(',');
        existing = existing
            .where(
              (item) =>
                  (item['variants'] as List? ?? [])
                      .map((variant) => variant['name'])
                      .join(',') ==
                  variantNames,
            )
            .toList();
      }
      if (existing.length > 1) {
        throw FormatException(
          'Ambiguous BoltFFI type $typeName across components',
        );
      }
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
              'name': typeName,
              'has_uniffi': false,
              if (kind == 'Enum')
                'shape': value['role'] == 'ErrorPayload' ? 1 : 0,
              if (kind == 'Record') 'fields': <Json>[],
              if (kind == 'Enum') 'variants': <Json>[],
            }
          : existing.single;
      if (existing.isEmpty) {
        items.add(semantic);
      }
      ids['$kind:${value['id']}'] = semantic;
      raw['$kind:${value['id']}'] = {...value, 'shape': shape};
    }

    Json resolveType(dynamic value) {
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
      final map = value as Json;
      final key = map.keys.single;
      final innerValue = map[key];
      if (key == 'Primitive') {
        return resolveType(innerValue);
      }
      if ({'Record', 'Enum', 'Class', 'Callback'}.contains(key)) {
        final target = ids['$key:$innerValue'];
        if (target == null) {
          throw FormatException('Missing BoltFFI type $value');
        }
        return {
          'kind': target['kind'],
          'module': target['module'],
          'name': target['name'],
        };
      }
      if ({'Optional', 'Sequence', 'Set'}.contains(key)) {
        final innerType = resolveType(innerValue);
        return key == 'Sequence' && innerType['kind'] == 'u8'
            ? {'kind': 'bytes'}
            : {
                'kind': {
                  'Optional': 'option',
                  'Sequence': 'sequence',
                  'Set': 'set',
                }[key],
                'inner': innerType,
              };
      }
      if (key == 'Map') {
        return {
          'kind': 'map',
          'key': resolveType(innerValue['key']),
          'value': resolveType(innerValue['value']),
        };
      }
      throw FormatException('Unsupported BoltFFI semantic type $value');
    }

    Json parseField(Json fieldJson) {
      final key = fieldJson['key'];
      final fieldName = key is Map && key['Named'] != null
          ? _spelling(key['Named'])
          : 'field${key['Unnamed']}';
      return {'name': fieldName, 'type': resolveType(fieldJson['ty'])};
    }

    for (final key in ids.keys) {
      final semantic = ids[key]!;
      if (semantic['has_uniffi'] == true) {
        continue;
      }
      final value = raw[key]!;
      if (semantic['kind'] == 'record') {
        semantic['fields'] = (value['fields'] as List)
            .cast<Json>()
            .map(parseField)
            .toList();
      }
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
            'fields': (fields as List).cast<Json>().map(parseField).toList(),
          };
        }).toList();
      }
    }

    Json parseParameter(Json param) {
      final payload = param['payload']['Value'] as Json?;
      if (payload == null) {
        throw FormatException('Unsupported callback parameter: $param');
      }
      Json paramType;
      if (payload['Direct'] != null) {
        paramType = resolveType(payload['Direct']['ty']);
      } else if (payload['Encoded'] != null) {
        paramType = resolveType(payload['Encoded']['ty']);
      } else if (payload['DirectVec'] != null) {
        final inner = resolveType(payload['DirectVec']['element']);
        paramType = inner['kind'] == 'u8'
            ? {'kind': 'bytes'}
            : {'kind': 'sequence', 'inner': inner};
      } else if (payload['ScalarOption'] != null) {
        paramType = {
          'kind': 'option',
          'inner': resolveType({
            'Primitive': payload['ScalarOption']['primitive'],
          }),
        };
      } else if (payload['Handle'] != null) {
        paramType = resolveType(payload['Handle']['target']);
      } else {
        throw FormatException('Unsupported BoltFFI parameter: $param');
      }
      return {
        'name': _spelling(param['name']),
        'type': paramType,
        'byRef': false,
        'default': null,
      };
    }

    Json parseReturns(dynamic plan) {
      if (plan == 'Void') {
        return {'kind': 'void'};
      }
      final planMap = plan as Json;
      final planKind = planMap.keys.single;
      final planValue = planMap.values.single;
      if (planKind.startsWith('Handle')) {
        return resolveType(planValue['target']);
      }
      if (planKind.startsWith('DirectVec')) {
        return {
          'kind': 'sequence',
          'inner': resolveType(planValue['element']),
        };
      }
      if (planKind.startsWith('ScalarOption')) {
        return {
          'kind': 'option',
          'inner': resolveType({'Primitive': planValue['primitive']}),
        };
      }
      return resolveType(planValue['ty']);
    }

    void registerFunction(
      Json value, {
      Json? owner,
      bool constructor = false,
    }) {
      final symbol = _spelling(value['symbol'] ?? value['target']);
      final name = _spelling(value['name']);
      final comp = owner == null
          ? module(symbol)
          : (owner['module'] as String).split('::').first;
      if (items.any(
        (item) =>
            {'function', 'constructor', 'method'}.contains(item['kind']) &&
            (item['module'] as String).split('::').first == comp &&
            item['name'] == name &&
            item['owner'] == owner?['name'],
      )) {
        return;
      }
      final call = value['callable'] as Json;
      final error = call['error'];
      items.add({
        'kind': owner == null
            ? 'function'
            : constructor
            ? 'constructor'
            : 'method',
        'module': comp,
        'name': name,
        if (owner != null) 'owner': owner['name'],
        'async': call['execution'] is Map,
        'inputs':
            (call['params'] as List).cast<Json>().map(parseParameter).toList(),
        'return': parseReturns(call['returns']['plan']),
        if (error is Map)
          'throws': resolveType((error.values.single as Json)['ty']),
        'has_uniffi': false,
        'bolt_symbol': symbol,
      });
    }

    for (final entry in decls) {
      if (entry['Function'] != null) {
        registerFunction(entry['Function'] as Json);
      }
      if (entry['Class'] != null) {
        final value = entry['Class'] as Json;
        final owner = ids['Class:${value['id']}']!;
        for (final init in (value['initializers'] as List).cast<Json>()) {
          registerFunction(init, owner: owner, constructor: true);
        }
        for (final method in (value['methods'] as List).cast<Json>()) {
          registerFunction(method, owner: owner);
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
