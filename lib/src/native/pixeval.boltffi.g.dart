// Generated.
// ignore_for_file: unused_local_variable, no_leading_underscores_for_local_identifiers, curly_braces_in_flow_control_structures
part of 'pixeval.g.dart';

Future<String> _b0(
  PixevalNative ctx, {
  required String message,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(message);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_ping",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<String>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >("boltffi_async_function_pixeval_native_ping_poll")
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_ping_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_ping_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >("boltffi_async_function_pixeval_native_ping_complete")
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.string();
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

String _b1(PixevalNative ctx) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final result = _library
        .lookup<NativeFunction<BoltBuffer Function()>>(
          "boltffi_function_pixeval_native_version",
        )
        .asFunction<BoltBuffer Function()>()();
    final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
    final decoded = r.string();
    r.finish();
    return decoded;
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<PkceAuthParamsDto> _b2(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_auth_ffi_create_pkce_auth_params",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<PkceAuthParamsDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_create_pkce_auth_params_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_create_pkce_auth_params_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_create_pkce_auth_params_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_create_pkce_auth_params_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readPkceAuthParamsDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<UserProfile?> _b3(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_auth_ffi_get_current_user",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<UserProfile?>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_current_user_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_current_user_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_current_user_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_current_user_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.optional(() => _readUserProfile(r, ctx, engine));
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<String?> _b4(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_auth_ffi_get_refresh_token",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<String?>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_refresh_token_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_refresh_token_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_refresh_token_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_refresh_token_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.optional(() => r.string());
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<String?> _b5(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_auth_ffi_get_stored_refresh_token",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<String?>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_stored_refresh_token_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_stored_refresh_token_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_stored_refresh_token_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_get_stored_refresh_token_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.optional(() => r.string());
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<AuthResponse> _b6(
  PixevalNative ctx, {
  required String code,
  required String codeVerifier,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(code);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.string(codeVerifier);
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint8>, UintPtr)
          >
        >("boltffi_function_pixeval_native_app_api_auth_ffi_login_by_code")
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int)
        >()(p0, bytes0.length, p1, bytes1.length);
    return await ctx._scheduler.run<AuthResponse>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_code_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_code_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_code_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_code_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readAuthResponse(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<AuthResponse> _b7(
  PixevalNative ctx, {
  required String refreshToken,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(refreshToken);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_auth_ffi_login_by_refresh_token",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<AuthResponse>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_refresh_token_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_refresh_token_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_refresh_token_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_login_by_refresh_token_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readAuthResponse(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b8(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_auth_ffi_logout",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_logout_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_logout_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_logout_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_logout_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<AuthResponse> _b9(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_auth_ffi_refresh_session",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<AuthResponse>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_refresh_session_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_refresh_session_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_auth_ffi_refresh_session_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_auth_ffi_refresh_session_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readAuthResponse(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<BooruSearchResultDto> _b10(
  PixevalNative ctx, {
  required String platformName,
  required String tags,
  required int page,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(platformName);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.string(tags);
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(
              Pointer<Uint8>,
              UintPtr,
              Pointer<Uint8>,
              UintPtr,
              Uint32,
            )
          >
        >("boltffi_function_pixeval_native_app_api_booru_ffi_search_booru")
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int, int)
        >()(p0, bytes0.length, p1, bytes1.length, page);
    return await ctx._scheduler.run<BooruSearchResultDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_booru_ffi_search_booru_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_booru_ffi_search_booru_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_booru_ffi_search_booru_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_booru_ffi_search_booru_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readBooruSearchResultDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<ParsedPixivLink> _b11(
  PixevalNative ctx, {
  required String url,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(url);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_deeplink_ffi_parse_pixiv_url",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<ParsedPixivLink>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_deeplink_ffi_parse_pixiv_url_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_deeplink_ffi_parse_pixiv_url_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_deeplink_ffi_parse_pixiv_url_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_deeplink_ffi_parse_pixiv_url_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readParsedPixivLink(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _b12(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(destination);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_download_ffi_cancel_download",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<bool>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_cancel_download_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_cancel_download_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_cancel_download_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result =
            _library
                .lookup<
                  NativeFunction<Uint8 Function(Uint64, Pointer<BoltStatus>)>
                >(
                  "boltffi_async_function_pixeval_native_app_api_download_ffi_cancel_download_complete",
                )
                .asFunction<int Function(int, Pointer<BoltStatus>)>()(
              handle,
              status,
            );
        ctx._bolt.check(status.ref);
        return result != 0;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b13(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_download_ffi_clear_all_downloads",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_clear_all_downloads_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_clear_all_downloads_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_clear_all_downloads_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        _library
            .lookup<NativeFunction<Void Function(Uint64, Pointer<BoltStatus>)>>(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_clear_all_downloads_complete",
            )
            .asFunction<void Function(int, Pointer<BoltStatus>)>()(
          handle,
          status,
        );
        ctx._bolt.check(status.ref);
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b14(
  PixevalNative ctx, {
  required int concurrency,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Uint32)>>(
          "boltffi_function_pixeval_native_app_api_download_ffi_init_download_manager",
        )
        .asFunction<int Function(int)>()(concurrency);
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_init_download_manager_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_init_download_manager_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_init_download_manager_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_init_download_manager_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _b15(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(destination);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_download_ffi_pause_download",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<bool>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_pause_download_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_pause_download_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_pause_download_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result =
            _library
                .lookup<
                  NativeFunction<Uint8 Function(Uint64, Pointer<BoltStatus>)>
                >(
                  "boltffi_async_function_pixeval_native_app_api_download_ffi_pause_download_complete",
                )
                .asFunction<int Function(int, Pointer<BoltStatus>)>()(
              handle,
              status,
            );
        ctx._bolt.check(status.ref);
        return result != 0;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _b16(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(destination);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_download_ffi_remove_download",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<bool>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_remove_download_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_remove_download_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_remove_download_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result =
            _library
                .lookup<
                  NativeFunction<Uint8 Function(Uint64, Pointer<BoltStatus>)>
                >(
                  "boltffi_async_function_pixeval_native_app_api_download_ffi_remove_download_complete",
                )
                .asFunction<int Function(int, Pointer<BoltStatus>)>()(
              handle,
              status,
            );
        ctx._bolt.check(status.ref);
        return result != 0;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _b17(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(destination);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_download_ffi_resume_download",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<bool>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_resume_download_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_resume_download_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_resume_download_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result =
            _library
                .lookup<
                  NativeFunction<Uint8 Function(Uint64, Pointer<BoltStatus>)>
                >(
                  "boltffi_async_function_pixeval_native_app_api_download_ffi_resume_download_complete",
                )
                .asFunction<int Function(int, Pointer<BoltStatus>)>()(
              handle,
              status,
            );
        ctx._bolt.check(status.ref);
        return result != 0;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b18(
  PixevalNative ctx, {
  required String url,
  required String destination,
  String? artworkId,
  required bool overwrite,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(url);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.string(destination);
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final w2 = WireWriter(endian: Endian.little);
    w2.optional(artworkId, (value) {
      w2.string(value);
    });
    final bytes2 = w2.takeBytes();
    final p2 = arena<Uint8>(bytes2.isEmpty ? 1 : bytes2.length);
    p2.asTypedList(bytes2.length).setAll(0, bytes2);
    final handle =
        _library
            .lookup<
              NativeFunction<
                Uint64 Function(
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                  Uint8,
                )
              >
            >(
              "boltffi_function_pixeval_native_app_api_download_ffi_start_download",
            )
            .asFunction<
              int Function(
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
                int,
              )
            >()(
          p0,
          bytes0.length,
          p1,
          bytes1.length,
          p2,
          bytes2.length,
          (overwrite ? 1 : 0),
        );
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_start_download_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_start_download_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_download_ffi_start_download_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_download_ffi_start_download_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<PixevalMakoIllustration> _b19(
  PixevalNative ctx, {
  required int illustId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "boltffi_function_pixeval_native_app_api_feed_ffi_get_illust_detail",
        )
        .asFunction<int Function(int)>()(illustId);
    return await ctx._scheduler.run<PixevalMakoIllustration>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_feed_ffi_get_illust_detail_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_feed_ffi_get_illust_detail_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_feed_ffi_get_illust_detail_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_feed_ffi_get_illust_detail_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readPixevalMakoIllustration(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _b20(
  PixevalNative ctx, {
  required int illustId,
  required bool isBookmarked,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Int64, Uint8)>>(
          "boltffi_function_pixeval_native_app_api_feed_ffi_toggle_bookmark",
        )
        .asFunction<int Function(int, int)>()(illustId, (isBookmarked ? 1 : 0));
    return await ctx._scheduler.run<bool>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_feed_ffi_toggle_bookmark_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_feed_ffi_toggle_bookmark_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_feed_ffi_toggle_bookmark_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<Uint8>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(Uint64, Pointer<BoltStatus>, Pointer<Uint8>)
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_feed_ffi_toggle_bookmark_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<Uint8>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.value;
        return result != 0;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<FilterAnalysisResultDto> _b21(
  PixevalNative ctx, {
  required String query,
  required int caretPos,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(query);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle =
        _library
            .lookup<
              NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr, Int32)>
            >(
              "boltffi_function_pixeval_native_app_api_filters_ffi_analyze_filter_query",
            )
            .asFunction<int Function(Pointer<Uint8>, int, int)>()(
          p0,
          bytes0.length,
          caretPos,
        );
    return await ctx._scheduler.run<FilterAnalysisResultDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_filters_ffi_analyze_filter_query_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_filters_ffi_analyze_filter_query_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_filters_ffi_analyze_filter_query_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_filters_ffi_analyze_filter_query_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readFilterAnalysisResultDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<bool>> _b22(
  PixevalNative ctx, {
  required String query,
  required List<PixevalFiltersArtworkMetadata> artworks,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(query);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.sequence(artworks, (value) {
      _writePixevalFiltersArtworkMetadata(w1, value, ctx, engine);
    });
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint8>, UintPtr)
          >
        >(
          "boltffi_function_pixeval_native_app_api_filters_ffi_filter_artworks_by_query",
        )
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int)
        >()(p0, bytes0.length, p1, bytes1.length);
    return await ctx._scheduler.run<List<bool>>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_filters_ffi_filter_artworks_by_query_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_filters_ffi_filter_artworks_by_query_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_filters_ffi_filter_artworks_by_query_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_filters_ffi_filter_artworks_by_query_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.sequence(() => r.boolean());
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b23(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_image_ffi_clear_image_cache",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_clear_image_cache_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_clear_image_cache_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_clear_image_cache_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_clear_image_cache_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<Uint8List> _b24(
  PixevalNative ctx, {
  required String url,
  String? referer,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(url);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.optional(referer, (value) {
      w1.string(value);
    });
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint8>, UintPtr)
          >
        >("boltffi_function_pixeval_native_app_api_image_ffi_fetch_image")
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int)
        >()(p0, bytes0.length, p1, bytes1.length);
    return await ctx._scheduler.run<Uint8List>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_fetch_image_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_fetch_image_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_fetch_image_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_fetch_image_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.buffer();
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<CacheStatsDto> _b25(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_image_ffi_get_cache_stats",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<CacheStatsDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cache_stats_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cache_stats_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cache_stats_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cache_stats_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readCacheStatsDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<Uint8List?> _b26(
  PixevalNative ctx, {
  required String url,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(url);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_image_ffi_get_cached_image",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<Uint8List?>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cached_image_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cached_image_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cached_image_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_get_cached_image_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.optional(() => r.buffer());
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b27(
  PixevalNative ctx, {
  required String cacheDir,
  BigInt? capacityBytes,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(cacheDir);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.optional(capacityBytes, (value) {
      w1.u64(value);
    });
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint8>, UintPtr)
          >
        >("boltffi_function_pixeval_native_app_api_image_ffi_init_image_cache")
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int)
        >()(p0, bytes0.length, p1, bytes1.length);
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_init_image_cache_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_init_image_cache_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_image_ffi_init_image_cache_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_image_ffi_init_image_cache_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<FlutterNetworkConfig> _b28(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_network_ffi_get_network_config",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<FlutterNetworkConfig>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_network_ffi_get_network_config_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_network_ffi_get_network_config_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_network_ffi_get_network_config_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_network_ffi_get_network_config_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readFlutterNetworkConfig(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<ConnectivityCheckResult> _b29(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_network_ffi_test_connectivity",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<ConnectivityCheckResult>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_network_ffi_test_connectivity_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_network_ffi_test_connectivity_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_network_ffi_test_connectivity_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_network_ffi_test_connectivity_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readConnectivityCheckResult(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b30(
  PixevalNative ctx, {
  required FlutterNetworkConfig config,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    _writeFlutterNetworkConfig(w0, config, ctx, engine);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_network_ffi_update_network_config",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_network_ffi_update_network_config_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_network_ffi_update_network_config_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_network_ffi_update_network_config_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_network_ffi_update_network_config_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<NovelContentDto> _b31(
  PixevalNative ctx, {
  required int novelId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "boltffi_function_pixeval_native_app_api_novel_ffi_get_novel_content_rendered",
        )
        .asFunction<int Function(int)>()(novelId);
    return await ctx._scheduler.run<NovelContentDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_content_rendered_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_content_rendered_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_content_rendered_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_content_rendered_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readNovelContentDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<NovelDetailDto> _b32(
  PixevalNative ctx, {
  required int novelId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "boltffi_function_pixeval_native_app_api_novel_ffi_get_novel_detail",
        )
        .asFunction<int Function(int)>()(novelId);
    return await ctx._scheduler.run<NovelDetailDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_detail_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_detail_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_detail_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_novel_ffi_get_novel_detail_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readNovelDetailDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<SauceNaoItemDto>> _b33(
  PixevalNative ctx, {
  required Uint8List imageBytes,
  String? apiKey,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.buffer(imageBytes);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.optional(apiKey, (value) {
      w1.string(value);
    });
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint8>, UintPtr)
          >
        >("boltffi_function_pixeval_native_app_api_saucenao_ffi_search_saucenao")
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int)
        >()(p0, bytes0.length, p1, bytes1.length);
    return await ctx._scheduler.run<List<SauceNaoItemDto>>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_saucenao_ffi_search_saucenao_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_saucenao_ffi_search_saucenao_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_saucenao_ffi_search_saucenao_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_saucenao_ffi_search_saucenao_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.sequence(() => _readSauceNaoItemDto(r, ctx, engine));
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<AutocompleteTagDto>> _b34(
  PixevalNative ctx, {
  required String word,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(word);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_search_ffi_get_search_autocomplete",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<List<AutocompleteTagDto>>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_search_ffi_get_search_autocomplete_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_search_ffi_get_search_autocomplete_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_search_ffi_get_search_autocomplete_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_search_ffi_get_search_autocomplete_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.sequence(
          () => _readAutocompleteTagDto(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<String> _b35(
  PixevalNative ctx, {
  required String message,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(message);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_simple_ffi_async_ping",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<String>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_simple_ffi_async_ping_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_simple_ffi_async_ping_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_simple_ffi_async_ping_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final result = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_simple_ffi_async_ping_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        ctx._bolt.check(status.ref);
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.string();
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b36(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_simple_ffi_init_app",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_simple_ffi_init_app_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_simple_ffi_init_app_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_simple_ffi_init_app_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        _library
            .lookup<NativeFunction<Void Function(Uint64, Pointer<BoltStatus>)>>(
              "boltffi_async_function_pixeval_native_app_api_simple_ffi_init_app_complete",
            )
            .asFunction<void Function(int, Pointer<BoltStatus>)>()(
          handle,
          status,
        );
        ctx._bolt.check(status.ref);
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b37(
  PixevalNative ctx, {
  required String keyword,
  String? translatedName,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(keyword);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.optional(translatedName, (value) {
      w1.string(value);
    });
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint8>, UintPtr)
          >
        >(
          "boltffi_function_pixeval_native_app_api_storage_ffi_add_search_history",
        )
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int)
        >()(p0, bytes0.length, p1, bytes1.length);
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_add_search_history_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_add_search_history_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_add_search_history_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_add_search_history_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b38(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_storage_ffi_clear_all_browse_history",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_browse_history_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_browse_history_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_browse_history_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_browse_history_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b39(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_function_pixeval_native_app_api_storage_ffi_clear_all_search_history",
        )
        .asFunction<int Function()>()();
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_search_history_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_search_history_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_search_history_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_clear_all_search_history_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _b40(
  PixevalNative ctx, {
  required String keyword,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(keyword);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_storage_ffi_delete_search_history_item",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<bool>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_delete_search_history_item_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_delete_search_history_item_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_delete_search_history_item_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<Uint8>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(Uint64, Pointer<BoltStatus>, Pointer<Uint8>)
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_delete_search_history_item_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<Uint8>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.value;
        return result != 0;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<BrowseHistoryItemDto>> _b41(
  PixevalNative ctx, {
  required int skip,
  required int take,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Uint32, Uint32)>>(
          "boltffi_function_pixeval_native_app_api_storage_ffi_get_browse_history",
        )
        .asFunction<int Function(int, int)>()(skip, take);
    return await ctx._scheduler.run<List<BrowseHistoryItemDto>>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_browse_history_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_browse_history_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_browse_history_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_browse_history_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.sequence(
          () => _readBrowseHistoryItemDto(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<SearchHistoryItemDto>> _b42(
  PixevalNative ctx, {
  required int limit,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Uint32)>>(
          "boltffi_function_pixeval_native_app_api_storage_ffi_get_search_history",
        )
        .asFunction<int Function(int)>()(limit);
    return await ctx._scheduler.run<List<SearchHistoryItemDto>>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_search_history_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_search_history_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_search_history_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_get_search_history_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.sequence(
          () => _readSearchHistoryItemDto(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b43(
  PixevalNative ctx, {
  required String dbPath,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(dbPath);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_function_pixeval_native_app_api_storage_ffi_init_storage",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_init_storage_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_init_storage_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_init_storage_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_init_storage_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b44(
  PixevalNative ctx, {
  required String workId,
  required String title,
  required String previewUrl,
  required String authorName,
  String? workType,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(workId);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.string(title);
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final w2 = WireWriter(endian: Endian.little);
    w2.string(previewUrl);
    final bytes2 = w2.takeBytes();
    final p2 = arena<Uint8>(bytes2.isEmpty ? 1 : bytes2.length);
    p2.asTypedList(bytes2.length).setAll(0, bytes2);
    final w3 = WireWriter(endian: Endian.little);
    w3.string(authorName);
    final bytes3 = w3.takeBytes();
    final p3 = arena<Uint8>(bytes3.isEmpty ? 1 : bytes3.length);
    p3.asTypedList(bytes3.length).setAll(0, bytes3);
    final w4 = WireWriter(endian: Endian.little);
    w4.optional(workType, (value) {
      w4.string(value);
    });
    final bytes4 = w4.takeBytes();
    final p4 = arena<Uint8>(bytes4.isEmpty ? 1 : bytes4.length);
    p4.asTypedList(bytes4.length).setAll(0, bytes4);
    final handle =
        _library
            .lookup<
              NativeFunction<
                Uint64 Function(
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                )
              >
            >(
              "boltffi_function_pixeval_native_app_api_storage_ffi_record_browse_history",
            )
            .asFunction<
              int Function(
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
              )
            >()(
          p0,
          bytes0.length,
          p1,
          bytes1.length,
          p2,
          bytes2.length,
          p3,
          bytes3.length,
          p4,
          bytes4.length,
        );
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_record_browse_history_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_record_browse_history_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_storage_ffi_record_browse_history_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_storage_ffi_record_browse_history_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _b45(
  PixevalNative ctx, {
  required String zipPath,
  required String outputPath,
  required String formatName,
  required List<int> frameDelaysMs,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(zipPath);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.string(outputPath);
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final w2 = WireWriter(endian: Endian.little);
    w2.string(formatName);
    final bytes2 = w2.takeBytes();
    final p2 = arena<Uint8>(bytes2.isEmpty ? 1 : bytes2.length);
    p2.asTypedList(bytes2.length).setAll(0, bytes2);
    final p3 = arena<Uint32>(frameDelaysMs.isEmpty ? 1 : frameDelaysMs.length);
    for (var n = 0; n < frameDelaysMs.length; n++) {
      p3[n] = frameDelaysMs[n];
    }
    final handle =
        _library
            .lookup<
              NativeFunction<
                Uint64 Function(
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint8>,
                  UintPtr,
                  Pointer<Uint32>,
                  UintPtr,
                )
              >
            >("boltffi_function_pixeval_native_app_api_ugoira_ffi_export_ugoira")
            .asFunction<
              int Function(
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
                Pointer<Uint8>,
                int,
                Pointer<Uint32>,
                int,
              )
            >()(
          p0,
          bytes0.length,
          p1,
          bytes1.length,
          p2,
          bytes2.length,
          p3,
          frameDelaysMs.length,
        );
    return await ctx._scheduler.run<void>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_export_ugoira_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_export_ugoira_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_export_ugoira_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<BoltBuffer Function(Uint64, Pointer<BoltStatus>)>
            >(
              "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_export_ugoira_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>)
            >()(handle, status);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        return;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<UgoiraFramesDto> _b46(
  PixevalNative ctx, {
  required Uint8List zipBytes,
  required List<int> delays,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.buffer(zipBytes);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final p1 = arena<Uint32>(delays.isEmpty ? 1 : delays.length);
    for (var n = 0; n < delays.length; n++) {
      p1[n] = delays[n];
    }
    final handle = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint32>, UintPtr)
          >
        >(
          "boltffi_function_pixeval_native_app_api_ugoira_ffi_extract_ugoira_frames_from_zip_bytes",
        )
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint32>, int)
        >()(p0, bytes0.length, p1, delays.length);
    return await ctx._scheduler.run<UgoiraFramesDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_extract_ugoira_frames_from_zip_bytes_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_extract_ugoira_frames_from_zip_bytes_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_extract_ugoira_frames_from_zip_bytes_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_extract_ugoira_frames_from_zip_bytes_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readUgoiraFramesDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<UgoiraMetadataDto> _b47(
  PixevalNative ctx, {
  required int illustId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "boltffi_function_pixeval_native_app_api_ugoira_ffi_get_ugoira_metadata",
        )
        .asFunction<int Function(int)>()(illustId);
    return await ctx._scheduler.run<UgoiraMetadataDto>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_get_ugoira_metadata_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_get_ugoira_metadata_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_get_ugoira_metadata_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_function_pixeval_native_app_api_ugoira_ffi_get_ugoira_metadata_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readUgoiraMetadataDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

DownloadEventReceiver _b48(PixevalNative ctx) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final result = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_init_class_pixeval_native_app_events_download_event_receiver_new",
        )
        .asFunction<int Function()>()();
    return DownloadEventReceiver._(ctx, result, engine);
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

SessionEventReceiver _b49(PixevalNative ctx) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final result = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_init_class_pixeval_native_app_events_session_event_receiver_new",
        )
        .asFunction<int Function()>()();
    return SessionEventReceiver._(ctx, result, engine);
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

void _b50(PixevalNative ctx, {required DownloadEventReceiver receiver}) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final receiverHandle = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    _library
        .lookup<NativeFunction<Void Function(Uint64)>>(
          "boltffi_method_class_pixeval_native_app_events_download_event_receiver_cancel",
        )
        .asFunction<void Function(int)>()(receiverHandle);
    return;
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<DownloadProgressEvent?> _b51(
  PixevalNative ctx, {
  required DownloadEventReceiver receiver,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final receiverHandle = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Uint64)>>(
          "boltffi_method_class_pixeval_native_app_events_download_event_receiver_next",
        )
        .asFunction<int Function(int)>()(receiverHandle);
    return await ctx._scheduler.run<DownloadProgressEvent?>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_method_class_pixeval_native_app_events_download_event_receiver_next_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_method_class_pixeval_native_app_events_download_event_receiver_next_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_method_class_pixeval_native_app_events_download_event_receiver_next_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_method_class_pixeval_native_app_events_download_event_receiver_next_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.optional(
          () => _readDownloadProgressEvent(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

void _b52(PixevalNative ctx, {required SessionEventReceiver receiver}) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final receiverHandle = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    _library
        .lookup<NativeFunction<Void Function(Uint64)>>(
          "boltffi_method_class_pixeval_native_app_events_session_event_receiver_cancel",
        )
        .asFunction<void Function(int)>()(receiverHandle);
    return;
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<SessionEvent?> _b53(
  PixevalNative ctx, {
  required SessionEventReceiver receiver,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final receiverHandle = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Uint64)>>(
          "boltffi_method_class_pixeval_native_app_events_session_event_receiver_next",
        )
        .asFunction<int Function(int)>()(receiverHandle);
    return await ctx._scheduler.run<SessionEvent?>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_method_class_pixeval_native_app_events_session_event_receiver_next_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_method_class_pixeval_native_app_events_session_event_receiver_next_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_method_class_pixeval_native_app_events_session_event_receiver_next_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_method_class_pixeval_native_app_events_session_event_receiver_next_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = r.optional(() => _readSessionEvent(r, ctx, engine));
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _b54(PixevalNative ctx, {required String restrict}) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(restrict);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final result = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Uint8>, UintPtr)>>(
          "boltffi_init_class_pixeval_native_app_feed_app_illustration_feed_following",
        )
        .asFunction<int Function(Pointer<Uint8>, int)>()(p0, bytes0.length);
    return AppIllustrationFeed._(ctx, result, engine);
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _b55(
  PixevalNative ctx, {
  required String mode,
  String? date,
}) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(mode);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.optional(date, (value) {
      w1.string(value);
    });
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final result = _library
        .lookup<
          NativeFunction<
            Uint64 Function(Pointer<Uint8>, UintPtr, Pointer<Uint8>, UintPtr)
          >
        >(
          "boltffi_init_class_pixeval_native_app_feed_app_illustration_feed_ranking",
        )
        .asFunction<
          int Function(Pointer<Uint8>, int, Pointer<Uint8>, int)
        >()(p0, bytes0.length, p1, bytes1.length);
    return AppIllustrationFeed._(ctx, result, engine);
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _b56(PixevalNative ctx) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final result = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "boltffi_init_class_pixeval_native_app_feed_app_illustration_feed_recommended",
        )
        .asFunction<int Function()>()();
    return AppIllustrationFeed._(ctx, result, engine);
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _b57(
  PixevalNative ctx, {
  required String word,
  String? sort,
  String? searchTarget,
}) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final w0 = WireWriter(endian: Endian.little);
    w0.string(word);
    final bytes0 = w0.takeBytes();
    final p0 = arena<Uint8>(bytes0.isEmpty ? 1 : bytes0.length);
    p0.asTypedList(bytes0.length).setAll(0, bytes0);
    final w1 = WireWriter(endian: Endian.little);
    w1.optional(sort, (value) {
      w1.string(value);
    });
    final bytes1 = w1.takeBytes();
    final p1 = arena<Uint8>(bytes1.isEmpty ? 1 : bytes1.length);
    p1.asTypedList(bytes1.length).setAll(0, bytes1);
    final w2 = WireWriter(endian: Endian.little);
    w2.optional(searchTarget, (value) {
      w2.string(value);
    });
    final bytes2 = w2.takeBytes();
    final p2 = arena<Uint8>(bytes2.isEmpty ? 1 : bytes2.length);
    p2.asTypedList(bytes2.length).setAll(0, bytes2);
    final output = arena<Uint64>();
    final errorBuffer = _library
        .lookup<
          NativeFunction<
            BoltBuffer Function(
              Pointer<Uint8>,
              UintPtr,
              Pointer<Uint8>,
              UintPtr,
              Pointer<Uint8>,
              UintPtr,
              Pointer<Uint64>,
            )
          >
        >(
          "boltffi_init_class_pixeval_native_app_feed_app_illustration_feed_search",
        )
        .asFunction<
          BoltBuffer Function(
            Pointer<Uint8>,
            int,
            Pointer<Uint8>,
            int,
            Pointer<Uint8>,
            int,
            Pointer<Uint64>,
          )
        >()(p0, bytes0.length, p1, bytes1.length, p2, bytes2.length, output);
    if (errorBuffer.length != 0) {
      final r = WireReader(ctx._bolt.lift(errorBuffer), endian: Endian.little);
      final error = r.string();
      r.finish();
      throw error;
    }
    ctx._bolt.lift(errorBuffer);
    final result = output.value;
    return AppIllustrationFeed._(ctx, result, engine);
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

void _b58(PixevalNative ctx, {required AppIllustrationFeed receiver}) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final receiverHandle = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    _library
        .lookup<NativeFunction<Void Function(Uint64)>>(
          "boltffi_method_class_pixeval_native_app_feed_app_illustration_feed_cancel",
        )
        .asFunction<void Function(int)>()(receiverHandle);
    return;
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

String? _b59(PixevalNative ctx, {required AppIllustrationFeed receiver}) {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final receiverHandle = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final result = _library
        .lookup<NativeFunction<BoltBuffer Function(Uint64)>>(
          "boltffi_method_class_pixeval_native_app_feed_app_illustration_feed_filter_query",
        )
        .asFunction<BoltBuffer Function(int)>()(receiverHandle);
    final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
    final decoded = r.optional(() => r.string());
    r.finish();
    return decoded;
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<PixevalMakoIllustrationFeedPage> _b60(
  PixevalNative ctx, {
  required AppIllustrationFeed receiver,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  const engine = NativeEngine.boltffi;
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final receiverHandle = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final handle = _library
        .lookup<NativeFunction<Uint64 Function(Uint64)>>(
          "boltffi_method_class_pixeval_native_app_feed_app_illustration_feed_next_page",
        )
        .asFunction<int Function(int)>()(receiverHandle);
    return await ctx._scheduler.run<PixevalMakoIllustrationFeedPage>(
      handle: handle,
      poll: (h, callback, id) {
        _library
            .lookup<
              NativeFunction<
                Int8 Function(
                  Uint64,
                  Uint64,
                  Pointer<NativeFunction<NativeWake>>,
                )
              >
            >(
              "boltffi_async_method_class_pixeval_native_app_feed_app_illustration_feed_next_page_poll",
            )
            .asFunction<
              int Function(int, int, Pointer<NativeFunction<NativeWake>>)
            >()(h, id, callback);
      },
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_method_class_pixeval_native_app_feed_app_illustration_feed_next_page_cancel",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "boltffi_async_method_class_pixeval_native_app_feed_app_illustration_feed_next_page_free",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<BoltStatus>();
        final output = arena<BoltBuffer>();
        final errorBuffer = _library
            .lookup<
              NativeFunction<
                BoltBuffer Function(
                  Uint64,
                  Pointer<BoltStatus>,
                  Pointer<BoltBuffer>,
                )
              >
            >(
              "boltffi_async_method_class_pixeval_native_app_feed_app_illustration_feed_next_page_complete",
            )
            .asFunction<
              BoltBuffer Function(int, Pointer<BoltStatus>, Pointer<BoltBuffer>)
            >()(handle, status, output);
        final errorBytes = ctx._bolt.lift(errorBuffer);
        ctx._bolt.check(status.ref);
        if (errorBytes.isNotEmpty) {
          final r = WireReader(errorBytes, endian: Endian.little);
          final error = r.string();
          r.finish();
          throw error;
        }
        final result = output.ref;
        final r = WireReader(ctx._bolt.lift(result), endian: Endian.little);
        final decoded = _readPixevalMakoIllustrationFeedPage(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}
