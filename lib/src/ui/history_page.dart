import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:extended_image/extended_image.dart';
import '../image/rust_image_provider.dart';
import '../rust/api/feed.dart';
import '../state/history_state.dart';
import 'artwork_detail_page.dart';
import 'novel_reader_page.dart';

class HistoryPage extends ConsumerWidget {
  const HistoryPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final history = ref.watch(browseHistoryProvider);
    final notifier = ref.read(browseHistoryProvider.notifier);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Browsing History'),
        actions: [
          if (history.isNotEmpty)
            IconButton(
              icon: const Icon(Icons.delete_outline),
              tooltip: 'Clear History',
              onPressed: () async {
                final confirm = await showDialog<bool>(
                  context: context,
                  builder: (ctx) => AlertDialog(
                    title: const Text('Clear Browsing History?'),
                    content: const Text('This will remove all locally stored browsing records.'),
                    actions: [
                      TextButton(
                        onPressed: () => Navigator.of(ctx).pop(false),
                        child: const Text('Cancel'),
                      ),
                      FilledButton(
                        onPressed: () => Navigator.of(ctx).pop(true),
                        child: const Text('Clear'),
                      ),
                    ],
                  ),
                );
                if (confirm == true) {
                  await notifier.clear();
                }
              },
            ),
        ],
      ),
      body: history.isEmpty
          ? Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.history, size: 64, color: Colors.grey[400]),
                  const SizedBox(height: 12),
                  Text(
                    'No browsing history yet',
                    style: TextStyle(color: Colors.grey[600], fontSize: 16),
                  ),
                ],
              ),
            )
          : ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              itemCount: history.length,
              separatorBuilder: (_, _) => const Divider(height: 1),
              itemBuilder: (context, index) {
                final item = history[index];
                return ListTile(
                  contentPadding: const EdgeInsets.symmetric(vertical: 4),
                  leading: ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: SizedBox(
                      width: 52,
                      height: 52,
                      child: item.previewUrl.isNotEmpty
                          ? ExtendedImage(
                              image: RustImageProvider(item.previewUrl),
                              fit: BoxFit.cover,
                            )
                          : Container(
                              color: Colors.grey.withValues(alpha: 0.2),
                              child: const Icon(Icons.image),
                            ),
                    ),
                  ),
                  title: Row(
                    children: [
                      if (item.workType == 'novel') ...[
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                          margin: const EdgeInsets.only(right: 6),
                          decoration: BoxDecoration(
                            color: Colors.deepPurple.withValues(alpha: 0.15),
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: const Text(
                            'Novel',
                            style: TextStyle(
                              color: Colors.deepPurple,
                              fontSize: 10,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                      Expanded(
                        child: Text(
                          item.title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                        ),
                      ),
                    ],
                  ),
                  subtitle: Text(
                    item.authorName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(color: Colors.grey[600], fontSize: 12),
                  ),
                  trailing: const Icon(Icons.chevron_right, size: 18),
                  onTap: () async {
                    final id = int.tryParse(item.id);
                    if (id == null) return;

                    if (item.workType == 'novel') {
                      Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => NovelReaderPage(
                            novelId: id,
                            initialTitle: item.title,
                          ),
                        ),
                      );
                      return;
                    }

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
                          SnackBar(content: Text('Failed to load artwork: $e')),
                        );
                      }
                    }
                  },
                );
              },
            ),
    );
  }
}
