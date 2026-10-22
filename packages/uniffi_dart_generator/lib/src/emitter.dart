import 'dart:convert';

import 'package:uniffi_dart_build/uniffi_dart_build.dart';

import 'semantic.dart';

String words(dynamic value) => value is String
    ? value
    : value['source_spelling'] as String? ??
          value['name'] as String? ??
          (value['parts'] as List).join('_');
String pascal(String value) => value
    .split(RegExp('[^a-zA-Z0-9]+'))
    .where((s) => s.isNotEmpty)
    .map((s) => s[0].toUpperCase() + s.substring(1))
    .join();
String camel(String value) {
  final s = pascal(value);
  return safe(s[0].toLowerCase() + s.substring(1));
}

String safe(String name) =>
    {
      'new',
      'class',
      'enum',
      'switch',
      'default',
      'case',
      'return',
      'this',
      'super',
      'is',
      'as',
      'in',
      'with',
      'void',
      'final',
      'const',
      'var',
      'if',
      'else',
      'throw',
      'rethrow',
      'try',
      'catch',
      'finally',
      'for',
      'while',
      'do',
      'break',
      'continue',
      'assert',
      'extends',
      'implements',
      'mixin',
      'null',
      'true',
      'false',
      'operator',
      'factory',
      'static',
      'external',
      'get',
      'set',
      'late',
      'required',
      'covariant',
    }.contains(name)
    ? '${name}Value'
    : name;
String crate(Json item) => (item['module'] as String).split('::').first;
String typeKey(Json t) => '${crate(t)}::${t['name']}';

/// Semantic values are shared, but each callable keeps its complete ABI plan.
final class DartBindingsEmitter {
  DartBindingsEmitter(NativeMetadata source, this.config, this.stem)
    : metadata = withBoltSemantics(source) {
    for (final item in metadata.uniffi) {
      if ({
        'record',
        'enum',
        'object',
        'trait',
        'callback',
      }.contains(item['kind'])) {
        final key=typeKey(item);
        final previous=types[key];
        if(previous!=null && jsonEncode(previous)!=jsonEncode(item)) {
          throw FormatException('Conflicting type declaration $key');
        }
        types[key] = item;
      }
    }
    for (final envelope in metadata.boltffi) {
      final declarations = (envelope['bindings']['Native']['decls'] as List)
          .cast<Json>();
      final ids = <String, Json>{};
      for (final declaration in declarations) {
        final kind = declaration.keys.single;
        var value = declaration[kind] as Json;
        if (kind == 'Record' || kind == 'Enum') {
          final shape = value.keys.single;
          value = value[shape] as Json;
          value = {...value, 'shape': shape};
        }
        if ({'Record', 'Enum', 'Class', 'Callback'}.contains(kind)) {
          final mapping =
              (envelope['dart_type_map'] as Json)['$kind:${value['id']}']
                  as Json;
          ids['$kind:${value['id']}'] = {
            ...value,
            'declKind': kind,
            'module': mapping['module'],
          };
        }
      }
      for (final declaration in declarations) {
        if (declaration.containsKey('Stream'))
          streams.add({'declaration': declaration['Stream'], 'ids': ids});
        if (declaration.containsKey('Function')) {
          final f = declaration['Function'] as Json;
          boltFunctions[(f['symbol'] as Json)['name'] as String] = {
            'declaration': f,
            'ids': ids,
          };
        }
      }
      for (final item in ids.values) {
        final name = words(item['name']);
        final matches = types.entries
            .where(
              (e) =>
                  e.key ==
                  '${(item['module'] as String).split('::').first}::$name',
            )
            .toList();
        if (matches.length == 1) {
          item['module'] = matches.single.value['module'];
          boltTypes[matches.single.key] = {'declaration': item, 'ids': ids};
        }
        if (item['declKind'] == 'Class') {
          for (final init in (item['initializers'] as List).cast<Json>()) {
            boltFunctions[(init['symbol'] as Json)['name'] as String] = {
              'declaration': init,
              'ids': ids,
              'owner': item,
              'constructor': true,
            };
          }
          for (final method in (item['methods'] as List).cast<Json>()) {
            boltFunctions[(method['target'] as Json)['name'] as String] = {
              'declaration': {...method, 'symbol': method['target']},
              'ids': ids,
              'owner': item,
            };
          }
        }
      }
    }
    final components = (config['components'] as List?)?.cast<String>();
    functions = metadata.uniffi
        .where(
          (i) =>
              {'function', 'method', 'constructor'}.contains(i['kind']) &&
              (components == null || components.contains(crate(i))),
        )
        .toList();
    final exclude = (config['exclude'] as List? ?? []).cast<String>().toSet();
    functions.removeWhere((f) => exclude.contains(qualified(f)));
    for (final f in functions) {
      for (final input in (f['inputs'] as List).cast<Json>()) {
        visit(input['type'] as Json);
      }
      visit(f['return'] as Json);
      if (f['throws'] != null) visit(f['throws'] as Json);
      if (f['owner'] != null)
        visit({'kind': 'object', 'module': f['module'], 'name': f['owner']});
    }
    for (final entry in streams.toList()) {
      final declaration = entry['declaration'] as Json,
          ids = entry['ids'] as Json;
      final ownerId = declaration['owner'];
      final owner = ownerId == null ? null : _boltType({'Class': ownerId}, ids);
      final item = declaration['item'] as Json,
          plan = item.values.single as Json;
      final type = _boltType(plan['ty'], ids);
      final component = owner == null
          ? words((metadata.boltffi.first)['package']['name'])
          : crate(owner);
      if (components != null && !components.contains(component)) {
        streams.remove(entry);
        continue;
      }
      entry['owner'] = owner;
      entry['type'] = type;
      entry['component'] = component;
      visit(type);
      if (owner != null) visit(owner);
    }
  }
  final NativeMetadata metadata;
  final Json config;
  final String stem;
  final types = <String, Json>{};
  final needed = <String>{};
  final boltTypes = <String, Json>{};
  final boltFunctions = <String, Json>{};
  final streams = <Json>[];
  final _symbols = <String, ({String native, String dart})>{};
  late final List<Json> functions;
  final main = StringBuffer(), uni = StringBuffer(), bolt = StringBuffer();
  String get className => config['class_name'] as String? ?? 'NativeBindings';
  String prefix(String component) =>
      (config['prefixes'] as Map?)?[component] as String? ?? pascal(component);
  String name(Json t) => _mappedName('${t['module']}::${t['name']}',safe('${prefix(crate(t))}${pascal(t['name'] as String)}'));
  String _mappedName(String key,String fallback) {
    final value=(config['names'] as Map?)?[key] as String? ?? fallback;
    if(!RegExp(r'^[a-zA-Z][a-zA-Z0-9_]*$').hasMatch(value) || safe(value)!=value)throw FormatException('Invalid Dart name $value for $key');
    return value;
  }
  String qualified(Json f) =>
      '${f['module']}::${f['owner'] == null ? '' : '${f['owner']}::'}${f['name']}';
  String methodName(Json f) => camel(
    '${f['kind'] == 'constructor' ? '${f['owner']}_' : ''}${f['name']}',
  );
  String functionName(Json f) => _mappedName(qualified(f),camel('${prefix(crate(f))}_${f['owner'] == null ? '' : '${f['owner']}_'}${f['name']}'));
  void visit(Json t) {
    switch (t['kind']) {
      case 'record':
      case 'enum':
      case 'object':
      case 'trait':
      case 'callback':
        final key = typeKey(t);
        if (!needed.add(key)) return;
        final declaration = types[key];
        if (declaration == null)
          throw FormatException('Missing cross-component type $key');
        for (final field
            in (declaration['fields'] as List? ?? []).cast<Json>()) {
          visit(field['type'] as Json);
        }
        for (final variant
            in (declaration['variants'] as List? ?? []).cast<Json>()) {
          for (final field in (variant['fields'] as List).cast<Json>()) {
            visit(field['type'] as Json);
          }
        }
      case 'option':
      case 'sequence':
      case 'set':
      case 'box':
      case 'custom':
        visit(t['inner'] as Json);
      case 'map':
        visit(t['key'] as Json);
        visit(t['value'] as Json);
    }
  }

  String dartType(Json t) => switch (t['kind']) {
    'void' => 'void',
    'bool' => 'bool',
    'f32' || 'f64' => 'double',
    'u64' => 'BigInt',
    'u8' || 'u16' || 'u32' || 'i8' || 'i16' || 'i32' || 'i64' => 'int',
    'string' => 'String',
    'bytes' => 'Uint8List',
    'timestamp' => 'DateTime',
    'duration' => 'Duration',
    'option' => '${dartType(t['inner'] as Json)}?',
    'box' || 'custom' => dartType(t['inner'] as Json),
    'sequence' => 'List<${dartType(t['inner'] as Json)}>',
    'set' => 'Set<${dartType(t['inner'] as Json)}>',
    'map' =>
      'Map<${dartType(t['key'] as Json)}, ${dartType(t['value'] as Json)}>',
    'record' || 'enum' || 'object' || 'trait' => name(t),
    _ => throw FormatException('Unsupported Dart type: $t'),
  };
  bool scalar(Json t) => {
    'u8',
    'u16',
    'u32',
    'u64',
    'i8',
    'i16',
    'i32',
    'i64',
    'f32',
    'f64',
    'bool',
    'void',
  }.contains(t['kind']);
  String nativeType(Json t, {bool uniffi = false}) => switch (t['kind']) {
    'enum' when !uniffi && isCStyle(t) => nativeType({
      'kind': (boltTypes[typeKey(t)]!['declaration']['repr'] as String)
          .toLowerCase(),
    }),
    'void' => 'Void',
    'bool' => uniffi ? 'Int8' : 'Uint8',
    'u8' => 'Uint8',
    'u16' => 'Uint16',
    'u32' => 'Uint32',
    'u64' => 'Uint64',
    'i8' => 'Int8',
    'i16' => 'Int16',
    'i32' => 'Int32',
    'i64' => 'Int64',
    'f32' => 'Float',
    'f64' => 'Double',
    'object' || 'trait' => uniffi ? 'Pointer<Void>' : 'Uint64',
    _ =>
      uniffi
          ? 'RustBuffer'
          : (isDirect(t) ? '_Direct${name(t)}' : 'BoltBuffer'),
  };
  String ffiDart(Json t, {bool uniffi = false}) => switch (t['kind']) {
    'enum' when !uniffi && isCStyle(t) => 'int',
    'void' => 'void',
    'f32' || 'f64' => 'double',
    'bool' ||
    'u8' ||
    'u16' ||
    'u32' ||
    'u64' ||
    'i8' ||
    'i16' ||
    'i32' ||
    'i64' => 'int',
    'object' || 'trait' => uniffi ? 'Pointer<Void>' : 'int',
    _ => nativeType(t, uniffi: uniffi),
  };
  bool isDirect(Json t) =>
      t['kind'] == 'record' &&
      boltTypes[typeKey(t)]?['declaration']['shape'] == 'Direct';
  bool isCStyle(Json t) =>
      t['kind'] == 'enum' &&
      boltTypes[typeKey(t)]?['declaration']['shape'] == 'CStyle';

  void _validateDirectLayout(Json t) {
    final declaration=boltTypes[typeKey(t)]!['declaration'] as Json;
    final layout=declaration['layout'] as Json;
    var offset=0,alignment=1;
    final fields=(types[typeKey(t)]!['fields'] as List).cast<Json>();
    if(fields.isEmpty)throw FormatException('Empty direct record ${typeKey(t)} cannot be represented by Dart FFI');
    for(final field in fields) {
      final type=field['type'] as Json;
      final size=switch(type['kind']) {'bool'||'u8'||'i8'=>1,'u16'||'i16'=>2,'u32'||'i32'||'f32'=>4,'u64'||'i64'||'f64'=>8,_=>throw FormatException('Unsupported direct layout field ${typeKey(t)}::${field['name']}')};
      offset=(offset+size-1)~/size*size;
      final nativeField=(layout['fields'] as List).cast<Json>().singleWhere((f)=>words(f['key']['Named'])==field['name']);
      if(nativeField['offset']!=offset)throw FormatException('Unsupported native field offset for ${typeKey(t)}::${field['name']}: ${nativeField['offset']}, expected $offset');
      offset+=size;if(size>alignment)alignment=size;
    }
    final size=(offset+alignment-1)~/alignment*alignment;
    if(layout['size']!=size || layout['alignment']!=alignment)throw FormatException('Unsupported size/alignment for ${typeKey(t)}: $layout');
  }
  String _lookup(String symbol, String native, String dart) {
    final previous = _symbols[symbol];
    if (previous != null && previous != (native: native, dart: dart))
      throw FormatException('Conflicting ABI for $symbol');
    _symbols[symbol] = (native: native, dart: dart);
    return '_library.lookup<NativeFunction<$native>>(${jsonEncode(symbol)}).asFunction<$dart>()';
  }

  String _decode(Json t, String reader, {String engine = 'engine'}) {
    final kind = t['kind'];
    if (scalar(t))
      return kind == 'void'
          ? 'null'
          : '$reader.${kind == 'bool' ? 'boolean' : kind}()';
    return switch (kind) {
      'string' => '$reader.string()',
      'bytes' => '$reader.buffer()',
      'option' =>
        '$reader.optional(() => ${_decode(t['inner'] as Json, reader, engine: engine)})',
      'box' || 'custom' => _decode(t['inner'] as Json, reader, engine: engine),
      'sequence' =>
        '$reader.sequence(() => ${_decode(t['inner'] as Json, reader, engine: engine)})',
      'set' =>
        '$reader.set(() => ${_decode(t['inner'] as Json, reader, engine: engine)})',
      'map' =>
        '$reader.map(() => ${_decode(t['key'] as Json, reader, engine: engine)}, () => ${_decode(t['value'] as Json, reader, engine: engine)})',
      'record' || 'enum' => '_read${name(t)}($reader, this, $engine)',
      'object' ||
      'trait' => '${name(t)}._(this, $reader.u64().toInt(), $engine)',
      'timestamp' => 'readNativeTimestamp($reader)',
      'duration' => 'readNativeDuration($reader)',
      _ => throw FormatException('Unsupported decode type $t'),
    };
  }

  String _encode(
    Json t,
    String value,
    String writer, {
    String engine = 'engine',
  }) {
    final kind = t['kind'];
    if (scalar(t))
      return kind == 'void'
          ? ''
          : '$writer.${kind == 'bool' ? 'boolean' : kind}($value);';
    return switch (kind) {
      'string' => '$writer.string($value);',
      'bytes' => '$writer.buffer($value);',
      'option' =>
        '$writer.optional($value, (value) { ${_encode(t['inner'] as Json, 'value', writer, engine: engine)} });',
      'box' ||
      'custom' => _encode(t['inner'] as Json, value, writer, engine: engine),
      'sequence' || 'set' =>
        '$writer.sequence($value, (value) { ${_encode(t['inner'] as Json, 'value', writer, engine: engine)} });',
      'map' =>
        '$writer.map($value, (key) { ${_encode(t['key'] as Json, 'key', writer, engine: engine)} }, (value) { ${_encode(t['value'] as Json, 'value', writer, engine: engine)} });',
      'record' || 'enum' => '_write${name(t)}($writer, $value, this, $engine);',
      'timestamp' => 'writeNativeTimestamp($writer, $value);',
      'duration' => 'writeNativeDuration($writer, $value);',
      _ => throw FormatException('Unsupported wire input type $t'),
    };
  }

  String _codecRead(Json t) => _decode(t, 'r').replaceAll('this', 'ctx');
  String _codecWrite(Json t, String value) =>
      _encode(t, value, 'w').replaceAll('this', 'ctx');
  Map<String, String> generate() {
    final publicNames=<String,String>{};
    void register(String value,String key) {
      final previous=publicNames[value];
      if(previous!=null && previous!=key)throw FormatException('Dart name $value collides between $previous and $key; configure prefixes or names');
      publicNames[value]=key;
    }
    for(final key in needed) {register(name(types[key]!),key);}
    for(final f in functions) {register(functionName(f),qualified(f));}
    main.writeln(
      "// Generated by uniffi_dart_generator. Do not edit.\n// ignore_for_file: unused_import, unused_element, unused_field, unused_local_variable, camel_case_types, no_leading_underscores_for_local_identifiers, curly_braces_in_flow_control_structures\nimport 'dart:async';\nimport 'dart:convert';\nimport 'dart:ffi';\nimport 'dart:typed_data';\nimport 'package:ffi/ffi.dart';\nimport 'package:uniffi_dart/uniffi_dart.dart';\npart '$stem.uniffi.g.dart';\npart '$stem.boltffi.g.dart';",
    );
    uni.writeln(
      "// Generated.\n// ignore_for_file: unused_local_variable, no_leading_underscores_for_local_identifiers, curly_braces_in_flow_control_structures\npart of '$stem.g.dart';",
    );
    bolt.writeln(
      "// Generated.\n// ignore_for_file: unused_local_variable, no_leading_underscores_for_local_identifiers, curly_braces_in_flow_control_structures\npart of '$stem.g.dart';",
    );
    for (final key in needed.toList()..sort()) {
      _emitType(types[key]!);
    }
    main.writeln(
      'class $className {\n$className(NativeLibrary library, {this.engine}) : _library = library { _verify(); }\nfinal NativeLibrary _library;\nfinal NativeEngine? engine;\nlate final _scheduler = NativeFutureScheduler();\nfinal _runtimes = <String, UniFfiRuntime>{};\nUniFfiRuntime _runtime(String component) => _runtimes.putIfAbsent(component, () => UniFfiRuntime(_library, component));\nlate final _bolt = BoltFfiRuntime(_library);\n$className? _uniffiView, _boltffiView;\n$className get uniffi { _ensureOpen(); return _uniffiView ??= $className(_library, engine:NativeEngine.uniffi); }\n$className get boltffi { _ensureOpen(); return _boltffiView ??= $className(_library, engine:NativeEngine.boltffi); }\nbool _closing=false;\nFuture<void>? _disposing;\nfinal _handles=<WeakReference<NativeHandle>>[];\nvoid _track(NativeHandle handle) { if(_handles.length>1024)_handles.removeWhere((r)=>r.target==null); _handles.add(WeakReference(handle)); }\nvoid _ensureOpen() {if(_closing)throw StateError("Native bindings have been disposed");}\nFuture<void> dispose() => _disposing ??= () async { _closing=true; await Future.wait<void>([if(_uniffiView!=null)_uniffiView!.dispose(),if(_boltffiView!=null)_boltffiView!.dispose(),_scheduler.dispose(),for(final reference in _handles)if(reference.target!=null)reference.target!.dispose()]);_handles.clear(); }();',
    );
    final verifications = StringBuffer();
    final verified = <String>{};
    for (var index = 0; index < functions.length; index++) {
      final f = functions[index];
      if (f['has_uniffi'] != false) uni.writeln(_emitUni(f, index));
      final symbol = _boltSymbol(f);
      final b = boltFunctions[symbol];
      if (b != null) bolt.writeln(_emitBolt(f, b, index));
      _emitPublic(f, index, b);
      if (f['has_uniffi'] == false) continue;
      final check =
          'uniffi_${crate(f)}_checksum_${f['kind'] == 'function'
              ? 'func'
              : f['kind'] == 'method'
              ? 'method'
              : 'constructor'}_${f['owner'] == null ? '' : '${(f['owner'] as String).toLowerCase()}_'}${(f['name'] as String).toLowerCase()}';
      if (verified.add(check))
        verifications.writeln(
          "if (${_lookup(check, 'Uint16 Function()', 'int Function()')}() != ${f['checksum']}) throw const NativeInteropException('Checksum mismatch: $check');",
        );
      final component = crate(f);
      if (verified.add(component)) {
        final check = 'ffi_${component}_uniffi_contract_version';
        verifications.writeln(
          "if (${_lookup(check, 'Uint32 Function()', 'int Function()')}() != 30) throw const NativeInteropException('Unsupported UniFFI contract version for $component');",
        );
      }
    }
    for (final entry in streams) {
      main.writeln(_emitStream(entry));
    }
    main.writeln('void _verify() { $verifications }\n}');
    _emitResolvers();
    return {
      '$stem.g.dart': main.toString(),
      '$stem.uniffi.g.dart': uni.toString(),
      '$stem.boltffi.g.dart': bolt.toString(),
    };
  }

  String _boltSymbol(Json f) {
    if (f['bolt_symbol'] != null) return f['bolt_symbol'] as String;
    if (f['owner'] != null) {
      final owner =
          boltTypes[typeKey({
                'module': f['module'],
                'name': f['owner'],
              })]?['declaration']
              as Json?;
      if (owner != null) {
        final list =
            (owner[f['kind'] == 'constructor' ? 'initializers' : 'methods']
                    as List)
                .cast<Json>();
        for (final method in list) {
          if (words(method['name']) == f['name'])
            return words(method['symbol'] ?? method['target']);
        }
      }
      return '';
    }
    final scope = (f['module'] as String).replaceAll('::', '_');
    final symbol = 'boltffi_function_${scope}_${f['name']}';
    return boltFunctions.containsKey(symbol) ? symbol : '';
  }

  void _emitPublic(Json f, int index, Json? b) {
    final params = _publicParams(f), values = _values(f);
    final returnType = dartType(f['return'] as Json),
        async = f['async'] == true;
    final signature = async ? 'Future<$returnType>' : returnType;
    final method = functionName(f);
    final mode = config['engine'] as String? ?? 'auto';
    final overrides = config['overrides'] as Map? ?? {};
    final selected = overrides[qualified(f)] as String? ?? overrides[crate(f)] as String? ?? mode;
    if (selected == 'boltffi' && b == null)
      throw FormatException('Missing BoltFFI export for ${qualified(f)}');
    if (selected == 'uniffi' && f['has_uniffi'] == false)
      throw FormatException('Missing UniFFI export for ${qualified(f)}');
    if (!{'auto', 'dual', 'uniffi', 'boltffi'}.contains(selected))
      throw FormatException('Unknown engine mode $selected');
    final defaultBolt =
        f['has_uniffi'] == false ||
        b != null &&
            f['throws'] == null &&
            (f['inputs'] as List).every(
              (i) => scalar(i['type'] as Json) || isDirect(i['type'] as Json),
            ) &&
            (scalar(f['return'] as Json) || isDirect(f['return'] as Json));
    final dispatch = f['kind'] == 'method'
        ? 'receiver.engine'
        : selected == 'uniffi'
        ? 'NativeEngine.uniffi'
        : selected == 'boltffi'
        ? 'NativeEngine.boltffi'
        : "engine ?? NativeEngine.${defaultBolt ? 'boltffi' : 'uniffi'}";
    main.writeln(
      '$signature $method($params) {\n_ensureOpen();\nfinal selected=$dispatch;',
    );
    if(f['kind']=='method' && {'uniffi','boltffi'}.contains(selected)) {
      main.writeln('if(selected!=NativeEngine.$selected)throw ArgumentError("The configured engine differs from the object owner");');
    }
    main.writeln(
      'if (selected == NativeEngine.boltffi) { ${b == null ? "throw UnsupportedError('Missing BoltFFI export: ${qualified(f)}');" : "return _b$index(this, $values);"} }\n${f['has_uniffi'] == false ? "throw UnsupportedError('Missing UniFFI export: ${qualified(f)}');" : "return _u$index(this, $values);"}\n}',
    );
  }

  String _publicParams(Json f) {
    final ps = <String>[];
    if (f['kind'] == 'method')
      ps.add(
        'required ${name({'module': f['module'], 'name': f['owner']})} receiver',
      );
    for (final input in (f['inputs'] as List).cast<Json>()) {
      final t = input['type'] as Json;
      ps.add(
        '${t['kind'] == 'option' ? '' : 'required '}${dartType(t)} ${camel(input['name'] as String)}',
      );
    }
    if (f['async'] == true)
      ps.add('NativeCancellationToken? cancellationToken');
    return ps.isEmpty ? '' : '{${ps.join(', ')}}';
  }

  String _values(Json f) => [
    if (f['kind'] == 'method') 'receiver: receiver',
    for (final p in (f['inputs'] as List).cast<Json>())
      '${camel(p['name'] as String)}: ${camel(p['name'] as String)}',
    if (f['async'] == true) 'cancellationToken: cancellationToken',
  ].join(', ');
  void _emitType(Json t) {
    final n = name(t), kind = t['kind'];
    if (kind == 'callback')
      throw FormatException(
        'Synchronous callback ${typeKey(t)} cannot be bound safely; expose a native async receiver',
      );
    if (kind == 'object' || kind == 'trait') {
      main.writeln(
        'class $n {\n$n._(this._ctx, int address, this.engine) : _handle=NativeHandle(address, engine, _ctx._library, (value) {',
      );
      final component = crate(t), lower = (t['name'] as String).toLowerCase();
      final free = t['has_uniffi'] == false
          ? ''
          : _lookup(
              'uniffi_${component}_fn_free_$lower',
              'Void Function(Pointer<Void>, Pointer<RustCallStatus>)',
              'void Function(Pointer<Void>, Pointer<RustCallStatus>)',
            );
      final boltInfo = boltTypes[typeKey(t)];
      main.writeln(
        t['has_uniffi'] == false
            ? "if(engine==NativeEngine.uniffi) { throw UnsupportedError('No UniFFI object export for $n'); }"
            : 'if(engine==NativeEngine.uniffi) { using((arena) { final status=arena<RustCallStatus>(); final _library=_ctx._library; $free(Pointer<Void>.fromAddress(value), status); _ctx._runtime(${jsonEncode(component)}).check(status.ref); }); }',
      );
      if (boltInfo != null) {
        final symbol = boltInfo['declaration']['release']['name'] as String;
        main.writeln(
          'else { final _library=_ctx._library; ${_lookup(symbol, 'Void Function(Uint64)', 'void Function(int)')}(value); }',
        );
      } else {
        main.writeln(
          "else { throw UnsupportedError('No BoltFFI object export for $n'); }",
        );
      }
      main.writeln(
        '}) { _ctx._track(_handle); }\nfinal $className _ctx;\nfinal NativeHandle _handle;\nfinal NativeEngine engine;\nFuture<void> dispose() => _handle.dispose();',
      );
      for (final method in functions.where(
        (f) =>
            f['kind'] == 'method' &&
            f['owner'] == t['name'] &&
            crate(f) == component,
      )) {
        final copy = {...method, 'kind': 'function'};
        final signature = method['async'] == true
            ? 'Future<${dartType(method['return'] as Json)}>'
            : dartType(method['return'] as Json);
        final args = _values(copy);
        main.writeln(
          '$signature ${methodName(method)}(${_publicParams(copy)}) => _ctx.${functionName(method)}(receiver:this${args.isEmpty ? '' : ', $args'});',
        );
      }
      for (final entry in streams.where(
        (s) => s['owner'] != null && typeKey(s['owner'] as Json) == typeKey(t),
      )) {
        main.writeln(
          'Stream<${dartType(entry['type'] as Json)}> ${camel(words(entry['declaration']['name']))}() => _ctx.${_streamName(entry)}(receiver:this);',
        );
      }
      main.writeln('}');
      return;
    }
    final fields = (t['fields'] as List? ?? []).cast<Json>();
    if (kind == 'record') {
      main.writeln(
        'class $n with NativeValue {\nconst $n({${fields.map((f) => '${f['type']['kind'] == 'option' ? '' : 'required '}this.${camel(f['name'] as String)}').join(', ')}});\n@override List<Object?> get nativeFields => [${fields.map((f) => camel(f['name'] as String)).join(', ')}];',
      );
      for (final field in fields) {
        main.writeln(
          'final ${dartType(field['type'] as Json)} ${camel(field['name'] as String)};',
        );
      }
      main.writeln('}');
      main.writeln(
        '$n _read$n(WireReader r, $className ctx, NativeEngine engine) => $n(${fields.map((f) => '${camel(f['name'] as String)}: ${_codecRead(f['type'] as Json)}').join(', ')});',
      );
      main.writeln(
        'void _write$n(WireWriter w, $n value, $className ctx, NativeEngine engine) { ${fields.map((f) => _codecWrite(f['type'] as Json, 'value.${camel(f['name'] as String)}')).join('\n')} }',
      );
      if (isDirect(t)) {
        _validateDirectLayout(t);
        bolt.writeln('final class _Direct$n extends Struct {');
        for (final field in fields) {
          final ft = field['type'] as Json;
          if (!scalar(ft))
            throw FormatException(
              'Nested direct layout is not yet supported: $n',
            );
          bolt.writeln(
            '@${nativeType(ft)}() external ${ffiDart(ft)} ${camel(field['name'] as String)};',
          );
        }
        bolt.writeln('}');
      }
      return;
    }
    if (kind != 'enum') throw FormatException('Unsupported declaration: $t');
    final variants = (t['variants'] as List).cast<Json>();
    final error = t['shape'] != 0;
    main.writeln(
      'sealed class $n ${error ? 'implements Exception' : ''} {\nconst $n();\nint get ordinal;',
    );
    for (final variant in variants) {
      final vn = pascal(variant['name'] as String),
          fields = (variant['fields'] as List).cast<Json>();
      main.writeln(
        'const factory $n.${camel(variant['name'] as String)}(${fields.isEmpty ? '' : '{${fields.map((f) => '${f['type']['kind'] == 'option' ? '' : 'required '}${dartType(f['type'] as Json)} ${camel(f['name'] as String)}').join(', ')}}'}) = ${n}_$vn;',
      );
    }
    main.writeln(
      'T when<T>({${variants.map((v) => 'required T Function(${(v['fields'] as List).cast<Json>().map((f) => '${dartType(f['type'] as Json)} ${camel(f['name'] as String)}').join(', ')}) ${camel(v['name'] as String)}').join(', ')}}) => switch(this) {',
    );
    for (final variant in variants) {
      final fields = (variant['fields'] as List).cast<Json>();
      main.writeln(
        '${n}_${pascal(variant['name'] as String)} value => ${camel(variant['name'] as String)}(${fields.map((f) => 'value.${camel(f['name'] as String)}').join(', ')}),',
      );
    }
    main.writeln('};\n}');
    for (final variant in variants) {
      final vn = '${n}_${pascal(variant['name'] as String)}',
          fields = (variant['fields'] as List).cast<Json>();
      main.writeln(
        'final class $vn extends $n with NativeValue { const $vn(${fields.isEmpty ? '' : '{${fields.map((f) => '${f['type']['kind'] == 'option' ? '' : 'required '}this.${camel(f['name'] as String)}').join(', ')}}'});\n@override int get ordinal => ${variants.indexOf(variant)};\n@override List<Object?> get nativeFields => [${fields.map((f) => camel(f['name'] as String)).join(', ')}];',
      );
      for (final f in fields) {
        main.writeln(
          'final ${dartType(f['type'] as Json)} ${camel(f['name'] as String)};',
        );
      }
      main.writeln('}');
    }
    final boltEnum = boltTypes[typeKey(t)]?['declaration'] as Json?;
    final cStyle = boltEnum?['shape'] == 'CStyle';
    final repr = cStyle ? (boltEnum!['repr'] as String).toLowerCase() : 'i32';
    final tags = <int>[
      for (var i = 0; i < variants.length; i++)
        cStyle
            ? ((boltEnum!['variants'] as List).cast<Json>().firstWhere(
                    (v) => words(v['name']) == variants[i]['name'],
                  )['discriminant']
                  as int)
            : i,
    ];
    if (cStyle) {
      main.writeln(
        'int _lower$n($n value) => switch(value) { ${[for (var i = 0; i < variants.length; i++) '${n}_${pascal(variants[i]['name'] as String)}() => ${tags[i]}'].join(', ')} };',
      );
      main.writeln(
        '$n _lift$n(int value) => switch(value) { ${[for (var i = 0; i < variants.length; i++) '${tags[i]} => $n.${camel(variants[i]['name'] as String)}()'].join(', ')}, _ => throw FormatException("Invalid $n discriminant: \$value") };',
      );
    }
    main.writeln(
      '$n _read$n(WireReader r, $className ctx, NativeEngine engine) {\nfinal tag=engine==NativeEngine.uniffi?r.i32():r.$repr();\nfinal variant=engine==NativeEngine.uniffi?tag-1:switch(tag) { ${[for (var i = 0; i < tags.length; i++) '${tags[i]} => $i'].join(', ')}, _ => -1 };\nreturn switch(variant) {',
    );
    for (var i = 0; i < variants.length; i++) {
      final v = variants[i], fields = (v['fields'] as List).cast<Json>();
      main.writeln(
        '$i => $n.${camel(v['name'] as String)}(${fields.map((f) => '${camel(f['name'] as String)}: ${_codecRead(f['type'] as Json)}').join(', ')}),',
      );
    }
    main.writeln("_ => throw FormatException('Unknown $n tag: \$tag'),\n};\n}");
    main.writeln(
      'void _write$n(WireWriter w, $n value, $className ctx, NativeEngine engine) { switch(value) {',
    );
    for (var i = 0; i < variants.length; i++) {
      final v = variants[i], fields = (v['fields'] as List).cast<Json>();
      main.writeln(
        'case ${n}_${pascal(v['name'] as String)} value: if(engine==NativeEngine.uniffi) {w.i32(${i + 1});} else {w.$repr(${tags[i]});} ${fields.map((f) => _codecWrite(f['type'] as Json, 'value.${camel(f['name'] as String)}')).join('\n')}',
      );
    }
    main.writeln('} }');
  }

  String _uniSymbol(Json f) =>
      'uniffi_${crate(f)}_fn_${f['kind'] == 'function'
          ? 'func'
          : f['kind'] == 'method'
          ? 'method'
          : 'constructor'}_${f['owner'] == null ? '' : '${(f['owner'] as String).toLowerCase()}_'}${(f['name'] as String).toLowerCase()}';
  String _streamName(Json entry) => camel(
    '${prefix(entry['component'] as String)}_${entry['owner'] == null ? '' : '${entry['owner']['name']}_'}${words(entry['declaration']['name'])}',
  );
  String _emitStream(Json entry) {
    final declaration = entry['declaration'] as Json,
        protocol = declaration['protocol'] as Json;
    final owner = entry['owner'] as Json?,
        t = entry['type'] as Json,
        item = declaration['item'] as Json;
    final out = StringBuffer(
      'Stream<${dartType(t)}> ${_streamName(entry)}(${owner == null ? '' : '{required ${name(owner)} receiver}'}) {\nfinal ctx=this;final _library=ctx._library;const engine=NativeEngine.boltffi;',
    );
    final subscribe = _lookup(
      words(protocol['subscribe']),
      'Uint64 Function(${owner == null ? '' : 'Uint64'})',
      'int Function(${owner == null ? '' : 'int'})',
    );
    final poll = _lookup(
      words(protocol['poll']),
      'Void Function(Uint64, Uint64, Pointer<NativeFunction<NativeWake>>)',
      'void Function(int, int, Pointer<NativeFunction<NativeWake>>)',
    );
    final unsubscribe = _lookup(
      words(protocol['unsubscribe']),
      'Void Function(Uint64)',
      'void Function(int)',
    );
    final free = _lookup(
      words(protocol['free']),
      'Void Function(Uint64)',
      'void Function(int)',
    );
    out.writeln(
      'return boltEventStream<${dartType(t)}>(subscribe:()=>$subscribe(${owner == null ? '' : 'receiver._handle.address'}),poll:$poll,unsubscribe:$unsubscribe,free:$free,${owner == null ? '' : 'owner:receiver._handle,libraryOwner:_library,'} readBatch:(handle) {',
    );
    if (item['Encoded'] != null) {
      final pop = _lookup(
        words(protocol['pop_batch']),
        'BoltBuffer Function(Uint64, UintPtr)',
        'BoltBuffer Function(int, int)',
      );
      out.writeln(
        'final bytes=ctx._bolt.lift($pop(handle,64)); if(bytes.isEmpty)return []; final r=WireReader(bytes,endian:Endian.little); final values=r.sequence(()=>${_decode(t, 'r')}); r.finish(); return values;',
      );
    } else if (item['Direct'] != null) {
      final nt = nativeType(t), dt = ffiDart(t);
      if (!scalar(t) && !isDirect(t))
        throw FormatException('Unsupported direct stream item $t');
      final pop = _lookup(
        words(protocol['pop_batch']),
        'UintPtr Function(Uint64, Pointer<$nt>, UintPtr)',
        'int Function(int, Pointer<$nt>, int)',
      );
      out.writeln(
        'return using((arena) {final output=arena<$nt>(64);final count=$pop(handle,output,64); if(count<0 || count>64) throw const NativeInteropException("Invalid stream batch count"); return List<${dartType(t)}>.generate(count,(i) {final $dt result=${scalar(t) ? 'output[i]' : '(output+i).ref'}; ${_liftBolt(t, 'result', encoded: false)} });});',
      );
    } else {
      throw FormatException('Unsupported stream item plan $item');
    }
    out.writeln('});\n}');
    return out
        .toString()
        .replaceAll('this', 'ctx')
        .replaceFirst('final ctx=ctx;', 'final ctx=this;');
  }

  String _emitUni(Json f, int index) {
    final out = StringBuffer(),
        async = f['async'] == true,
        ret = f['return'] as Json,
        component = crate(f);
    final publicParams = _publicParams(f);
    out.writeln(
      '${async ? 'Future<${dartType(ret)}>' : dartType(ret)} _u$index($className ctx${publicParams.isEmpty ? '' : ', $publicParams'}) ${async ? 'async' : ''} {\nfinal _library=ctx._library;\nfinal rt=ctx._runtime(${jsonEncode(component)});\nconst engine=NativeEngine.uniffi;\nfinal args=UniFfiArguments();\nfinal arena=Arena();',
    );
    final leases = <String>[];
    final values = <String>[], natives = <String>[], darts = <String>[];
    final inputs = <Json>[
      if (f['kind'] == 'method')
        {
          'name': 'receiver',
          'type': {'kind': 'object', 'module': f['module'], 'name': f['owner']},
        },
      ...(f['inputs'] as List).cast<Json>(),
    ];
    out.writeln('final leases=<NativeHandle>[]; try {');
    for (var i = 0; i < inputs.length; i++) {
      final input = inputs[i],
          t = input['type'] as Json,
          v = camel(input['name'] as String);
      natives.add(nativeType(t, uniffi: true));
      darts.add(ffiDart(t, uniffi: true));
      if (scalar(t)) {
        values.add(
          t['kind'] == 'bool'
              ? '($v ? 1 : 0)'
              : t['kind'] == 'u64'
              ? 'lowerNativeU64($v)'
              : v,
        );
      } else if (t['kind'] == 'object' || t['kind'] == 'trait') {
        final objectCrate = crate(t),
            lower = (t['name'] as String).toLowerCase();
        final clone = _lookup(
          'uniffi_${objectCrate}_fn_clone_$lower',
          'Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)',
          'Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)',
        );
        final free = _lookup(
          'uniffi_${objectCrate}_fn_free_$lower',
          'Void Function(Pointer<Void>, Pointer<RustCallStatus>)',
          'void Function(Pointer<Void>, Pointer<RustCallStatus>)',
        );
        out.writeln(
          'final lease$i=$v._handle.acquire(engine, ctx._library); leases.add($v._handle);',
        );
        leases.add(v);
        out.writeln(
          'final status$i=arena<RustCallStatus>(); final p$i=$clone(Pointer<Void>.fromAddress(lease$i),status$i); rt.check(status$i.ref);\nargs.own(() { $free(p$i,status$i); rt.check(status$i.ref); });',
        );
        values.add('p$i');
      } else {
        if (t['kind'] == 'string')
          out.writeln('final bytes$i=Uint8List.fromList(utf8.encode($v));');
        else {
          out.writeln(
            'final writer$i=WireWriter(); ${_encode(t, v, 'writer$i')} final bytes$i=writer$i.takeBytes();',
          );
        }
        out.writeln('final p$i=args.buffer(rt, bytes$i);');
        values.add('p$i');
      }
    }
    if (!async) {
      natives.add('Pointer<RustCallStatus>');
      darts.add('Pointer<RustCallStatus>');
      out.writeln('final status=arena<RustCallStatus>();');
      values.add('status');
    }
    final lookup = _lookup(
      _uniSymbol(f),
      '${async ? 'Uint64' : nativeType(ret, uniffi: true)} Function(${natives.join(', ')})',
      '${async ? 'int' : ffiDart(ret, uniffi: true)} Function(${darts.join(', ')})',
    );
    out.writeln('final call=$lookup;\nargs.transfer();');
    out.writeln(
      '${ret['kind'] == 'void' && !async ? '' : 'final result='}call(${values.join(', ')});',
    );
    if (async) {
      final suffix = scalar(ret)
          ? (ret['kind'] == 'bool'
                ? 'i8'
                : ret['kind'] == 'void'
                ? 'void'
                : ret['kind'])
          : ret['kind'] == 'object'
          ? 'pointer'
          : 'rust_buffer';
      final base = 'ffi_${component}_rust_future';
      final poll = _lookup(
        '${base}_poll_$suffix',
        'Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)',
        'void Function(int, Pointer<NativeFunction<NativeWake>>, int)',
      );
      final complete = _lookup(
        '${base}_complete_$suffix',
        '${nativeType(ret, uniffi: true)} Function(Uint64, Pointer<RustCallStatus>)',
        '${ffiDart(ret, uniffi: true)} Function(int, Pointer<RustCallStatus>)',
      );
      final cancel = _lookup(
        '${base}_cancel_$suffix',
        'Void Function(Uint64)',
        'void Function(int)',
      );
      final free = _lookup(
        '${base}_free_$suffix',
        'Void Function(Uint64)',
        'void Function(int)',
      );
      out.writeln(
        'return await ctx._scheduler.run<${dartType(ret)}>(handle:result,poll:$poll,cancel:$cancel,free:$free,cancellationToken:cancellationToken,complete:(handle) { final status=arena<RustCallStatus>(); ${ret['kind'] == 'void' ? '' : 'final value='}$complete(handle,status); ${_uniCheck(f)} ${_liftUni(ret, ret['kind'] == 'void' ? 'null' : 'value')} });',
      );
    } else {
      out.writeln(
        '${_uniCheck(f)} ${_liftUni(ret, ret['kind'] == 'void' ? 'null' : 'result')}',
      );
    }
    out.writeln(
      '} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }\n}',
    );
    return out.toString().replaceAll('this', 'ctx');
  }

  String _uniCheck(Json f) {
    final error = f['throws'] as Json?;
    return error == null || error['kind'] == 'void'
        ? 'rt.check(status.ref);'
        : 'rt.check(status.ref, (bytes) { final r=WireReader(bytes); final value=${_decode(error, 'r')}; r.finish(); return value; });';
  }

  String _liftUni(Json t, String value) {
    if (t['kind'] == 'void') return 'return;';
    if (scalar(t))
      return 'return ${t['kind'] == 'bool'
          ? '$value != 0'
          : t['kind'] == 'u64'
          ? 'BigInt.from($value).toUnsigned(64)'
          : value};';
    if (t['kind'] == 'object')
      return 'return ${name(t)}._(ctx, $value.address, engine);';
    if (t['kind'] == 'string')
      return 'return utf8.decode(rt.lift($value),allowMalformed:false);';
    return 'final r=WireReader(rt.lift($value)); final decoded=${_decode(t, 'r')}; r.finish(); return decoded;';
  }

  // BoltFFI types are resolved through IDs in the envelope, never by ABI guesses.
  Json _boltType(dynamic value, Json ids) {
    if (value is String) {
      if (value == 'String') return {'kind': 'string'};
      if (value == 'Bytes') return {'kind': 'bytes'};
      if (value == 'Void') return {'kind': 'void'};
      return {'kind': value.toLowerCase()};
    }
    final t = value as Json, key = t.keys.single, inner = t[key];
    if (key == 'Primitive') return _boltType(inner, ids);
    if ({'Record', 'Enum', 'Class', 'Callback'}.contains(key)) {
      final decl = ids['$key:$inner'] as Json?;
      if (decl == null) throw FormatException('Unresolved BoltFFI type $value');
      final matches = types.values
          .where(
            (t) =>
                t['name'] == words(decl['name']) &&
                crate(t) == (decl['module'] as String).split('::').first,
          )
          .toList();
      if (matches.length != 1)
        throw FormatException(
          'Ambiguous BoltFFI type $value: ${words(decl['name'])}',
        );
      return {
        'kind': {
          'Record': 'record',
          'Enum': 'enum',
          'Class': 'object',
          'Callback': 'callback',
        }[key],
        'module': matches.single['module'],
        'name': matches.single['name'],
      };
    }
    if ({'Optional', 'Sequence', 'Set'}.contains(key))
      return {
        'kind': {
          'Optional': 'option',
          'Sequence': 'sequence',
          'Set': 'set',
        }[key],
        'inner': _boltType(inner, ids),
      };
    if (key == 'Map')
      return {
        'kind': 'map',
        'key': _boltType(inner['key'], ids),
        'value': _boltType(inner['value'], ids),
      };
    throw FormatException('Unsupported BoltFFI type $value');
  }

  String _emitBolt(Json f, Json entry, int index) {
    final declaration = entry['declaration'] as Json,
        callable = declaration['callable'] as Json,
        ids = entry['ids'] as Json;
    final out = StringBuffer(),
        async = f['async'] == true,
        ret = f['return'] as Json;
    final execution = callable['execution'];
    final isAsync = execution is Map;
    if (async != isAsync)
      throw FormatException('Engine execution mismatch for ${qualified(f)}');
    out.writeln(
      '${async ? 'Future<${dartType(ret)}>' : dartType(ret)} _b$index($className ctx${_publicParams(f).isEmpty ? '' : ', ${_publicParams(f)}'}) ${async ? 'async' : ''} {\nfinal _library=ctx._library;\nconst engine=NativeEngine.boltffi;\nfinal arena=Arena();',
    );
    final natives = <String>[], darts = <String>[], values = <String>[];
    final leases = <String>[];
    out.writeln('final leases=<NativeHandle>[]; try {');
    if (f['kind'] == 'method') {
      out.writeln(
        'final receiverHandle=receiver._handle.acquire(engine, ctx._library); leases.add(receiver._handle);',
      );
      leases.add('receiver');
      natives.add('Uint64');
      darts.add('int');
      values.add('receiverHandle');
    }
    final params = (callable['params'] as List).cast<Json>();
    if (params.length != (f['inputs'] as List).length)
      throw FormatException(
        'Engine argument count mismatch for ${qualified(f)}',
      );
    for (var i = 0; i < params.length; i++) {
      final p = params[i], value = (p['payload'] as Json)['Value'];
      if (value is! Map)
        throw FormatException('Unsupported BoltFFI parameter payload: $p');
      final name = camel(words(p['name'])),
          source = ((f['inputs'] as List)[i] as Json)['type'] as Json;
      if (value['Encoded'] != null) {
        final info = value['Encoded'] as Json;
        if (info['shape'] != 'Slice')
          throw FormatException('Unsupported encoded parameter shape: $info');
        out.writeln(
          'final w$i=WireWriter(endian:Endian.little); ${_encode(source, name, 'w$i')} final bytes$i=w$i.takeBytes(); final p$i=arena<Uint8>(bytes$i.isEmpty?1:bytes$i.length); p$i.asTypedList(bytes$i.length).setAll(0,bytes$i);',
        );
        natives.addAll(['Pointer<Uint8>', 'UintPtr']);
        darts.addAll(['Pointer<Uint8>', 'int']);
        values.addAll(['p$i', 'bytes$i.length']);
      } else if (value['DirectVec'] != null) {
        final element = source['kind'] == 'bytes'
            ? <String, dynamic>{'kind': 'u8'}
            : source['inner'] as Json;
        if (!scalar(element))
          throw FormatException('Unsupported direct vector element: $element');
        final nt = nativeType(element);
        out.writeln(
          'final p$i=arena<$nt>($name.isEmpty?1:$name.length); for(var n=0;n<$name.length;n++) { p$i[n]=${element['kind'] == 'u64'
              ? 'lowerNativeU64($name[n])'
              : element['kind'] == 'bool'
              ? '($name[n]?1:0)'
              : '$name[n]'}; }',
        );
        natives.addAll(['Pointer<$nt>', 'UintPtr']);
        darts.addAll(['Pointer<$nt>', 'int']);
        values.addAll(['p$i', '$name.length']);
      } else if (value['Direct'] != null) {
        if (isDirect(source)) {
          final record = types[typeKey(source)]!,
              fields = (record['fields'] as List).cast<Json>();
          out.writeln('final p$i=arena<_Direct${this.name(source)}>();');
          for (final field in fields) {
            final fieldName = camel(field['name'] as String),
                t = field['type'] as Json;
            out.writeln(
              'p$i.ref.$fieldName=${t['kind'] == 'u64'
                  ? 'lowerNativeU64($name.$fieldName)'
                  : t['kind'] == 'bool'
                  ? '($name.$fieldName?1:0)'
                  : '$name.$fieldName'};',
            );
          }
          // DirectViaReturnSlot and Direct parameter are by-value C structs.
          natives.add('_Direct${this.name(source)}');
          darts.add('_Direct${this.name(source)}');
          values.add('p$i.ref');
        } else {
          natives.add(nativeType(source));
          darts.add(ffiDart(source));
          values.add(
            isCStyle(source)
                ? '_lower${this.name(source)}($name)'
                : source['kind'] == 'bool'
                ? '($name?1:0)'
                : source['kind'] == 'u64'
                ? 'lowerNativeU64($name)'
                : name,
          );
        }
      } else if (value['ScalarOption'] != null) {
        // BoltFFI 0.31 native optional scalars use a borrowed wire slice.
        // The packed scalar transport is specific to its Wasm bindings.
        out.writeln(
          'final w$i=WireWriter(endian:Endian.little); ${_encode(source, name, 'w$i')} final bytes$i=w$i.takeBytes(); final p$i=arena<Uint8>(bytes$i.length); p$i.asTypedList(bytes$i.length).setAll(0,bytes$i);',
        );
        natives.addAll(['Pointer<Uint8>', 'UintPtr']);
        darts.addAll(['Pointer<Uint8>', 'int']);
        values.addAll(['p$i', 'bytes$i.length']);
      } else if(value['Handle']!=null) {
        final info=value['Handle'] as Json;
        if(info['carrier']!='U64' || info['receive']!='ByRef' || info['presence']!='Required' || source['kind']!='object') {
          throw FormatException('Unsupported BoltFFI object ownership for ${qualified(f)}: $info');
        }
        out.writeln('final p$i=$name._handle.acquire(engine,ctx._library);leases.add($name._handle);');
        natives.add('Uint64');darts.add('int');values.add('p$i');
      } else {
        throw FormatException('Unsupported BoltFFI parameter: $p');
      }
    }
    final plan = callable['returns']['plan'];
    final planKind = plan is String ? plan : (plan as Json).keys.single;
    final error = callable['error'];
    final hasError = error != 'None';
    final bufferReturn =
        planKind.startsWith('Encoded') ||
        planKind.startsWith('ScalarOption') ||
        planKind.startsWith('DirectVec');
    final returnNative = planKind.startsWith('Handle')
        ? 'Uint64'
        : planKind == 'Void'
        ? 'Void'
        : bufferReturn
        ? 'BoltBuffer'
        : nativeType(ret);
    final returnDart = returnNative == 'Uint64'
        ? 'int'
        : returnNative == 'Void'
        ? 'void'
        : returnNative == 'BoltBuffer'
        ? 'BoltBuffer'
        : ffiDart(ret);
    if (!{
      'Void',
      'DirectViaReturnSlot',
      'DirectViaOutPointer',
      'EncodedViaReturnSlot',
      'EncodedViaOutPointer',
      'HandleViaReturnSlot',
      'HandleViaOutPointer',
      'ScalarOptionViaReturnSlot',
      'ScalarOptionViaOutPointer',
      'DirectVecViaReturnSlot',
      'DirectVecViaOutPointer',
    }.contains(planKind))
      throw FormatException('Unsupported BoltFFI return plan $plan');
    if (async) {
      final lookup = _lookup(
        words(declaration['symbol']),
        'Uint64 Function(${natives.join(', ')})',
        'int Function(${darts.join(', ')})',
      );
      out.writeln('final handle=$lookup(${values.join(', ')});');
      final protocol = execution['Asynchronous']['PollHandle'] as Json;
      final poll = _lookup(
        words(protocol['poll']),
        'Int8 Function(Uint64, Uint64, Pointer<NativeFunction<NativeWake>>)',
        'int Function(int, int, Pointer<NativeFunction<NativeWake>>)',
      );
      final extra = hasError && ret['kind'] != 'void'
          ? ', Pointer<$returnNative>'
          : '';
      final complete = _lookup(
        words(protocol['complete']),
        '${hasError ? 'BoltBuffer' : returnNative} Function(Uint64, Pointer<BoltStatus>$extra)',
        '${hasError ? 'BoltBuffer' : returnDart} Function(int, Pointer<BoltStatus>$extra)',
      );
      final cancel = _lookup(
        words(protocol['cancel']),
        'Void Function(Uint64)',
        'void Function(int)',
      );
      final free = _lookup(
        words(protocol['free']),
        'Void Function(Uint64)',
        'void Function(int)',
      );
      out.writeln(
        'return await ctx._scheduler.run<${dartType(ret)}>(handle:handle,poll:(h,callback,id) { $poll(h,id,callback); },cancel:$cancel,free:$free,cancellationToken:cancellationToken,complete:(handle) { final status=arena<BoltStatus>();',
      );
      if (hasError) {
        if (error is! Map || !error.containsKey('EncodedViaReturnSlot'))
          throw FormatException('Unsupported async BoltFFI error plan $error');
        final errorType = _boltType(error['EncodedViaReturnSlot']['ty'], ids);
        if (ret['kind'] != 'void')
          out.writeln('final output=arena<$returnNative>();');
        out.writeln(
          'final errorBuffer=$complete(handle,status${ret['kind'] == 'void' ? '' : ', output'}); final errorBytes=ctx._bolt.lift(errorBuffer); ctx._bolt.check(status.ref); if(errorBytes.isNotEmpty) { final r=WireReader(errorBytes,endian:Endian.little); final error=${_decode(errorType, 'r')}; r.finish(); throw error; }',
        );
        if (ret['kind'] != 'void')
          out.writeln(
            'final result=${scalar(ret) || ret['kind'] == 'object' ? 'output.value' : 'output.ref'};',
          );
      } else {
        out.writeln(
          '${ret['kind'] == 'void' ? '' : 'final result='}$complete(handle,status); ctx._bolt.check(status.ref);',
        );
      }
      out.writeln(
        '${_liftBolt(ret, 'result', encoded: bufferReturn && !planKind.startsWith('DirectVec'), directVec: planKind.startsWith('DirectVec'))} });',
      );
    } else {
      if (planKind.endsWith('OutPointer')) {
        natives.add('Pointer<$returnNative>');
        darts.add('Pointer<$returnNative>');
        values.add('output');
        out.writeln('final output=arena<$returnNative>();');
      }
      final lookup = _lookup(
        words(declaration['symbol']),
        '${hasError ? 'BoltBuffer' : returnNative} Function(${natives.join(', ')})',
        '${hasError ? 'BoltBuffer' : returnDart} Function(${darts.join(', ')})',
      );
      if (hasError) {
        if (error is! Map || !error.containsKey('EncodedViaReturnSlot'))
          throw FormatException('Unsupported BoltFFI error plan $error');
        final errorType = _boltType(error['EncodedViaReturnSlot']['ty'], ids);
        out.writeln(
          'final errorBuffer=$lookup(${values.join(', ')}); if(errorBuffer.length!=0) { final r=WireReader(ctx._bolt.lift(errorBuffer),endian:Endian.little); final error=${_decode(errorType, 'r')}; r.finish(); throw error; } ctx._bolt.lift(errorBuffer);',
        );
        if (ret['kind'] != 'void')
          out.writeln(
            'final result=${scalar(ret) || ret['kind'] == 'object' ? 'output.value' : 'output.ref'};',
          );
      } else {
        out.writeln(
          '${ret['kind'] == 'void' ? '' : 'final result='}$lookup(${values.join(', ')});',
        );
      }
      out.writeln(
        _liftBolt(
          ret,
          'result',
          encoded: bufferReturn && !planKind.startsWith('DirectVec'),
          directVec: planKind.startsWith('DirectVec'),
        ),
      );
    }
    out.writeln(
      '} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }\n}',
    );
    return out.toString().replaceAll('this', 'ctx');
  }

  String _liftBolt(
    Json t,
    String value, {
    required bool encoded,
    bool directVec = false,
  }) {
    if (t['kind'] == 'void') return 'return;';
    if (encoded)
      return 'final r=WireReader(ctx._bolt.lift($value),endian:Endian.little); final decoded=${_decode(t, 'r')}; r.finish(); return decoded;';
    if (directVec) {
      final inner = t['kind'] == 'bytes'
          ? <String, dynamic>{'kind': 'u8'}
          : t['inner'] as Json;
      if (!scalar(inner) || inner['kind'] == 'void')
        throw FormatException(
          'Unsupported direct vector return element $inner',
        );
      return 'final bytes=ctx._bolt.lift($value); final stride=sizeOf<${nativeType(inner)}>(); if(bytes.length%stride!=0)throw const FormatException("Invalid direct vector byte length"); final r=WireReader(bytes,endian:Endian.little); return ${t['kind'] == 'bytes' ? 'bytes' : 'List.generate(bytes.length~/stride,(_)=>${_decode(inner, 'r')})'};';
    }
    if (isCStyle(t)) return 'return _lift${name(t)}($value);';
    if (scalar(t))
      return 'return ${t['kind'] == 'bool'
          ? '$value!=0'
          : t['kind'] == 'u64'
          ? 'BigInt.from($value).toUnsigned(64)'
          : value};';
    if (t['kind'] == 'object') return 'return ${name(t)}._(ctx,$value,engine);';
    if (isDirect(t)) {
      final fields = (types[typeKey(t)]!['fields'] as List).cast<Json>();
      return 'return ${name(t)}(${fields.map((f) {
        final n = camel(f['name'] as String), k = f['type']['kind'];
        return '$n: ${k == 'bool'
            ? '$value.$n!=0'
            : k == 'u64'
            ? 'BigInt.from($value.$n).toUnsigned(64)'
            : '$value.$n'}';
      }).join(', ')});';
    }
    throw FormatException('Unsupported direct BoltFFI return $t');
  }

  void _emitResolvers() {
    final asset = config['asset_id'] as String?;
    if (asset == null) return;
    final out = StringBuffer(
      'NativeLibrary ${camel(className)}AssetLibrary() => NativeLibrary((symbol) => switch(symbol) {',
    );
    var index = 0;
    // Runtime helpers also need native asset entrypoints.
    for (final component
        in functions
            .where((f) => f['has_uniffi'] != false)
            .map(crate)
            .toSet()) {
      _lookup(
        'ffi_${component}_rustbuffer_alloc',
        'RustBuffer Function(Uint64, Pointer<RustCallStatus>)',
        'RustBuffer Function(int, Pointer<RustCallStatus>)',
      );
      _lookup(
        'ffi_${component}_rustbuffer_free',
        'Void Function(RustBuffer, Pointer<RustCallStatus>)',
        'void Function(RustBuffer, Pointer<RustCallStatus>)',
      );
    }
    if (boltFunctions.isNotEmpty)
      _lookup(
        'boltffi_free_buf',
        'Void Function(BoltBuffer)',
        'void Function(BoltBuffer)',
      );
    for (final entry in _symbols.entries) {
      final sig = entry.value.dart;
      final pos = sig.indexOf(' Function(');
      final ret = sig.substring(0, pos),
          params = sig.substring(pos + 10, sig.length - 1);
      // Split on commas outside nested function type parameters.
      final parts = <String>[];
      var start = 0, depth = 0;
      for (var i = 0; i < params.length; i++) {
        final c = params[i];
        if (c == '<' || c == '(') depth++;
        if (c == '>' || c == ')') depth--;
        if (c == ',' && depth == 0) {
          parts.add(params.substring(start, i).trim());
          start = i + 1;
        }
      }
      if (params.trim().isNotEmpty) parts.add(params.substring(start).trim());
      main.writeln(
        '@Native<${entry.value.native}>(symbol:${jsonEncode(entry.key)}, assetId:${jsonEncode(asset)})\nexternal $ret _asset$index(${[for (var i = 0; i < parts.length; i++) '${parts[i]} arg$i'].join(', ')});',
      );
      out.writeln(
        '${jsonEncode(entry.key)} => Native.addressOf<NativeFunction<${entry.value.native}>>(_asset$index).cast<Void>(),',
      );
      index++;
    }
    out.writeln(
      "_ => throw ArgumentError('Unknown native symbol: \$symbol'), });",
    );
    main.writeln(out);
  }
}
