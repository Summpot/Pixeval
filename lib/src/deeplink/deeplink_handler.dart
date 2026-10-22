import 'package:flutter/material.dart';

import '../native/api/deeplink.dart';
import '../native/api/feed.dart';
import '../ui/artwork_detail_page.dart';
import '../ui/novel_reader_page.dart';

class DeeplinkHandler {
  static Future<void> handleUrl(BuildContext context, String url) async {
    final parsed = await parsePixivUrl(url: url);
    if (!context.mounted) return;

    await parsed.when<Future<void>>(
      artwork: (id) async {
        try {
          final detail = await getIllustDetail(illustId: id);
          if (context.mounted) {
            Navigator.of(context).push(
              MaterialPageRoute(
                builder: (_) => ArtworkDetailPage(illust: detail),
              ),
            );
          }
        } catch (e) {
          if (context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text('Failed to load artwork from link: $e')),
            );
          }
        }
      },
      novel: (id) async {
        Navigator.of(
          context,
        ).push(MaterialPageRoute(builder: (_) => NovelReaderPage(novelId: id)));
      },
      user: (id) async {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('Pixiv user link: ID $id')));
      },
      unknown: (raw) async {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Unknown or unsupported Pixiv link: $raw')),
        );
      },
    );
  }
}
