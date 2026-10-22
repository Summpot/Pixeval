// Generated.
// ignore_for_file: unused_local_variable, no_leading_underscores_for_local_identifiers, curly_braces_in_flow_control_structures
part of 'fixture.g.dart';
Counter _u0(NativeBindings ctx, {required int value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<Pointer<Void> Function(Int64, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_constructor_counter_new").asFunction<Pointer<Void> Function(int, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(value, status);
rt.check(status.ref); return Counter._(ctx, result.address, engine);
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _u1(NativeBindings ctx, {required int a, required int b})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<Int64 Function(Int64, Int64, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_add").asFunction<int Function(int, int, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(a, b, status);
rt.check(status.ref); return result;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Future<int> _u2(NativeBindings ctx, {required int a, required int b, NativeCancellationToken? cancellationToken}) async {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final call=_library.lookup<NativeFunction<Uint64 Function(Int32, Int32)>>("uniffi_native_ffi_fixture_fn_func_async_divide").asFunction<int Function(int, int)>();
args.transfer();
final result=call(a, b);
return await ctx._scheduler.run<int>(handle:result,poll:_library.lookup<NativeFunction<Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)>>("ffi_native_ffi_fixture_rust_future_poll_i32").asFunction<void Function(int, Pointer<NativeFunction<NativeWake>>, int)>(),cancel:_library.lookup<NativeFunction<Void Function(Uint64)>>("ffi_native_ffi_fixture_rust_future_cancel_i32").asFunction<void Function(int)>(),free:_library.lookup<NativeFunction<Void Function(Uint64)>>("ffi_native_ffi_fixture_rust_future_free_i32").asFunction<void Function(int)>(),cancellationToken:cancellationToken,complete:(handle) { final status=arena<RustCallStatus>(); final value=_library.lookup<NativeFunction<Int32 Function(Uint64, Pointer<RustCallStatus>)>>("ffi_native_ffi_fixture_rust_future_complete_i32").asFunction<int Function(int, Pointer<RustCallStatus>)>()(handle,status); rt.check(status.ref, (bytes) { final r=WireReader(bytes); final value=_readFixtureError(r, ctx, engine); r.finish(); return value; }); return value; });
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Future<String> _u3(NativeBindings ctx, {required String value, required int delayMs, NativeCancellationToken? cancellationToken}) async {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final bytes0=Uint8List.fromList(utf8.encode(value));
final p0=args.buffer(rt, bytes0);
final call=_library.lookup<NativeFunction<Uint64 Function(RustBuffer, Uint32)>>("uniffi_native_ffi_fixture_fn_func_delayed_echo").asFunction<int Function(RustBuffer, int)>();
args.transfer();
final result=call(p0, delayMs);
return await ctx._scheduler.run<String>(handle:result,poll:_library.lookup<NativeFunction<Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)>>("ffi_native_ffi_fixture_rust_future_poll_rust_buffer").asFunction<void Function(int, Pointer<NativeFunction<NativeWake>>, int)>(),cancel:_library.lookup<NativeFunction<Void Function(Uint64)>>("ffi_native_ffi_fixture_rust_future_cancel_rust_buffer").asFunction<void Function(int)>(),free:_library.lookup<NativeFunction<Void Function(Uint64)>>("ffi_native_ffi_fixture_rust_future_free_rust_buffer").asFunction<void Function(int)>(),cancellationToken:cancellationToken,complete:(handle) { final status=arena<RustCallStatus>(); final value=_library.lookup<NativeFunction<RustBuffer Function(Uint64, Pointer<RustCallStatus>)>>("ffi_native_ffi_fixture_rust_future_complete_rust_buffer").asFunction<RustBuffer Function(int, Pointer<RustCallStatus>)>()(handle,status); rt.check(status.ref); return utf8.decode(rt.lift(value),allowMalformed:false); });
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _u4(NativeBindings ctx, {required int a, required int b})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<Int32 Function(Int32, Int32, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_divide").asFunction<int Function(int, int, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(a, b, status);
rt.check(status.ref, (bytes) { final r=WireReader(bytes); final value=_readFixtureError(r, ctx, engine); r.finish(); return value; }); return result;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Uint8List _u5(NativeBindings ctx, {required Uint8List value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final writer0=WireWriter(); writer0.buffer(value); final bytes0=writer0.takeBytes();
final p0=args.buffer(rt, bytes0);
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_bytes").asFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); final r=WireReader(rt.lift(result)); final decoded=r.buffer(); r.finish(); return decoded;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Event _u6(NativeBindings ctx, {required Event value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final writer0=WireWriter(); _writeEvent(writer0, value, ctx, engine); final bytes0=writer0.takeBytes();
final p0=args.buffer(rt, bytes0);
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_event").asFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); final r=WireReader(rt.lift(result)); final decoded=_readEvent(r, ctx, engine); r.finish(); return decoded;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Mode _u7(NativeBindings ctx, {required Mode value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final writer0=WireWriter(); _writeMode(writer0, value, ctx, engine); final bytes0=writer0.takeBytes();
final p0=args.buffer(rt, bytes0);
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_mode").asFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); final r=WireReader(rt.lift(result)); final decoded=_readMode(r, ctx, engine); r.finish(); return decoded;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

List<BigInt> _u8(NativeBindings ctx, {required List<BigInt> value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final writer0=WireWriter(); writer0.sequence(value, (value) { writer0.u64(value); }); final bytes0=writer0.takeBytes();
final p0=args.buffer(rt, bytes0);
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_numbers").asFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); final r=WireReader(rt.lift(result)); final decoded=r.sequence(() => r.u64()); r.finish(); return decoded;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

BigInt? _u9(NativeBindings ctx, {BigInt? value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final writer0=WireWriter(); writer0.optional(value, (value) { writer0.u64(value); }); final bytes0=writer0.takeBytes();
final p0=args.buffer(rt, bytes0);
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_optional").asFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); final r=WireReader(rt.lift(result)); final decoded=r.optional(() => r.u64()); r.finish(); return decoded;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Payload _u10(NativeBindings ctx, {required Payload value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final writer0=WireWriter(); _writePayload(writer0, value, ctx, engine); final bytes0=writer0.takeBytes();
final p0=args.buffer(rt, bytes0);
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_payload").asFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); final r=WireReader(rt.lift(result)); final decoded=_readPayload(r, ctx, engine); r.finish(); return decoded;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

Point _u11(NativeBindings ctx, {required Point value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final writer0=WireWriter(); _writePoint(writer0, value, ctx, engine); final bytes0=writer0.takeBytes();
final p0=args.buffer(rt, bytes0);
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_point").asFunction<RustBuffer Function(RustBuffer, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); final r=WireReader(rt.lift(result)); final decoded=_readPoint(r, ctx, engine); r.finish(); return decoded;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

BigInt _u12(NativeBindings ctx, {required BigInt value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<Uint64 Function(Uint64, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_echo_u64").asFunction<int Function(int, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(lowerNativeU64(value), status);
rt.check(status.ref); return BigInt.from(result).toUnsigned(64);
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _u13(NativeBindings ctx, {required Counter value})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final lease0=value._handle.acquire(engine, ctx._library); leases.add(value._handle);
final status0=arena<RustCallStatus>(); final p0=_library.lookup<NativeFunction<Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_clone_counter").asFunction<Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)>()(Pointer<Void>.fromAddress(lease0),status0); rt.check(status0.ref);
args.own(() { _library.lookup<NativeFunction<Void Function(Pointer<Void>, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_free_counter").asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(p0,status0); rt.check(status0.ref); });
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<Int64 Function(Pointer<Void>, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_read_counter").asFunction<int Function(Pointer<Void>, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, status);
rt.check(status.ref); return result;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

String _u14(NativeBindings ctx, {required bool fail})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<RustBuffer Function(Int8, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_func_string_error").asFunction<RustBuffer Function(int, Pointer<RustCallStatus>)>();
args.transfer();
final result=call((fail ? 1 : 0), status);
rt.check(status.ref, (bytes) { final r=WireReader(bytes); final value=r.string(); r.finish(); return value; }); return utf8.decode(rt.lift(result),allowMalformed:false);
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

int _u15(NativeBindings ctx, {required Counter receiver, required int amount})  {
final _library=ctx._library;
final rt=ctx._runtime("native_ffi_fixture");
const engine=NativeEngine.uniffi;
final args=UniFfiArguments();
final arena=Arena();
final leases=<NativeHandle>[]; try {
final lease0=receiver._handle.acquire(engine, ctx._library); leases.add(receiver._handle);
final status0=arena<RustCallStatus>(); final p0=_library.lookup<NativeFunction<Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_clone_counter").asFunction<Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)>()(Pointer<Void>.fromAddress(lease0),status0); rt.check(status0.ref);
args.own(() { _library.lookup<NativeFunction<Void Function(Pointer<Void>, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_free_counter").asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(p0,status0); rt.check(status0.ref); });
final status=arena<RustCallStatus>();
final call=_library.lookup<NativeFunction<Int64 Function(Pointer<Void>, Int64, Pointer<RustCallStatus>)>>("uniffi_native_ffi_fixture_fn_method_counter_increment").asFunction<int Function(Pointer<Void>, int, Pointer<RustCallStatus>)>();
args.transfer();
final result=call(p0, amount, status);
rt.check(status.ref); return result;
} finally {args.dispose(); arena.releaseAll(); for(final lease in leases.reversed) {lease.releaseCall();} }
}

