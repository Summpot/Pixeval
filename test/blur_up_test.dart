import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pixeval_flutter/src/rust/frb_generated.dart';
import 'package:pixeval_flutter/src/rust/api/feed.dart';
import 'package:pixeval_flutter/src/ui/components/artwork_card.dart';
import 'package:pixeval_flutter/src/ui/components/blur_up.dart';

import 'widget_test.dart' as mocks;

final png = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==',
);

class ImageApi extends mocks.MockRustLibApi {
  final full = Completer<Uint8List>();
  final requests = <String>[];
  @override
  Future<Uint8List> crateApiImageFetchImage({
    required String url,
    String? referer,
  }) {
    requests.add(url);
    return url == 'full-image' ? full.future : Future.value(png);
  }
}

void main() {
  testWidgets(
    'card decodes an independent thumbnail while the larger image is pending',
    (tester) async {
      final api = ImageApi();
      RustLib.initMock(api: api);
      const item = IllustSummary(
        id: 1,
        title: 'Artwork',
        caption: '',
        userId: 2,
        userName: 'Artist',
        userAccount: 'artist',
        userAvatarUrl: '',
        previewUrl: 'full-image',
        thumbnailUrl: 'tiny-thumbnail',
        originalUrl: 'original',
        width: 600,
        height: 800,
        pageCount: 1,
        isBookmarked: false,
        totalBookmarks: 0,
        totalViews: 0,
        tags: [],
        isR18: false,
        isUgoira: false,
        pageUrls: [],
        originalPageUrls: [],
      );
      try {
        await tester.pumpWidget(
          const MaterialApp(
            home: Center(
              child: SizedBox(width: 200, child: ArtworkCard(illust: item)),
            ),
          ),
        );
        await tester.pump();
        expect(api.requests, containsAll(['full-image', 'tiny-thumbnail']));
        expect(api.full.isCompleted, isFalse);
        expect(find.byType(BlurUpPlaceholder), findsOneWidget);
        api.full.complete(png);
        await tester.runAsync(
          () => Future<void>.delayed(const Duration(milliseconds: 40)),
        );
        await tester.pumpAndSettle();
        expect(find.byType(FadeInArtwork), findsOneWidget);
        await tester.pumpWidget(const SizedBox.shrink());
      } finally {
        RustLib.dispose();
        PaintingBinding.instance.imageCache.clear();
      }
    },
  );
}
