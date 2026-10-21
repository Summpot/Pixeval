import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:pixeval_flutter/src/rust/api/auth.dart';
import 'package:pixeval_flutter/src/rust/frb_generated.dart';
import 'package:pixeval_flutter/src/state/auth_state.dart';

import 'widget_test.dart' as mocks;

class PersistedApi extends mocks.MockRustLibApi {
  final receivedTokens = <String>[];
  @override
  Future<String?> crateApiAuthGetStoredRefreshToken() async =>
      'rotated-on-background-request';
  @override
  Future<AuthResponse> crateApiAuthLoginByRefreshToken({
    required String refreshToken,
  }) async {
    receivedTokens.add(refreshToken);
    return super.crateApiAuthLoginByRefreshToken(refreshToken: refreshToken);
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test(
    'restart prefers the rotated Rust credential over legacy preferences',
    () async {
      SharedPreferences.setMockInitialValues({
        'pixeval_refresh_token': 'stale',
      });
      final api = PersistedApi();
      RustLib.initMock(api: api);
      final container = ProviderContainer();
      final ready = Completer<void>();
      container.listen(authProvider, (_, next) {
        if (next.user != null && !ready.isCompleted) ready.complete();
      });
      try {
        await ready.future.timeout(const Duration(seconds: 3));
        expect(api.receivedTokens, ['rotated-on-background-request']);
        expect(
          (await SharedPreferences.getInstance()).getString(
            'pixeval_refresh_token',
          ),
          'mock_refresh',
        );
      } finally {
        container.dispose();
        RustLib.dispose();
      }
    },
  );
}
