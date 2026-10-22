import 'dart:ffi';

import 'package:uniffi_dart/uniffi_dart.dart';

import 'api_contract.dart';
import 'pixeval.g.dart';
import 'service.dart';

export 'api_contract.dart';

export 'package:uniffi_dart/uniffi_dart.dart' show NativeEngine;

/// Application entrypoint. Tests inject the application API without loading
/// native code; production uses generated, verified native asset bindings.
final class NativeApi {
  NativeApi._();
  static final instance = NativeApi._();
  PixevalApi? _api;
  PixevalApi get api =>
      _api ?? (throw StateError('NativeApi has not been initialized'));
  static Future<void> init({
    DynamicLibrary? library,
    NativeEngine? engine,
    PixevalApi? api,
  }) async {
    await dispose();
    final implementation =
        api ??
        PixevalService(
          PixevalNative(
            library == null
                ? pixevalNativeAssetLibrary()
                : NativeLibrary.dynamic(library),
            engine: engine,
          ),
        );
    instance._api = implementation;
    try {
      await implementation.initApp();
    } catch (_) {
      instance._api = null;
      await implementation.close();
      rethrow;
    }
  }

  static void initMock({required PixevalApi api}) {
    instance._api = api;
  }

  static Future<void> dispose() async {
    final api = instance._api;
    instance._api = null;
    await api?.close();
  }
}
