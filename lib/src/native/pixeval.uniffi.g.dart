// Generated.
// ignore_for_file: unused_local_variable, no_leading_underscores_for_local_identifiers, curly_braces_in_flow_control_structures
part of 'pixeval.g.dart';

Future<String> _u0(
  PixevalNative ctx, {
  required String message,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(message));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_ping",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<String>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        return utf8.decode(rt.lift(value), allowMalformed: false);
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

String _u1(PixevalNative ctx) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<NativeFunction<RustBuffer Function(Pointer<RustCallStatus>)>>(
          "uniffi_pixeval_native_fn_func_version",
        )
        .asFunction<RustBuffer Function(Pointer<RustCallStatus>)>();
    args.transfer();
    final result = call(status);
    rt.check(status.ref);
    return utf8.decode(rt.lift(result), allowMalformed: false);
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<PkceAuthParamsDto> _u2(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_create_pkce_auth_params",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<PkceAuthParamsDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readPkceAuthParamsDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<UserProfile?> _u3(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_get_current_user",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<UserProfile?>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = r.optional(() => _readUserProfile(r, ctx, engine));
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<String?> _u4(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_get_refresh_token",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<String?>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = r.optional(() => r.string());
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<String?> _u5(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_get_stored_refresh_token",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<String?>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.optional(() => r.string());
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<AuthResponse> _u6(
  PixevalNative ctx, {
  required String code,
  required String codeVerifier,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(code));
    final p0 = args.buffer(rt, bytes0);
    final bytes1 = Uint8List.fromList(utf8.encode(codeVerifier));
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_login_by_code",
        )
        .asFunction<int Function(RustBuffer, RustBuffer)>();
    args.transfer();
    final result = call(p0, p1);
    return await ctx._scheduler.run<AuthResponse>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readAuthResponse(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<AuthResponse> _u7(
  PixevalNative ctx, {
  required String refreshToken,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(refreshToken));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_login_by_refresh_token",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<AuthResponse>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readAuthResponse(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u8(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_logout",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<AuthResponse> _u9(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_refresh_session",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<AuthResponse>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readAuthResponse(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<BooruSearchResultDto> _u10(
  PixevalNative ctx, {
  required String platformName,
  required String tags,
  required int page,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(platformName));
    final p0 = args.buffer(rt, bytes0);
    final bytes1 = Uint8List.fromList(utf8.encode(tags));
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<
          NativeFunction<Uint64 Function(RustBuffer, RustBuffer, Uint32)>
        >("uniffi_pixeval_native_fn_func_search_booru")
        .asFunction<int Function(RustBuffer, RustBuffer, int)>();
    args.transfer();
    final result = call(p0, p1, page);
    return await ctx._scheduler.run<BooruSearchResultDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readBooruSearchResultDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<ParsedPixivLink> _u11(
  PixevalNative ctx, {
  required String url,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(url));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_parse_pixiv_url",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<ParsedPixivLink>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = _readParsedPixivLink(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _u12(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(destination));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_cancel_download",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<bool>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_i8")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_i8",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_i8",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value =
            _library
                .lookup<
                  NativeFunction<Int8 Function(Uint64, Pointer<RustCallStatus>)>
                >("ffi_pixeval_native_rust_future_complete_i8")
                .asFunction<int Function(int, Pointer<RustCallStatus>)>()(
              handle,
              status,
            );
        rt.check(status.ref);
        return value != 0;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u13(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_clear_all_downloads",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref);
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u14(
  PixevalNative ctx, {
  required int concurrency,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Uint32)>>(
          "uniffi_pixeval_native_fn_func_init_download_manager",
        )
        .asFunction<int Function(int)>();
    args.transfer();
    final result = call(concurrency);
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _u15(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(destination));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_pause_download",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<bool>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_i8")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_i8",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_i8",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value =
            _library
                .lookup<
                  NativeFunction<Int8 Function(Uint64, Pointer<RustCallStatus>)>
                >("ffi_pixeval_native_rust_future_complete_i8")
                .asFunction<int Function(int, Pointer<RustCallStatus>)>()(
              handle,
              status,
            );
        rt.check(status.ref);
        return value != 0;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _u16(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(destination));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_remove_download",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<bool>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_i8")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_i8",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_i8",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value =
            _library
                .lookup<
                  NativeFunction<Int8 Function(Uint64, Pointer<RustCallStatus>)>
                >("ffi_pixeval_native_rust_future_complete_i8")
                .asFunction<int Function(int, Pointer<RustCallStatus>)>()(
              handle,
              status,
            );
        rt.check(status.ref);
        return value != 0;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _u17(
  PixevalNative ctx, {
  required String destination,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(destination));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_resume_download",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<bool>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_i8")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_i8",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_i8",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value =
            _library
                .lookup<
                  NativeFunction<Int8 Function(Uint64, Pointer<RustCallStatus>)>
                >("ffi_pixeval_native_rust_future_complete_i8")
                .asFunction<int Function(int, Pointer<RustCallStatus>)>()(
              handle,
              status,
            );
        rt.check(status.ref);
        return value != 0;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u18(
  PixevalNative ctx, {
  required String url,
  required String destination,
  String? artworkId,
  required bool overwrite,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(url));
    final p0 = args.buffer(rt, bytes0);
    final bytes1 = Uint8List.fromList(utf8.encode(destination));
    final p1 = args.buffer(rt, bytes1);
    final writer2 = WireWriter();
    writer2.optional(artworkId, (value) {
      writer2.string(value);
    });
    final bytes2 = writer2.takeBytes();
    final p2 = args.buffer(rt, bytes2);
    final call = _library
        .lookup<
          NativeFunction<
            Uint64 Function(RustBuffer, RustBuffer, RustBuffer, Int8)
          >
        >("uniffi_pixeval_native_fn_func_start_download")
        .asFunction<int Function(RustBuffer, RustBuffer, RustBuffer, int)>();
    args.transfer();
    final result = call(p0, p1, p2, (overwrite ? 1 : 0));
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<PixevalMakoIllustration> _u19(
  PixevalNative ctx, {
  required int illustId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "uniffi_pixeval_native_fn_func_get_illust_detail",
        )
        .asFunction<int Function(int)>();
    args.transfer();
    final result = call(illustId);
    return await ctx._scheduler.run<PixevalMakoIllustration>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readPixevalMakoIllustration(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _u20(
  PixevalNative ctx, {
  required int illustId,
  required bool isBookmarked,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Int64, Int8)>>(
          "uniffi_pixeval_native_fn_func_toggle_bookmark",
        )
        .asFunction<int Function(int, int)>();
    args.transfer();
    final result = call(illustId, (isBookmarked ? 1 : 0));
    return await ctx._scheduler.run<bool>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_i8")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_i8",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_i8",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value =
            _library
                .lookup<
                  NativeFunction<Int8 Function(Uint64, Pointer<RustCallStatus>)>
                >("ffi_pixeval_native_rust_future_complete_i8")
                .asFunction<int Function(int, Pointer<RustCallStatus>)>()(
              handle,
              status,
            );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return value != 0;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<FilterAnalysisResultDto> _u21(
  PixevalNative ctx, {
  required String query,
  required int caretPos,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(query));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, Int32)>>(
          "uniffi_pixeval_native_fn_func_analyze_filter_query",
        )
        .asFunction<int Function(RustBuffer, int)>();
    args.transfer();
    final result = call(p0, caretPos);
    return await ctx._scheduler.run<FilterAnalysisResultDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = _readFilterAnalysisResultDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<bool>> _u22(
  PixevalNative ctx, {
  required String query,
  required List<PixevalFiltersArtworkMetadata> artworks,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(query));
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.sequence(artworks, (value) {
      _writePixevalFiltersArtworkMetadata(writer1, value, ctx, engine);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_filter_artworks_by_query",
        )
        .asFunction<int Function(RustBuffer, RustBuffer)>();
    args.transfer();
    final result = call(p0, p1);
    return await ctx._scheduler.run<List<bool>>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.sequence(() => r.boolean());
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u23(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_clear_image_cache",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<Uint8List> _u24(
  PixevalNative ctx, {
  required String url,
  String? referer,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(url));
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.optional(referer, (value) {
      writer1.string(value);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_fetch_image",
        )
        .asFunction<int Function(RustBuffer, RustBuffer)>();
    args.transfer();
    final result = call(p0, p1);
    return await ctx._scheduler.run<Uint8List>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.buffer();
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<CacheStatsDto> _u25(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_get_cache_stats",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<CacheStatsDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = _readCacheStatsDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<Uint8List?> _u26(
  PixevalNative ctx, {
  required String url,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(url));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_get_cached_image",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<Uint8List?>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = r.optional(() => r.buffer());
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u27(
  PixevalNative ctx, {
  required String cacheDir,
  BigInt? capacityBytes,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(cacheDir));
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.optional(capacityBytes, (value) {
      writer1.u64(value);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_init_image_cache",
        )
        .asFunction<int Function(RustBuffer, RustBuffer)>();
    args.transfer();
    final result = call(p0, p1);
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<FlutterNetworkConfig> _u28(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_get_network_config",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<FlutterNetworkConfig>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = _readFlutterNetworkConfig(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<ConnectivityCheckResult> _u29(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_test_connectivity",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<ConnectivityCheckResult>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        final r = WireReader(rt.lift(value));
        final decoded = _readConnectivityCheckResult(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u30(
  PixevalNative ctx, {
  required FlutterNetworkConfig config,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final writer0 = WireWriter();
    _writeFlutterNetworkConfig(writer0, config, ctx, engine);
    final bytes0 = writer0.takeBytes();
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_update_network_config",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<NovelContentDto> _u31(
  PixevalNative ctx, {
  required int novelId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "uniffi_pixeval_native_fn_func_get_novel_content_rendered",
        )
        .asFunction<int Function(int)>();
    args.transfer();
    final result = call(novelId);
    return await ctx._scheduler.run<NovelContentDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readNovelContentDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<NovelDetailDto> _u32(
  PixevalNative ctx, {
  required int novelId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "uniffi_pixeval_native_fn_func_get_novel_detail",
        )
        .asFunction<int Function(int)>();
    args.transfer();
    final result = call(novelId);
    return await ctx._scheduler.run<NovelDetailDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readNovelDetailDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<SauceNaoItemDto>> _u33(
  PixevalNative ctx, {
  required Uint8List imageBytes,
  String? apiKey,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final writer0 = WireWriter();
    writer0.buffer(imageBytes);
    final bytes0 = writer0.takeBytes();
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.optional(apiKey, (value) {
      writer1.string(value);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_search_saucenao",
        )
        .asFunction<int Function(RustBuffer, RustBuffer)>();
    args.transfer();
    final result = call(p0, p1);
    return await ctx._scheduler.run<List<SauceNaoItemDto>>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.sequence(() => _readSauceNaoItemDto(r, ctx, engine));
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<AutocompleteTagDto>> _u34(
  PixevalNative ctx, {
  required String word,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(word));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_get_search_autocomplete",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<List<AutocompleteTagDto>>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.sequence(
          () => _readAutocompleteTagDto(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<String> _u35(
  PixevalNative ctx, {
  required String message,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(message));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_async_ping",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<String>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref);
        return utf8.decode(rt.lift(value), allowMalformed: false);
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u36(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_init_app",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref);
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u37(
  PixevalNative ctx, {
  required String keyword,
  String? translatedName,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(keyword));
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.optional(translatedName, (value) {
      writer1.string(value);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_add_search_history",
        )
        .asFunction<int Function(RustBuffer, RustBuffer)>();
    args.transfer();
    final result = call(p0, p1);
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u38(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_clear_all_browse_history",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u39(
  PixevalNative ctx, {
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function()>>(
          "uniffi_pixeval_native_fn_func_clear_all_search_history",
        )
        .asFunction<int Function()>();
    args.transfer();
    final result = call();
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<bool> _u40(
  PixevalNative ctx, {
  required String keyword,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(keyword));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_delete_search_history_item",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<bool>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_i8")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_i8",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_i8",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value =
            _library
                .lookup<
                  NativeFunction<Int8 Function(Uint64, Pointer<RustCallStatus>)>
                >("ffi_pixeval_native_rust_future_complete_i8")
                .asFunction<int Function(int, Pointer<RustCallStatus>)>()(
              handle,
              status,
            );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return value != 0;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<BrowseHistoryItemDto>> _u41(
  PixevalNative ctx, {
  required int skip,
  required int take,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Uint32, Uint32)>>(
          "uniffi_pixeval_native_fn_func_get_browse_history",
        )
        .asFunction<int Function(int, int)>();
    args.transfer();
    final result = call(skip, take);
    return await ctx._scheduler.run<List<BrowseHistoryItemDto>>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.sequence(
          () => _readBrowseHistoryItemDto(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<List<SearchHistoryItemDto>> _u42(
  PixevalNative ctx, {
  required int limit,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Uint32)>>(
          "uniffi_pixeval_native_fn_func_get_search_history",
        )
        .asFunction<int Function(int)>();
    args.transfer();
    final result = call(limit);
    return await ctx._scheduler.run<List<SearchHistoryItemDto>>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.sequence(
          () => _readSearchHistoryItemDto(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u43(
  PixevalNative ctx, {
  required String dbPath,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(dbPath));
    final p0 = args.buffer(rt, bytes0);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_init_storage",
        )
        .asFunction<int Function(RustBuffer)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u44(
  PixevalNative ctx, {
  required String workId,
  required String title,
  required String previewUrl,
  required String authorName,
  String? workType,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(workId));
    final p0 = args.buffer(rt, bytes0);
    final bytes1 = Uint8List.fromList(utf8.encode(title));
    final p1 = args.buffer(rt, bytes1);
    final bytes2 = Uint8List.fromList(utf8.encode(previewUrl));
    final p2 = args.buffer(rt, bytes2);
    final bytes3 = Uint8List.fromList(utf8.encode(authorName));
    final p3 = args.buffer(rt, bytes3);
    final writer4 = WireWriter();
    writer4.optional(workType, (value) {
      writer4.string(value);
    });
    final bytes4 = writer4.takeBytes();
    final p4 = args.buffer(rt, bytes4);
    final call = _library
        .lookup<
          NativeFunction<
            Uint64 Function(
              RustBuffer,
              RustBuffer,
              RustBuffer,
              RustBuffer,
              RustBuffer,
            )
          >
        >("uniffi_pixeval_native_fn_func_record_browse_history")
        .asFunction<
          int Function(
            RustBuffer,
            RustBuffer,
            RustBuffer,
            RustBuffer,
            RustBuffer,
          )
        >();
    args.transfer();
    final result = call(p0, p1, p2, p3, p4);
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<void> _u45(
  PixevalNative ctx, {
  required String zipPath,
  required String outputPath,
  required String formatName,
  required List<int> frameDelaysMs,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(zipPath));
    final p0 = args.buffer(rt, bytes0);
    final bytes1 = Uint8List.fromList(utf8.encode(outputPath));
    final p1 = args.buffer(rt, bytes1);
    final bytes2 = Uint8List.fromList(utf8.encode(formatName));
    final p2 = args.buffer(rt, bytes2);
    final writer3 = WireWriter();
    writer3.sequence(frameDelaysMs, (value) {
      writer3.u32(value);
    });
    final bytes3 = writer3.takeBytes();
    final p3 = args.buffer(rt, bytes3);
    final call = _library
        .lookup<
          NativeFunction<
            Uint64 Function(RustBuffer, RustBuffer, RustBuffer, RustBuffer)
          >
        >("uniffi_pixeval_native_fn_func_export_ugoira")
        .asFunction<
          int Function(RustBuffer, RustBuffer, RustBuffer, RustBuffer)
        >();
    args.transfer();
    final result = call(p0, p1, p2, p3);
    return await ctx._scheduler.run<void>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_void")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_void",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_void",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        _library
            .lookup<
              NativeFunction<Void Function(Uint64, Pointer<RustCallStatus>)>
            >("ffi_pixeval_native_rust_future_complete_void")
            .asFunction<void Function(int, Pointer<RustCallStatus>)>()(
          handle,
          status,
        );
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        return;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<UgoiraFramesDto> _u46(
  PixevalNative ctx, {
  required Uint8List zipBytes,
  required List<int> delays,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final writer0 = WireWriter();
    writer0.buffer(zipBytes);
    final bytes0 = writer0.takeBytes();
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.sequence(delays, (value) {
      writer1.u32(value);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final call = _library
        .lookup<NativeFunction<Uint64 Function(RustBuffer, RustBuffer)>>(
          "uniffi_pixeval_native_fn_func_extract_ugoira_frames_from_zip_bytes",
        )
        .asFunction<int Function(RustBuffer, RustBuffer)>();
    args.transfer();
    final result = call(p0, p1);
    return await ctx._scheduler.run<UgoiraFramesDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readUgoiraFramesDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<UgoiraMetadataDto> _u47(
  PixevalNative ctx, {
  required int illustId,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Int64)>>(
          "uniffi_pixeval_native_fn_func_get_ugoira_metadata",
        )
        .asFunction<int Function(int)>();
    args.transfer();
    final result = call(illustId);
    return await ctx._scheduler.run<UgoiraMetadataDto>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readUgoiraMetadataDto(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

DownloadEventReceiver _u48(PixevalNative ctx) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<Pointer<Void> Function(Pointer<RustCallStatus>)>
        >("uniffi_pixeval_native_fn_constructor_downloadeventreceiver_new")
        .asFunction<Pointer<Void> Function(Pointer<RustCallStatus>)>();
    args.transfer();
    final result = call(status);
    rt.check(status.ref);
    return DownloadEventReceiver._(ctx, result.address, engine);
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

SessionEventReceiver _u49(PixevalNative ctx) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<Pointer<Void> Function(Pointer<RustCallStatus>)>
        >("uniffi_pixeval_native_fn_constructor_sessioneventreceiver_new")
        .asFunction<Pointer<Void> Function(Pointer<RustCallStatus>)>();
    args.transfer();
    final result = call(status);
    rt.check(status.ref);
    return SessionEventReceiver._(ctx, result.address, engine);
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

void _u50(PixevalNative ctx, {required DownloadEventReceiver receiver}) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final lease0 = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final status0 = arena<RustCallStatus>();
    final p0 = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_clone_downloadeventreceiver")
        .asFunction<
          Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
        >()(Pointer<Void>.fromAddress(lease0), status0);
    rt.check(status0.ref);
    args.own(() {
      _library
          .lookup<
            NativeFunction<
              Void Function(Pointer<Void>, Pointer<RustCallStatus>)
            >
          >("uniffi_pixeval_native_fn_free_downloadeventreceiver")
          .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(
        p0,
        status0,
      );
      rt.check(status0.ref);
    });
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<Void Function(Pointer<Void>, Pointer<RustCallStatus>)>
        >("uniffi_pixeval_native_fn_method_downloadeventreceiver_cancel")
        .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>();
    args.transfer();
    call(p0, status);
    rt.check(status.ref);
    return;
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<DownloadProgressEvent?> _u51(
  PixevalNative ctx, {
  required DownloadEventReceiver receiver,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final lease0 = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final status0 = arena<RustCallStatus>();
    final p0 = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_clone_downloadeventreceiver")
        .asFunction<
          Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
        >()(Pointer<Void>.fromAddress(lease0), status0);
    rt.check(status0.ref);
    args.own(() {
      _library
          .lookup<
            NativeFunction<
              Void Function(Pointer<Void>, Pointer<RustCallStatus>)
            >
          >("uniffi_pixeval_native_fn_free_downloadeventreceiver")
          .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(
        p0,
        status0,
      );
      rt.check(status0.ref);
    });
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Void>)>>(
          "uniffi_pixeval_native_fn_method_downloadeventreceiver_next",
        )
        .asFunction<int Function(Pointer<Void>)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<DownloadProgressEvent?>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.optional(
          () => _readDownloadProgressEvent(r, ctx, engine),
        );
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

void _u52(PixevalNative ctx, {required SessionEventReceiver receiver}) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final lease0 = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final status0 = arena<RustCallStatus>();
    final p0 = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_clone_sessioneventreceiver")
        .asFunction<
          Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
        >()(Pointer<Void>.fromAddress(lease0), status0);
    rt.check(status0.ref);
    args.own(() {
      _library
          .lookup<
            NativeFunction<
              Void Function(Pointer<Void>, Pointer<RustCallStatus>)
            >
          >("uniffi_pixeval_native_fn_free_sessioneventreceiver")
          .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(
        p0,
        status0,
      );
      rt.check(status0.ref);
    });
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<Void Function(Pointer<Void>, Pointer<RustCallStatus>)>
        >("uniffi_pixeval_native_fn_method_sessioneventreceiver_cancel")
        .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>();
    args.transfer();
    call(p0, status);
    rt.check(status.ref);
    return;
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<SessionEvent?> _u53(
  PixevalNative ctx, {
  required SessionEventReceiver receiver,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final lease0 = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final status0 = arena<RustCallStatus>();
    final p0 = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_clone_sessioneventreceiver")
        .asFunction<
          Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
        >()(Pointer<Void>.fromAddress(lease0), status0);
    rt.check(status0.ref);
    args.own(() {
      _library
          .lookup<
            NativeFunction<
              Void Function(Pointer<Void>, Pointer<RustCallStatus>)
            >
          >("uniffi_pixeval_native_fn_free_sessioneventreceiver")
          .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(
        p0,
        status0,
      );
      rt.check(status0.ref);
    });
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Void>)>>(
          "uniffi_pixeval_native_fn_method_sessioneventreceiver_next",
        )
        .asFunction<int Function(Pointer<Void>)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<SessionEvent?>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = r.optional(() => _readSessionEvent(r, ctx, engine));
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _u54(PixevalNative ctx, {required String restrict}) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(restrict));
    final p0 = args.buffer(rt, bytes0);
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(RustBuffer, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_constructor_appillustrationfeed_following")
        .asFunction<
          Pointer<Void> Function(RustBuffer, Pointer<RustCallStatus>)
        >();
    args.transfer();
    final result = call(p0, status);
    rt.check(status.ref);
    return AppIllustrationFeed._(ctx, result.address, engine);
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _u55(
  PixevalNative ctx, {
  required String mode,
  String? date,
}) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(mode));
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.optional(date, (value) {
      writer1.string(value);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(
              RustBuffer,
              RustBuffer,
              Pointer<RustCallStatus>,
            )
          >
        >("uniffi_pixeval_native_fn_constructor_appillustrationfeed_ranking")
        .asFunction<
          Pointer<Void> Function(
            RustBuffer,
            RustBuffer,
            Pointer<RustCallStatus>,
          )
        >();
    args.transfer();
    final result = call(p0, p1, status);
    rt.check(status.ref);
    return AppIllustrationFeed._(ctx, result.address, engine);
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _u56(PixevalNative ctx) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<Pointer<Void> Function(Pointer<RustCallStatus>)>
        >("uniffi_pixeval_native_fn_constructor_appillustrationfeed_recommended")
        .asFunction<Pointer<Void> Function(Pointer<RustCallStatus>)>();
    args.transfer();
    final result = call(status);
    rt.check(status.ref);
    return AppIllustrationFeed._(ctx, result.address, engine);
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

AppIllustrationFeed _u57(
  PixevalNative ctx, {
  required String word,
  String? sort,
  String? searchTarget,
}) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final bytes0 = Uint8List.fromList(utf8.encode(word));
    final p0 = args.buffer(rt, bytes0);
    final writer1 = WireWriter();
    writer1.optional(sort, (value) {
      writer1.string(value);
    });
    final bytes1 = writer1.takeBytes();
    final p1 = args.buffer(rt, bytes1);
    final writer2 = WireWriter();
    writer2.optional(searchTarget, (value) {
      writer2.string(value);
    });
    final bytes2 = writer2.takeBytes();
    final p2 = args.buffer(rt, bytes2);
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(
              RustBuffer,
              RustBuffer,
              RustBuffer,
              Pointer<RustCallStatus>,
            )
          >
        >("uniffi_pixeval_native_fn_constructor_appillustrationfeed_search")
        .asFunction<
          Pointer<Void> Function(
            RustBuffer,
            RustBuffer,
            RustBuffer,
            Pointer<RustCallStatus>,
          )
        >();
    args.transfer();
    final result = call(p0, p1, p2, status);
    rt.check(status.ref, (bytes) {
      final r = WireReader(bytes);
      final value = r.string();
      r.finish();
      return value;
    });
    return AppIllustrationFeed._(ctx, result.address, engine);
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

void _u58(PixevalNative ctx, {required AppIllustrationFeed receiver}) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final lease0 = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final status0 = arena<RustCallStatus>();
    final p0 = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_clone_appillustrationfeed")
        .asFunction<
          Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
        >()(Pointer<Void>.fromAddress(lease0), status0);
    rt.check(status0.ref);
    args.own(() {
      _library
          .lookup<
            NativeFunction<
              Void Function(Pointer<Void>, Pointer<RustCallStatus>)
            >
          >("uniffi_pixeval_native_fn_free_appillustrationfeed")
          .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(
        p0,
        status0,
      );
      rt.check(status0.ref);
    });
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<Void Function(Pointer<Void>, Pointer<RustCallStatus>)>
        >("uniffi_pixeval_native_fn_method_appillustrationfeed_cancel")
        .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>();
    args.transfer();
    call(p0, status);
    rt.check(status.ref);
    return;
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

String? _u59(PixevalNative ctx, {required AppIllustrationFeed receiver}) {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final lease0 = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final status0 = arena<RustCallStatus>();
    final p0 = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_clone_appillustrationfeed")
        .asFunction<
          Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
        >()(Pointer<Void>.fromAddress(lease0), status0);
    rt.check(status0.ref);
    args.own(() {
      _library
          .lookup<
            NativeFunction<
              Void Function(Pointer<Void>, Pointer<RustCallStatus>)
            >
          >("uniffi_pixeval_native_fn_free_appillustrationfeed")
          .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(
        p0,
        status0,
      );
      rt.check(status0.ref);
    });
    final status = arena<RustCallStatus>();
    final call = _library
        .lookup<
          NativeFunction<
            RustBuffer Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_method_appillustrationfeed_filter_query")
        .asFunction<
          RustBuffer Function(Pointer<Void>, Pointer<RustCallStatus>)
        >();
    args.transfer();
    final result = call(p0, status);
    rt.check(status.ref);
    final r = WireReader(rt.lift(result));
    final decoded = r.optional(() => r.string());
    r.finish();
    return decoded;
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}

Future<PixevalMakoIllustrationFeedPage> _u60(
  PixevalNative ctx, {
  required AppIllustrationFeed receiver,
  NativeCancellationToken? cancellationToken,
}) async {
  final _library = ctx._library;
  final rt = ctx._runtime("pixeval_native");
  const engine = NativeEngine.uniffi;
  final args = UniFfiArguments();
  final arena = Arena();
  final leases = <NativeHandle>[];
  try {
    final lease0 = receiver._handle.acquire(engine, ctx._library);
    leases.add(receiver._handle);
    final status0 = arena<RustCallStatus>();
    final p0 = _library
        .lookup<
          NativeFunction<
            Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
          >
        >("uniffi_pixeval_native_fn_clone_appillustrationfeed")
        .asFunction<
          Pointer<Void> Function(Pointer<Void>, Pointer<RustCallStatus>)
        >()(Pointer<Void>.fromAddress(lease0), status0);
    rt.check(status0.ref);
    args.own(() {
      _library
          .lookup<
            NativeFunction<
              Void Function(Pointer<Void>, Pointer<RustCallStatus>)
            >
          >("uniffi_pixeval_native_fn_free_appillustrationfeed")
          .asFunction<void Function(Pointer<Void>, Pointer<RustCallStatus>)>()(
        p0,
        status0,
      );
      rt.check(status0.ref);
    });
    final call = _library
        .lookup<NativeFunction<Uint64 Function(Pointer<Void>)>>(
          "uniffi_pixeval_native_fn_method_appillustrationfeed_next_page",
        )
        .asFunction<int Function(Pointer<Void>)>();
    args.transfer();
    final result = call(p0);
    return await ctx._scheduler.run<PixevalMakoIllustrationFeedPage>(
      handle: result,
      poll: _library
          .lookup<
            NativeFunction<
              Void Function(Uint64, Pointer<NativeFunction<NativeWake>>, Uint64)
            >
          >("ffi_pixeval_native_rust_future_poll_rust_buffer")
          .asFunction<
            void Function(int, Pointer<NativeFunction<NativeWake>>, int)
          >(),
      cancel: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_cancel_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      free: _library
          .lookup<NativeFunction<Void Function(Uint64)>>(
            "ffi_pixeval_native_rust_future_free_rust_buffer",
          )
          .asFunction<void Function(int)>(),
      cancellationToken: cancellationToken,
      complete: (handle) {
        final status = arena<RustCallStatus>();
        final value = _library
            .lookup<
              NativeFunction<
                RustBuffer Function(Uint64, Pointer<RustCallStatus>)
              >
            >("ffi_pixeval_native_rust_future_complete_rust_buffer")
            .asFunction<
              RustBuffer Function(int, Pointer<RustCallStatus>)
            >()(handle, status);
        rt.check(status.ref, (bytes) {
          final r = WireReader(bytes);
          final value = r.string();
          r.finish();
          return value;
        });
        final r = WireReader(rt.lift(value));
        final decoded = _readPixevalMakoIllustrationFeedPage(r, ctx, engine);
        r.finish();
        return decoded;
      },
    );
  } finally {
    args.dispose();
    arena.releaseAll();
    for (final lease in leases.reversed) {
      lease.releaseCall();
    }
  }
}
