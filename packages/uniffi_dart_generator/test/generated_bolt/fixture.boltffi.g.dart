// Generated.
// ignore_for_file: unused_local_variable, no_leading_underscores_for_local_identifiers, curly_braces_in_flow_control_structures
part of 'fixture.g.dart';
final class _DirectPoint extends Struct {
@Double() external double x;
@Double() external double y;
}
Counter _b0(NativeBindings ctx, {required int value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final result=_library.lookup<NativeFunction<Uint64 Function(Int64)>>("boltffi_init_class_native_ffi_fixture_counter_new").asFunction<int Function(int)>()(value);
return Counter._(ctx,result,engine);
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _b1(NativeBindings ctx, {required Counter receiver, required int amount})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final receiverHandle=receiver._handle.acquire(engine, ctx._library); leases.add(receiver._handle);
final result=_library.lookup<NativeFunction<Int64 Function(Uint64, Int64)>>("boltffi_method_class_native_ffi_fixture_counter_increment").asFunction<int Function(int, int)>()(receiverHandle, amount);
return result;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Future<int> _b2(NativeBindings ctx, {required Counter receiver, required int delayMs, NativeCancellationToken? cancellationToken}) async {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final receiverHandle=receiver._handle.acquire(engine, ctx._library); leases.add(receiver._handle);
final handle=_library.lookup<NativeFunction<Uint64 Function(Uint64, Uint32)>>("boltffi_method_class_native_ffi_fixture_counter_read_later").asFunction<int Function(int, int)>()(receiverHandle, delayMs);
return await ctx._scheduler.run<int>(handle:handle,poll:(h,callback,id) { _library.lookup<NativeFunction<Int8 Function(Uint64, Uint64, Pointer<NativeFunction<NativeWake>>)>>("boltffi_async_method_class_native_ffi_fixture_counter_read_later_poll").asFunction<int Function(int, int, Pointer<NativeFunction<NativeWake>>)>()(h,id,callback); },cancel:_library.lookup<NativeFunction<Void Function(Uint64)>>("boltffi_async_method_class_native_ffi_fixture_counter_read_later_cancel").asFunction<void Function(int)>(),free:_library.lookup<NativeFunction<Void Function(Uint64)>>("boltffi_async_method_class_native_ffi_fixture_counter_read_later_free").asFunction<void Function(int)>(),cancellationToken:cancellationToken,complete:(handle) { final status=arena<BoltStatus>();
final result=_library.lookup<NativeFunction<Int64 Function(Uint64, Pointer<BoltStatus>)>>("boltffi_async_method_class_native_ffi_fixture_counter_read_later_complete").asFunction<int Function(int, Pointer<BoltStatus>)>()(handle,status); ctx._bolt.check(status.ref);
return result; });
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Ticker _b3(NativeBindings ctx)  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final result=_library.lookup<NativeFunction<Uint64 Function()>>("boltffi_init_class_native_ffi_fixture_ticker_new").asFunction<int Function()>()();
return Ticker._(ctx,result,engine);
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

void _b4(NativeBindings ctx, {required Ticker receiver, required int value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final receiverHandle=receiver._handle.acquire(engine, ctx._library); leases.add(receiver._handle);
_library.lookup<NativeFunction<Void Function(Uint64, Uint32)>>("boltffi_method_class_native_ffi_fixture_ticker_emit").asFunction<void Function(int, int)>()(receiverHandle, value);
return;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Mode _b5(NativeBindings ctx, {required Mode value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final result=_library.lookup<NativeFunction<Uint32 Function(Uint32)>>("boltffi_function_native_ffi_fixture_echo_mode").asFunction<int Function(int)>()(_lowerMode(value));
return _liftMode(result);
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

BigInt? _b6(NativeBindings ctx, {BigInt? value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final w0=WireWriter(endian:Endian.little); w0.optional(value, (value) { w0.u64(value); }); final bytes0=w0.takeBytes(); final p0=arena<Uint8>(bytes0.length); p0.asTypedList(bytes0.length).setAll(0,bytes0);
final result=_library.lookup<NativeFunction<BoltBuffer Function(Pointer<Uint8>, UintPtr)>>("boltffi_function_native_ffi_fixture_echo_optional").asFunction<BoltBuffer Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
final r=WireReader(ctx._bolt.lift(result),endian:Endian.little); final decoded=r.optional(() => r.u64()); r.finish(); return decoded;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Uint8List _b7(NativeBindings ctx, {required Uint8List value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final w0=WireWriter(endian:Endian.little); w0.buffer(value); final bytes0=w0.takeBytes(); final p0=arena<Uint8>(bytes0.isEmpty?1:bytes0.length); p0.asTypedList(bytes0.length).setAll(0,bytes0);
final result=_library.lookup<NativeFunction<BoltBuffer Function(Pointer<Uint8>, UintPtr)>>("boltffi_function_native_ffi_fixture_echo_bytes").asFunction<BoltBuffer Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
final r=WireReader(ctx._bolt.lift(result),endian:Endian.little); final decoded=r.buffer(); r.finish(); return decoded;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

List<BigInt> _b8(NativeBindings ctx, {required List<BigInt> value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final p0=arena<Uint64>(value.isEmpty?1:value.length); for(var n=0;n<value.length;n++) { p0[n]=lowerNativeU64(value[n]); }
final result=_library.lookup<NativeFunction<BoltBuffer Function(Pointer<Uint64>, UintPtr)>>("boltffi_function_native_ffi_fixture_echo_numbers").asFunction<BoltBuffer Function(Pointer<Uint64>, int)>()(p0, value.length);
final bytes=ctx._bolt.lift(result); final stride=sizeOf<Uint64>(); if(bytes.length%stride!=0)throw const FormatException("Invalid direct vector byte length"); final r=WireReader(bytes,endian:Endian.little); return List.generate(bytes.length~/stride,(_)=>r.u64());
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _b9(NativeBindings ctx, {required int a, required int b})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final result=_library.lookup<NativeFunction<Int64 Function(Int64, Int64)>>("boltffi_function_native_ffi_fixture_add").asFunction<int Function(int, int)>()(a, b);
return result;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

BigInt _b10(NativeBindings ctx, {required BigInt value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final result=_library.lookup<NativeFunction<Uint64 Function(Uint64)>>("boltffi_function_native_ffi_fixture_echo_u64").asFunction<int Function(int)>()(lowerNativeU64(value));
return BigInt.from(result).toUnsigned(64);
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Point _b11(NativeBindings ctx, {required Point value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final p0=arena<_DirectPoint>();
p0.ref.x=value.x;
p0.ref.y=value.y;
final result=_library.lookup<NativeFunction<_DirectPoint Function(_DirectPoint)>>("boltffi_function_native_ffi_fixture_echo_point").asFunction<_DirectPoint Function(_DirectPoint)>()(p0.ref);
return Point(x: result.x, y: result.y);
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Payload _b12(NativeBindings ctx, {required Payload value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final w0=WireWriter(endian:Endian.little); _writePayload(w0, value, ctx, engine); final bytes0=w0.takeBytes(); final p0=arena<Uint8>(bytes0.isEmpty?1:bytes0.length); p0.asTypedList(bytes0.length).setAll(0,bytes0);
final result=_library.lookup<NativeFunction<BoltBuffer Function(Pointer<Uint8>, UintPtr)>>("boltffi_function_native_ffi_fixture_echo_payload").asFunction<BoltBuffer Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
final r=WireReader(ctx._bolt.lift(result),endian:Endian.little); final decoded=_readPayload(r, ctx, engine); r.finish(); return decoded;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Event _b13(NativeBindings ctx, {required Event value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final w0=WireWriter(endian:Endian.little); _writeEvent(w0, value, ctx, engine); final bytes0=w0.takeBytes(); final p0=arena<Uint8>(bytes0.isEmpty?1:bytes0.length); p0.asTypedList(bytes0.length).setAll(0,bytes0);
final result=_library.lookup<NativeFunction<BoltBuffer Function(Pointer<Uint8>, UintPtr)>>("boltffi_function_native_ffi_fixture_echo_event").asFunction<BoltBuffer Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
final r=WireReader(ctx._bolt.lift(result),endian:Endian.little); final decoded=_readEvent(r, ctx, engine); r.finish(); return decoded;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _b14(NativeBindings ctx, {required int a, required int b})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final output=arena<Int32>();
final errorBuffer=_library.lookup<NativeFunction<BoltBuffer Function(Int32, Int32, Pointer<Int32>)>>("boltffi_function_native_ffi_fixture_divide").asFunction<BoltBuffer Function(int, int, Pointer<Int32>)>()(a, b, output); if(errorBuffer.length!=0) { final r=WireReader(ctx._bolt.lift(errorBuffer),endian:Endian.little); final error=_readFixtureError(r, ctx, engine); r.finish(); throw error; } ctx._bolt.lift(errorBuffer);
final result=output.value;
return result;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

String _b15(NativeBindings ctx, {required bool fail})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final output=arena<BoltBuffer>();
final errorBuffer=_library.lookup<NativeFunction<BoltBuffer Function(Uint8, Pointer<BoltBuffer>)>>("boltffi_function_native_ffi_fixture_string_error").asFunction<BoltBuffer Function(int, Pointer<BoltBuffer>)>()((fail?1:0), output); if(errorBuffer.length!=0) { final r=WireReader(ctx._bolt.lift(errorBuffer),endian:Endian.little); final error=r.string(); r.finish(); throw error; } ctx._bolt.lift(errorBuffer);
final result=output.ref;
final r=WireReader(ctx._bolt.lift(result),endian:Endian.little); final decoded=r.string(); r.finish(); return decoded;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Future<String> _b16(NativeBindings ctx, {required String value, required int delayMs, NativeCancellationToken? cancellationToken}) async {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final w0=WireWriter(endian:Endian.little); w0.string(value); final bytes0=w0.takeBytes(); final p0=arena<Uint8>(bytes0.isEmpty?1:bytes0.length); p0.asTypedList(bytes0.length).setAll(0,bytes0);
final handle=_library.lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr, Uint32)>>("boltffi_function_native_ffi_fixture_delayed_echo").asFunction<int Function(Pointer<Uint8>, int, int)>()(p0, bytes0.length, delayMs);
return await ctx._scheduler.run<String>(handle:handle,poll:(h,callback,id) { _library.lookup<NativeFunction<Int8 Function(Uint64, Uint64, Pointer<NativeFunction<NativeWake>>)>>("boltffi_async_function_native_ffi_fixture_delayed_echo_poll").asFunction<int Function(int, int, Pointer<NativeFunction<NativeWake>>)>()(h,id,callback); },cancel:_library.lookup<NativeFunction<Void Function(Uint64)>>("boltffi_async_function_native_ffi_fixture_delayed_echo_cancel").asFunction<void Function(int)>(),free:_library.lookup<NativeFunction<Void Function(Uint64)>>("boltffi_async_function_native_ffi_fixture_delayed_echo_free").asFunction<void Function(int)>(),cancellationToken:cancellationToken,complete:(handle) { final status=arena<BoltStatus>();
final result=_library.lookup<NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>>("boltffi_async_function_native_ffi_fixture_delayed_echo_complete").asFunction<BoltBuffer Function(int, Pointer<BoltStatus>)>()(handle,status); ctx._bolt.check(status.ref);
final r=WireReader(ctx._bolt.lift(result),endian:Endian.little); final decoded=r.string(); r.finish(); return decoded; });
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _b17(NativeBindings ctx, {required Counter value})  {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final p0=value._handle.acquire(engine,ctx._library);leases.add(value._handle);
final result=_library.lookup<NativeFunction<Int64 Function(Uint64)>>("boltffi_function_native_ffi_fixture_read_counter").asFunction<int Function(int)>()(p0);
return result;
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Future<int> _b18(NativeBindings ctx, {required int a, required int b, NativeCancellationToken? cancellationToken}) async {
final _library=ctx._library;
const engine=NativeEngine.boltffi;
final arena=Arena();
final leases=<NativeHandle>[]; try {
final handle=_library.lookup<NativeFunction<Uint64 Function(Int32, Int32)>>("boltffi_function_native_ffi_fixture_async_divide").asFunction<int Function(int, int)>()(a, b);
return await ctx._scheduler.run<int>(handle:handle,poll:(h,callback,id) { _library.lookup<NativeFunction<Int8 Function(Uint64, Uint64, Pointer<NativeFunction<NativeWake>>)>>("boltffi_async_function_native_ffi_fixture_async_divide_poll").asFunction<int Function(int, int, Pointer<NativeFunction<NativeWake>>)>()(h,id,callback); },cancel:_library.lookup<NativeFunction<Void Function(Uint64)>>("boltffi_async_function_native_ffi_fixture_async_divide_cancel").asFunction<void Function(int)>(),free:_library.lookup<NativeFunction<Void Function(Uint64)>>("boltffi_async_function_native_ffi_fixture_async_divide_free").asFunction<void Function(int)>(),cancellationToken:cancellationToken,complete:(handle) { final status=arena<BoltStatus>();
final output=arena<Int32>();
final errorBuffer=_library.lookup<NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>, Pointer<Int32>)>>("boltffi_async_function_native_ffi_fixture_async_divide_complete").asFunction<BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<Int32>)>()(handle,status, output); final errorBytes=ctx._bolt.lift(errorBuffer); ctx._bolt.check(status.ref); if(errorBytes.isNotEmpty) { final r=WireReader(errorBytes,endian:Endian.little); final error=_readFixtureError(r, ctx, engine); r.finish(); throw error; }
final result=output.value;
return result; });
} finally {arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

