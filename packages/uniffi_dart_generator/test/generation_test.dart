import 'package:test/test.dart';
import 'package:uniffi_dart_build/uniffi_dart_build.dart';
import '../lib/src/emitter.dart';

Json function(String module,String name)=>{'kind':'function','module':module,'name':name,'inputs':[],'return':{'kind':'i32'},'async':false,'checksum':13};

void main() {
  test('generation is deterministic with exactly three declared Dart outputs',() {
    final metadata=NativeMetadata([function('first','ping')],[],{});
    Map<String,String> generate()=>DartBindingsEmitter(metadata,{},'binding').generate();
    expect(generate(),generate());
    expect(generate().keys,unorderedEquals(['binding.g.dart','binding.uniffi.g.dart','binding.boltffi.g.dart']));
  });
  test('missing forced engine produces a named diagnostic',() {
    final metadata=NativeMetadata([function('first','ping')],[],{});
    expect(()=>DartBindingsEmitter(metadata,{'engine':'boltffi'},'binding').generate(),throwsA(isA<FormatException>().having((e)=>e.message,'message',contains('first::ping'))));
  });
  test('cross-crate references resolve and explicit names remove collisions',() {
    final items=<Json>[
      {'kind':'record','module':'first','name':'Item','fields':[{'name':'id','type':{'kind':'u64'}}]},
      {...function('second','item'),'return':{'kind':'record','module':'first','name':'Item'}},
    ];
    final output=DartBindingsEmitter(NativeMetadata(items,[],{}),{'names':{'first::Item':'SharedItem'}},'binding').generate();
    expect(output['binding.g.dart'],contains('class SharedItem'));
    expect(output['binding.g.dart'],contains('SharedItem secondItem('));
  });
  test('collisions and conflicting metadata cannot silently hide interfaces',() {
    final source=NativeMetadata([function('first','ping'),function('second','ping')],[],{});
    expect(()=>DartBindingsEmitter(source,{'prefixes':{'first':'','second':''}},'binding').generate(),throwsFormatException);
    final conflicting=NativeMetadata([
      {'kind':'record','module':'first','name':'Item','fields':[]},
      {'kind':'record','module':'first','name':'Item','fields':[{'name':'id','type':{'kind':'u32'}}]},
    ],[],{});
    expect(()=>DartBindingsEmitter(conflicting,{},'binding'),throwsFormatException);
  });
  test('unsafe synchronous callbacks are rejected before output',() {
    final source=NativeMetadata([
      {'kind':'callback','module':'first','name':'Listener','methods':[]},
      {...function('first','listen'),'inputs':[{'name':'listener','type':{'kind':'callback','module':'first','name':'Listener'}}]},
    ],[],{});
    expect(()=>DartBindingsEmitter(source,{},'binding').generate(),throwsA(isA<FormatException>().having((e)=>e.message,'message',contains('native async receiver'))));
  });
}
