import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pixeval_flutter/main.dart';
import 'package:pixeval_flutter/src/native/api/auth.dart';
import 'package:pixeval_flutter/src/native/api/deeplink.dart';
import 'package:pixeval_flutter/src/native/native_api.dart';
import 'package:pixeval_flutter/src/state/auth_state.dart';

import 'widget_test.dart' as mocks;

class PendingAuth extends AuthNotifier {
  @override
  AuthState build() => const AuthState(isLoading: true);

  void finishRestore() {
    state = const AuthState(
      user: UserProfile(
        id: '1',
        name: 'Alice',
        account: 'alice',
        mailAddress: '',
        isPremium: false,
        avatarUrl: '',
      ),
    );
  }
}

class LinkApi extends mocks.MockPixevalApi {
  final parsed = <String>[];

  @override
  Future<ParsedPixivLink> parsePixivUrl({required String url}) async {
    parsed.add(url);
    return ParsedPixivLink.unknown(rawUrl: url);
  }
}

void main() {
  const channel = MethodChannel('io.github.pixeval.pixeval_flutter/deeplink');
  testWidgets(
    'cold links wait for authentication; warm links use the same handler',
    (tester) async {
      final api = LinkApi();
      NativeApi.initMock(api: api);
      final container = ProviderContainer(
        overrides: [authProvider.overrideWith(PendingAuth.new)],
      );
      const coldLink = 'https://www.pixiv.net/artworks/1';
      const warmLink = 'pixiv://illusts/2';
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(channel, (
        call,
      ) async {
        expect(call.method, 'getPendingLinks');
        return [coldLink];
      });
      try {
        await tester.pumpWidget(
          UncontrolledProviderScope(
            container: container,
            child: const PixevalApp(),
          ),
        );
        await tester.pumpAndSettle();
        expect(api.parsed, isEmpty);
        (container.read(authProvider.notifier) as PendingAuth).finishRestore();
        await tester.pumpAndSettle();
        expect(api.parsed, [coldLink]);

        final delivered = Completer<void>();
        tester.binding.channelBuffers.push(
          channel.name,
          const StandardMethodCodec().encodeMethodCall(
            const MethodCall('onLink', warmLink),
          ),
          (_) => delivered.complete(),
        );
        await tester.pumpAndSettle();
        await delivered.future;
        expect(api.parsed, [coldLink, warmLink]);
      } finally {
        await tester.pumpWidget(const SizedBox.shrink());
        await tester.pump();
        tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
          channel,
          null,
        );
        container.dispose();
        NativeApi.dispose();
      }
    },
  );
}
