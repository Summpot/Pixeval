import 'dart:ffi';
import 'dart:convert';
import 'dart:typed_data';

import 'package:ffi/ffi.dart';

import 'lifecycle.dart';

final class RustBuffer extends Struct {
  @Uint64()
  external int capacity;
  @Uint64()
  external int length;
  external Pointer<Uint8> data;
}

final class RustCallStatus extends Struct {
  @Int8()
  external int code;
  external RustBuffer error;
}

final class BoltBuffer extends Struct {
  external Pointer<Uint8> data;
  @UintPtr()
  external int length;
  @UintPtr()
  external int capacity;
  @UintPtr()
  external int alignment;
}

final class BoltStatus extends Struct {
  @Int32()
  external int code;
}

typedef _AllocNative = RustBuffer Function(Uint64, Pointer<RustCallStatus>);
typedef _AllocDart = RustBuffer Function(int, Pointer<RustCallStatus>);
typedef _FreeNative = Void Function(RustBuffer, Pointer<RustCallStatus>);
typedef _FreeDart = void Function(RustBuffer, Pointer<RustCallStatus>);

final class NativeLibrary {
  NativeLibrary(this.lookupAddress);
  factory NativeLibrary.dynamic(DynamicLibrary library) =>
      NativeLibrary((name) => library.lookup<Void>(name));
  final Pointer<Void> Function(String) lookupAddress;
  Pointer<T> lookup<T extends NativeType>(String name) =>
      lookupAddress(name).cast<T>();
}

final class UniFfiRuntime {
  UniFfiRuntime(this.library, this.component)
    : _alloc = library
          .lookup<NativeFunction<_AllocNative>>(
            'ffi_${component}_rustbuffer_alloc',
          )
          .asFunction<_AllocDart>(),
      _free = library
          .lookup<NativeFunction<_FreeNative>>(
            'ffi_${component}_rustbuffer_free',
          )
          .asFunction<_FreeDart>();
  final NativeLibrary library;
  final String component;
  final _AllocDart _alloc;
  final _FreeDart _free;
  RustBuffer lower(Uint8List bytes) => using((arena) {
    final status = arena<RustCallStatus>();
    final value = _alloc(bytes.length, status);
    check(status.ref);
    if (bytes.isNotEmpty) value.data.asTypedList(bytes.length).setAll(0, bytes);
    return value;
  });
  Uint8List lift(RustBuffer buffer) {
    try {
      if (buffer.length < 0 ||
          buffer.length > buffer.capacity ||
          (buffer.length > 0 && buffer.data == nullptr)) {
        throw const NativeInteropException('Invalid RustBuffer bounds');
      }
      return buffer.length == 0
          ? Uint8List(0)
          : Uint8List.fromList(buffer.data.asTypedList(buffer.length));
    } finally {
      release(buffer);
    }
  }

  void release(RustBuffer buffer) => using((arena) {
    final status = arena<RustCallStatus>();
    _free(buffer, status);
    if (status.ref.code != 0)
      throw const NativeInteropException('RustBuffer free failed');
  });
  void check(RustCallStatus status, [Object Function(Uint8List)? decodeError]) {
    if (status.code == 0) return;
    final bytes = lift(status.error);
    if (status.code == 1 && decodeError != null) throw decodeError(bytes);
    if (status.code == 3) throw const NativeCancelledException();
    throw NativeInteropException(
      'UniFFI status ${status.code}: ${utf8.decode(bytes, allowMalformed: true)}',
    );
  }
}

/// Owns lowered arguments until the generated code enters the native call.
/// A conversion failure releases earlier allocations rather than leaking them.
final class UniFfiArguments {
  final _cleanup = <void Function()>[];
  RustBuffer buffer(UniFfiRuntime runtime, Uint8List bytes) {
    final result = runtime.lower(bytes);
    _cleanup.add(() => runtime.release(result));
    return result;
  }

  void own(void Function() release) => _cleanup.add(release);
  void transfer() => _cleanup.clear();
  void dispose() {
    for (final release in _cleanup.reversed) {
      release();
    }
    _cleanup.clear();
  }
}

int lowerNativeU64(BigInt value) {
  if (value.isNegative || value.bitLength > 64)
    throw RangeError('Value is not u64: $value');
  return value.toSigned(64).toInt();
}

final class BoltFfiRuntime {
  BoltFfiRuntime(this.library)
    : _free = library
          .lookup<NativeFunction<Void Function(BoltBuffer)>>('boltffi_free_buf')
          .asFunction<void Function(BoltBuffer)>();
  final NativeLibrary library;
  final void Function(BoltBuffer) _free;
  Uint8List lift(BoltBuffer buffer) {
    try {
      if (buffer.length < 0 ||
          buffer.length > buffer.capacity ||
          (buffer.length > 0 && buffer.data == nullptr)) {
        throw const NativeInteropException('Invalid BoltFFI buffer bounds');
      }
      return buffer.length == 0
          ? Uint8List(0)
          : Uint8List.fromList(buffer.data.asTypedList(buffer.length));
    } finally {
      _free(buffer);
    }
  }

  void check(BoltStatus status) {
    if (status.code == 0) return;
    if (status.code == 4) throw const NativeCancelledException();
    throw NativeInteropException('BoltFFI status ${status.code}');
  }
}
