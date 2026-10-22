import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:extended_image/extended_image.dart';

import '../image/rust_image_provider.dart';
import '../state/download_state.dart';

class DownloadManagerPage extends ConsumerStatefulWidget {
  const DownloadManagerPage({super.key});

  @override
  ConsumerState<DownloadManagerPage> createState() =>
      _DownloadManagerPageState();
}

class _DownloadManagerPageState extends ConsumerState<DownloadManagerPage>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final downloadState = ref.watch(downloadProvider);
    final notifier = ref.read(downloadProvider.notifier);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Download Manager'),
        actions: [
          if (downloadState.completedItems.isNotEmpty)
            TextButton.icon(
              onPressed: () => notifier.clearCompleted(),
              icon: const Icon(Icons.clear_all),
              label: const Text('Clear Finished'),
            ),
        ],
        bottom: TabBar(
          controller: _tabController,
          tabs: [
            Tab(text: 'All (${downloadState.allItems.length})'),
            Tab(text: 'Active (${downloadState.activeItems.length})'),
            Tab(text: 'Completed (${downloadState.completedItems.length})'),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabController,
        children: [
          _DownloadListView(
            items: downloadState.allItems,
            onPause: (dest) => notifier.pause(dest),
            onResume: (dest) => notifier.resume(dest),
            onCancel: (dest) => notifier.cancel(dest),
          ),
          _DownloadListView(
            items: downloadState.activeItems,
            onPause: (dest) => notifier.pause(dest),
            onResume: (dest) => notifier.resume(dest),
            onCancel: (dest) => notifier.cancel(dest),
          ),
          _DownloadListView(
            items: downloadState.completedItems,
            onPause: (dest) => notifier.pause(dest),
            onResume: (dest) => notifier.resume(dest),
            onCancel: (dest) => notifier.cancel(dest),
          ),
        ],
      ),
    );
  }
}

class _DownloadListView extends StatelessWidget {
  final List<DownloadItem> items;
  final ValueChanged<String> onPause;
  final ValueChanged<String> onResume;
  final ValueChanged<String> onCancel;

  const _DownloadListView({
    required this.items,
    required this.onPause,
    required this.onResume,
    required this.onCancel,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    if (items.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.download_done_outlined,
              size: 64,
              color: Colors.grey[400],
            ),
            const SizedBox(height: 12),
            Text(
              'No downloads in this section',
              style: TextStyle(color: Colors.grey[600], fontSize: 16),
            ),
          ],
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(12),
      itemCount: items.length,
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final item = items[index];
        final filename = item.destination.split(RegExp(r'[/\\]')).last;

        return Card(
          elevation: 1,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          child: Padding(
            padding: const EdgeInsets.all(12.0),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                // Thumbnail
                ClipRRect(
                  borderRadius: BorderRadius.circular(8),
                  child: SizedBox(
                    width: 56,
                    height: 56,
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
                const SizedBox(width: 12),

                // Details & Progress
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        filename,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 14,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        item.title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(color: Colors.grey[600], fontSize: 12),
                      ),
                      const SizedBox(height: 8),
                      LinearProgressIndicator(
                        value: item.isCompleted
                            ? 1.0
                            : (item.progressPercentage / 100.0).clamp(0.0, 1.0),
                        backgroundColor:
                            theme.colorScheme.surfaceContainerHighest,
                      ),
                      const SizedBox(height: 6),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            item.formattedSize,
                            style: TextStyle(
                              color: Colors.grey[600],
                              fontSize: 11,
                            ),
                          ),
                          _StatusBadge(state: item.state),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),

                // Actions
                if (item.isRunning)
                  IconButton(
                    icon: const Icon(Icons.pause_circle_outline),
                    onPressed: () => onPause(item.destination),
                  )
                else if (item.isPaused)
                  IconButton(
                    icon: const Icon(Icons.play_circle_outline),
                    onPressed: () => onResume(item.destination),
                  )
                else
                  IconButton(
                    icon: const Icon(Icons.close, color: Colors.grey),
                    onPressed: () => onCancel(item.destination),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _StatusBadge extends StatelessWidget {
  final String state;

  const _StatusBadge({required this.state});

  @override
  Widget build(BuildContext context) {
    Color bg;
    Color fg;

    switch (state) {
      case 'Completed':
        bg = Colors.green.withValues(alpha: 0.15);
        fg = Colors.green[700]!;
        break;
      case 'Running':
        bg = Colors.blue.withValues(alpha: 0.15);
        fg = Colors.blue[700]!;
        break;
      case 'Paused':
        bg = Colors.orange.withValues(alpha: 0.15);
        fg = Colors.orange[800]!;
        break;
      case 'Error':
        bg = Colors.red.withValues(alpha: 0.15);
        fg = Colors.red[700]!;
        break;
      default:
        bg = Colors.grey.withValues(alpha: 0.15);
        fg = Colors.grey[700]!;
        break;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        state,
        style: TextStyle(color: fg, fontSize: 10, fontWeight: FontWeight.bold),
      ),
    );
  }
}
