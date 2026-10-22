import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:pixeval_flutter/src/native/api/auth.dart';
import 'package:pixeval_flutter/src/native/native_api.dart';
import 'package:pixeval_flutter/src/state/auth_state.dart';

import 'widget_test.dart' as mocks;

class PersistedApi extends mocks.MockPixevalApi {
  final receivedTokens = <String>[];
  @override
  Future<String?> getStoredRefreshToken() async =>
      'rotated-on-background-request';
  @override
  Future<AuthResponse> loginByRefreshToken({
    required String refreshToken,
  }) async {
    receivedTokens.add(refreshToken);
    return super.loginByRefreshToken(refreshToken: refreshToken);
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
      NativeApi.initMock(api: api);
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
        NativeApi.dispose();
      }
    },
  );
}
