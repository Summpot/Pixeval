import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:pixeval_flutter/src/native/api/auth.dart';
import 'package:pixeval_flutter/src/native/native_api.dart';
import 'package:pixeval_flutter/src/state/auth_state.dart';
import 'package:pixeval_flutter/src/ui/login_page.dart';

import 'widget_test.dart' as mocks;

class OAuthMockApi extends mocks.MockPixevalApi {
  String? lastCode;
  String? lastVerifier;

  @override
  Future<AuthResponse> loginByCode({
    required String code,
    required String codeVerifier,
  }) async {
    lastCode = code;
    lastVerifier = codeVerifier;
    if (code == 'invalid_code') {
      throw 'Authentication error: {"has_error":true,"errors":{"system":{"message":"\\u4e0d\\u6b63\\u306a\\u30ea\\u30af\\u30a8\\u30b9\\u30c8\\u3059\\u3002","code":918}},"error":"invalid_request"}';
    }
    return super.loginByCode(code: code, codeVerifier: codeVerifier);
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('OAuth URL and Code Extraction', () {
    test('extractOAuthCode handles pixiv custom scheme callback', () {
      final code = extractOAuthCode(
        'pixiv://account/login?code=auth_code_12345',
      );
      expect(code, 'auth_code_12345');
    });

    test('extractOAuthCode handles trailing slash in pixiv scheme', () {
      final code = extractOAuthCode(
        'pixiv://account/login/?code=auth_code_12345',
      );
      expect(code, 'auth_code_12345');
    });

    test('extractOAuthCode handles https callback url', () {
      final code = extractOAuthCode(
        'https://app-api.pixiv.net/web/v1/users/auth/pixiv/callback?code=auth_code_https',
      );
      expect(code, 'auth_code_https');
    });

    test('extractOAuthCode handles intent url format', () {
      final code = extractOAuthCode(
        'intent://account/login?code=auth_code_intent#Intent;scheme=pixiv;package=io.github.pixeval.pixeval_flutter;end',
      );
      expect(code, 'auth_code_intent');
    });

    test('extractOAuthCode handles raw code string', () {
      final code = extractOAuthCode('  JH4XC8ZR_raw_code-test  ');
      expect(code, 'JH4XC8ZR_raw_code-test');
    });

    test(
      'extractOAuthCode rejects authorization request URL with helpful error',
      () {
        expect(
          () => extractOAuthCode(
            'https://app-api.pixiv.net/web/v1/login?code_challenge=EjfFir&code_challenge_method=S256&client=pixiv-android',
          ),
          throwsA(
            isA<FormatException>().having(
              (e) => e.message,
              'message',
              contains('发起授权的登录链接（包含 code_challenge）'),
            ),
          ),
        );
      },
    );

    test('extractOAuthCode rejects url without code', () {
      expect(
        () => extractOAuthCode('https://www.pixiv.net/artworks/123'),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('未包含 Pixiv 授权码'),
          ),
        ),
      );
    });
  });

  group('Callback URL Identification', () {
    test('isPixivCallbackUrl distinguishes callback from login request', () {
      // Login request MUST NOT be identified as callback
      expect(
        isPixivCallbackUrl(
          'https://app-api.pixiv.net/web/v1/login?code_challenge=EjfFir&code_challenge_method=S256&client=pixiv-android',
        ),
        isFalse,
      );

      // Valid callbacks
      expect(isPixivCallbackUrl('pixiv://account/login?code=xyz'), isTrue);
      expect(
        isPixivCallbackUrl(
          'https://app-api.pixiv.net/web/v1/users/auth/pixiv/callback?code=xyz',
        ),
        isTrue,
      );
      expect(
        isPixivCallbackUrl(
          'intent://account/login?code=xyz#Intent;scheme=pixiv;end',
        ),
        isTrue,
      );

      // Random text
      expect(isPixivCallbackUrl('https://www.google.com'), isFalse);
      expect(isPixivCallbackUrl(''), isFalse);
    });
  });

  group('Error Message Formatting', () {
    test('formatAuthErrorMessage decodes unicode and humanizes invalid_request', () {
      const rawError =
          'Exchange code error: Authentication error: {"has_error":true,"errors":{"system":{"message":"\\u4e0d\\u6b63\\u306a\\u30ea\\u30af\\u30a8\\u30b9\\u30c8\\u3059\\u3002","code":918}},"error":"invalid_request"}';
      final formatted = formatAuthErrorMessage(rawError);
      expect(formatted, contains('授权请求无效 (invalid_request)'));
      expect(formatted, contains('重新生成授权链接'));
      expect(formatted.contains(r'\u4e0d\u6b63'), isFalse);
    });
  });

  group('AuthNotifier OAuth Input Flow', () {
    test(
      'loginWithOAuthInput succeeds with saved verifier and callback link',
      () async {
        SharedPreferences.setMockInitialValues({
          'pixeval_pkce_verifier': 'saved_mock_verifier',
        });
        final api = OAuthMockApi();
        NativeApi.initMock(api: api);
        final container = ProviderContainer();

        try {
          final notifier = container.read(authProvider.notifier);
          final success = await notifier.loginWithOAuthInput(
            'pixiv://account/login?code=valid_test_code',
          );

          expect(success, isTrue);
          expect(api.lastCode, 'valid_test_code');
          expect(api.lastVerifier, 'saved_mock_verifier');
          expect(container.read(authProvider).isLoggedIn, isTrue);
        } finally {
          container.dispose();
          NativeApi.dispose();
        }
      },
    );

    test(
      'loginWithOAuthInput surfaces formatted error on invalid code',
      () async {
        SharedPreferences.setMockInitialValues({
          'pixeval_pkce_verifier': 'saved_mock_verifier',
        });
        final api = OAuthMockApi();
        NativeApi.initMock(api: api);
        final container = ProviderContainer();

        try {
          final notifier = container.read(authProvider.notifier);
          final success = await notifier.loginWithOAuthInput(
            'pixiv://account/login?code=invalid_code',
          );

          expect(success, isFalse);
          final err = container.read(authProvider).errorMessage;
          expect(err, isNotNull);
          expect(err, contains('授权请求无效 (invalid_request)'));
        } finally {
          container.dispose();
          NativeApi.dispose();
        }
      },
    );
  });

  group('LoginGuidePage Widget Behavior', () {
    testWidgets('pasting login URL displays immediate warning banner', (
      tester,
    ) async {
      SharedPreferences.setMockInitialValues({});
      final api = OAuthMockApi();
      NativeApi.initMock(api: api);

      try {
        await tester.pumpWidget(
          const ProviderScope(child: MaterialApp(home: LoginGuidePage())),
        );
        await tester.pumpAndSettle();

        final inputFinder = find.byType(TextFormField).first;
        await tester.enterText(
          inputFinder,
          'https://app-api.pixiv.net/web/v1/login?code_challenge=EjfFir&client=pixiv-android',
        );
        await tester.pumpAndSettle();

        expect(find.textContaining('当前输入框中是发起授权的登录链接'), findsOneWidget);
      } finally {
        NativeApi.dispose();
      }
    });
  });
}
