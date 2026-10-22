import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../constants/app_assets.dart';
import '../image/rust_image_provider.dart';
import '../native/api/booru.dart';
import '../native/api/feed.dart';
import '../native/api/image.dart';
import '../native/api/saucenao.dart';
import '../state/history_state.dart';
import '../state/search_state.dart';
import 'artwork_detail_page.dart';
import 'components/artwork_waterfall.dart';

class SearchPage extends StatelessWidget {
  final String? initialQuery;

  const SearchPage({super.key, this.initialQuery});

  @override
  Widget build(BuildContext context) => ProviderScope(
    overrides: [searchProvider.overrideWith(SearchNotifier.new)],
    child: _SearchView(initialQuery: initialQuery),
  );
}

class _SearchView extends ConsumerStatefulWidget {
  const _SearchView({this.initialQuery});
  final String? initialQuery;
  @override
  ConsumerState<_SearchView> createState() => _SearchPageState();
}

class _SearchPageState extends ConsumerState<_SearchView> {
  late TextEditingController _textController;

  static const _popularTags = [
    'original',
    'Frieren',
    'GenshinImpact',
    'BlueArchive',
    'Arknights',
    'HonkaiStarRail',
    'Hololive',
    'VOCALOID',
  ];

  @override
  void initState() {
    super.initState();
    _textController = TextEditingController(text: widget.initialQuery ?? '');
    if (widget.initialQuery != null && widget.initialQuery!.isNotEmpty) {
      Future.microtask(() {
        _triggerSearch(widget.initialQuery!);
      });
    }
  }

  @override
  void dispose() {
    _textController.dispose();
    super.dispose();
  }

  void _triggerSearch(String keyword) {
    final clean = keyword.trim();
    if (clean.isEmpty) return;
    _textController.text = clean;
    ref.read(searchProvider.notifier).search(clean, refresh: true);
    ref.read(searchHistoryProvider.notifier).record(clean);
  }

  void _showSauceNaoDialog() {
    final urlController = TextEditingController();
    List<SauceNaoItemDto>? results;
    bool isSearching = false;
    String? error;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setModalState) => DraggableScrollableSheet(
          expand: false,
          initialChildSize: 0.7,
          maxChildSize: 0.9,
          builder: (_, scrollCtrl) => Padding(
            padding: const EdgeInsets.all(16.0),
            child: ListView(
              controller: scrollCtrl,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'SauceNAO 以图搜图',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.pop(ctx),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                const Text(
                  '输入图片直链 URL 搜索 Pixiv 及其他平台出处：',
                  style: TextStyle(fontSize: 12, color: Colors.grey),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: urlController,
                        decoration: const InputDecoration(
                          hintText: 'https://...',
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    FilledButton(
                      onPressed: isSearching
                          ? null
                          : () async {
                              final u = urlController.text.trim();
                              if (u.isEmpty) return;
                              setModalState(() {
                                isSearching = true;
                                error = null;
                              });
                              try {
                                final bytes = await fetchImage(url: u);
                                final res = await searchSaucenao(
                                  imageBytes: bytes,
                                );
                                if (!ctx.mounted) return;
                                setModalState(() {
                                  results = res;
                                  isSearching = false;
                                });
                              } catch (e) {
                                if (!ctx.mounted) return;
                                setModalState(() {
                                  error = e.toString();
                                  isSearching = false;
                                });
                              }
                            },
                      child: isSearching
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('搜图'),
                    ),
                  ],
                ),
                if (error != null) ...[
                  const SizedBox(height: 12),
                  Text(
                    '搜图失败: $error',
                    style: const TextStyle(color: Colors.red, fontSize: 13),
                  ),
                ],
                if (results != null) ...[
                  const SizedBox(height: 16),
                  Text(
                    '搜索结果 (${results!.length})',
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  for (final item in results!)
                    Card(
                      child: ListTile(
                        leading: Image(
                          image: RustImageProvider(item.thumbnailUrl),
                          width: 48,
                          height: 48,
                          fit: BoxFit.cover,
                          errorBuilder: (context, error, stackTrace) =>
                              const Icon(Icons.image),
                        ),
                        title: Text(
                          item.title.isNotEmpty ? item.title : '未知作品',
                        ),
                        subtitle: Text(
                          '${item.platform} • 作者: ${item.authorName} • 相似度: ${item.similarity.toStringAsFixed(1)}%',
                        ),
                        trailing: const Icon(Icons.open_in_new, size: 18),
                        onTap: () async {
                          if (item.artworkId != null &&
                              item.platform.toLowerCase().contains('pixiv')) {
                            Navigator.pop(ctx);
                            final id = int.tryParse(item.artworkId!);
                            if (id != null) {
                              try {
                                final detail = await getIllustDetail(
                                  illustId: id,
                                );
                                if (mounted) {
                                  Navigator.push(
                                    context,
                                    MaterialPageRoute(
                                      builder: (_) =>
                                          ArtworkDetailPage(illust: detail),
                                    ),
                                  );
                                  return;
                                }
                              } catch (_) {}
                            }
                            if (!mounted) return;
                          }
                          final sources = [
                            item.sourceUrl,
                            ...item.extUrls,
                          ].whereType<String>();
                          final uri = sources
                              .map(Uri.tryParse)
                              .whereType<Uri>()
                              .where(
                                (u) =>
                                    u.scheme == 'https' || u.scheme == 'http',
                              )
                              .firstOrNull;
                          if (uri != null) {
                            await launchUrl(
                              uri,
                              mode: LaunchMode.externalApplication,
                            );
                          }
                        },
                      ),
                    ),
                ],
              ],
            ),
          ),
        ),
      ),
    ).whenComplete(urlController.dispose);
  }

  void _showBooruDialog() {
    String platform = 'yandere';
    final tagCtrl = TextEditingController();
    BooruSearchResultDto? result;
    bool isSearching = false;
    String? error;
    int currentPage = 1;
    int requestGeneration = 0;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setModalState) {
          Future<void> fetchBooruPage(int page) async {
            final generation = ++requestGeneration;
            final requestedPlatform = platform;
            final requestedTags = tagCtrl.text.trim();
            setModalState(() {
              isSearching = true;
              error = null;
            });
            try {
              final res = await searchBooru(
                platformName: requestedPlatform,
                tags: requestedTags,
                page: page,
              );
              if (!ctx.mounted || generation != requestGeneration) return;
              setModalState(() {
                result = res;
                currentPage = page;
                isSearching = false;
              });
            } catch (e) {
              if (!ctx.mounted || generation != requestGeneration) return;
              setModalState(() {
                error = e.toString();
                isSearching = false;
              });
            }
          }

          return DraggableScrollableSheet(
            expand: false,
            initialChildSize: 0.75,
            maxChildSize: 0.95,
            builder: (_, scrollCtrl) => Padding(
              padding: const EdgeInsets.all(16.0),
              child: ListView(
                controller: scrollCtrl,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        '多 Booru 标签浏览',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      IconButton(
                        icon: const Icon(Icons.close),
                        onPressed: () => Navigator.pop(ctx),
                      ),
                    ],
                  ),
                  Row(
                    children: [
                      DropdownButton<String>(
                        value: platform,
                        items: [
                          DropdownMenuItem(
                            value: 'yandere',
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Image.asset(
                                  AppAssets.platformYandere,
                                  width: 20,
                                  height: 20,
                                ),
                                const SizedBox(width: 8),
                                const Text('Yande.re'),
                              ],
                            ),
                          ),
                          DropdownMenuItem(
                            value: 'danbooru',
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Image.asset(
                                  AppAssets.platformDanbooru,
                                  width: 20,
                                  height: 20,
                                ),
                                const SizedBox(width: 8),
                                const Text('Danbooru'),
                              ],
                            ),
                          ),
                          DropdownMenuItem(
                            value: 'gelbooru',
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Image.asset(
                                  AppAssets.platformGelbooru,
                                  width: 20,
                                  height: 20,
                                ),
                                const SizedBox(width: 8),
                                const Text('Gelbooru'),
                              ],
                            ),
                          ),
                          DropdownMenuItem(
                            value: 'rule34',
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Image.asset(
                                  AppAssets.platformRule34,
                                  width: 20,
                                  height: 20,
                                ),
                                const SizedBox(width: 8),
                                const Text('Rule34'),
                              ],
                            ),
                          ),
                          DropdownMenuItem(
                            value: 'sankaku',
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Image.asset(
                                  AppAssets.platformSankaku,
                                  width: 20,
                                  height: 20,
                                ),
                                const SizedBox(width: 8),
                                const Text('Sankaku'),
                              ],
                            ),
                          ),
                        ],
                        onChanged: (val) {
                          if (val != null) {
                            setModalState(() {
                              platform = val;
                              currentPage = 1;
                              requestGeneration++;
                              result = null;
                              error = null;
                              isSearching = false;
                            });
                          }
                        },
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: TextField(
                          controller: tagCtrl,
                          decoration: const InputDecoration(
                            hintText: '标签 (例如 hatsune_miku)',
                            isDense: true,
                            border: OutlineInputBorder(),
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      FilledButton(
                        onPressed: isSearching ? null : () => fetchBooruPage(1),
                        child: isSearching
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Text('搜索'),
                      ),
                    ],
                  ),
                  if (error != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      '搜索失败: $error',
                      style: const TextStyle(color: Colors.red, fontSize: 13),
                    ),
                  ],
                  if (result != null) ...[
                    const SizedBox(height: 16),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          '结果 (${result!.results.length})',
                          style: const TextStyle(fontWeight: FontWeight.bold),
                        ),
                        Row(
                          children: [
                            IconButton(
                              icon: const Icon(Icons.chevron_left),
                              onPressed: currentPage > 1 && !isSearching
                                  ? () => fetchBooruPage(currentPage - 1)
                                  : null,
                            ),
                            Text('第 $currentPage 页'),
                            IconButton(
                              icon: const Icon(Icons.chevron_right),
                              onPressed:
                                  !isSearching && result!.results.isNotEmpty
                                  ? () => fetchBooruPage(currentPage + 1)
                                  : null,
                            ),
                          ],
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    for (final item in result!.results)
                      Card(
                        margin: const EdgeInsets.symmetric(vertical: 4),
                        child: ListTile(
                          leading: ClipRRect(
                            borderRadius: BorderRadius.circular(4),
                            child: SizedBox(
                              width: 48,
                              height: 48,
                              child:
                                  item.previewUrl != null &&
                                      item.previewUrl!.isNotEmpty
                                  ? Image(
                                      image: RustImageProvider(
                                        item.previewUrl!,
                                      ),
                                      fit: BoxFit.cover,
                                      errorBuilder: (
                                        context,
                                        error,
                                        stackTrace,
                                      ) => const Icon(Icons.image),
                                    )
                                  : const Icon(Icons.image),
                            ),
                          ),
                          title: Text(
                            item.tagString,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          subtitle: Text(
                            'ID: ${item.id} • MD5: ${item.md5 ?? '—'}',
                          ),
                          onTap: () {
                            final viewUrl =
                                item.sampleUrl ??
                                item.originalUrl ??
                                item.previewUrl;
                            if (viewUrl != null && viewUrl.isNotEmpty) {
                              showDialog(
                                context: context,
                                builder: (dlgCtx) => Dialog(
                                  child: Column(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      AppBar(
                                        title: Text('Post ${item.id}'),
                                        leading: IconButton(
                                          icon: const Icon(Icons.close),
                                          onPressed: () =>
                                              Navigator.pop(dlgCtx),
                                        ),
                                      ),
                                      Flexible(
                                        child: Image(
                                          image: RustImageProvider(viewUrl),
                                          fit: BoxFit.contain,
                                          errorBuilder:
                                              (context, error, stackTrace) =>
                                                  const Padding(
                                                    padding: EdgeInsets.all(
                                                      32.0,
                                                    ),
                                                    child: Icon(
                                                      Icons.broken_image,
                                                      size: 64,
                                                    ),
                                                  ),
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              );
                            }
                          },
                        ),
                      ),
                  ],
                ],
              ),
            ),
          );
        },
      ),
    ).whenComplete(tagCtrl.dispose);
  }

  @override
  Widget build(BuildContext context) {
    final search = ref.watch(searchProvider);
    final notifier = ref.read(searchProvider.notifier);
    final history = ref.watch(searchHistoryProvider);
    final historyNotifier = ref.read(searchHistoryProvider.notifier);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        titleSpacing: 0,
        title: TextField(
          controller: _textController,
          autofocus: widget.initialQuery == null,
          textInputAction: TextInputAction.search,
          decoration: InputDecoration(
            hintText: '搜索插画、标签或使用 tag: 等语法...',
            border: InputBorder.none,
            suffixIcon: _textController.text.isNotEmpty
                ? IconButton(
                    icon: const Icon(Icons.clear, size: 20),
                    onPressed: () {
                      _textController.clear();
                      notifier.clearSuggestions();
                      setState(() {});
                    },
                  )
                : null,
          ),
          onChanged: (val) {
            notifier.updateAutocomplete(
              val,
              caretPosition: _textController.selection.extentOffset.clamp(
                0,
                val.length,
              ),
            );
            setState(() {});
          },
          onSubmitted: _triggerSearch,
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.image_search),
            tooltip: 'SauceNAO 搜图',
            onPressed: _showSauceNaoDialog,
          ),
          IconButton(
            icon: const Icon(Icons.filter_vintage),
            tooltip: 'Booru 浏览',
            onPressed: _showBooruDialog,
          ),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(84),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 16.0),
                alignment: Alignment.centerLeft,
                child: Row(
                  children: [
                    DropdownButton<String>(
                      value: search.sort,
                      underline: const SizedBox.shrink(),
                      icon: const Icon(Icons.arrow_drop_down, size: 20),
                      items: const [
                        DropdownMenuItem(
                          value: 'date_desc',
                          child: Text('最新优先'),
                        ),
                        DropdownMenuItem(
                          value: 'date_asc',
                          child: Text('最早优先'),
                        ),
                        DropdownMenuItem(
                          value: 'popular_desc',
                          child: Text('热门优先'),
                        ),
                      ],
                      onChanged: (val) {
                        if (val != null) notifier.setSort(val);
                      },
                    ),
                    const SizedBox(width: 16),
                    PopupMenuButton<String>(
                      icon: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            search.searchTarget == 'partial_match_for_tags'
                                ? '部分标签'
                                : (search.searchTarget == 'exact_match_for_tags'
                                      ? '精确标签'
                                      : '标题/描述'),
                            style: TextStyle(
                              color: theme.colorScheme.primary,
                              fontSize: 13,
                            ),
                          ),
                          const Icon(Icons.arrow_drop_down, size: 20),
                        ],
                      ),
                      onSelected: (val) => notifier.setSearchTarget(val),
                      itemBuilder: (_) => const [
                        PopupMenuItem(
                          value: 'partial_match_for_tags',
                          child: Text('标签部分一致'),
                        ),
                        PopupMenuItem(
                          value: 'exact_match_for_tags',
                          child: Text('标签完全一致'),
                        ),
                        PopupMenuItem(
                          value: 'title_and_caption',
                          child: Text('标题及正文匹配'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                child: Text(
                  '高级筛选作用于检索结果；无标签或标题条件时筛选推荐流。',
                  style: TextStyle(fontSize: 11),
                ),
              ),
            ],
          ),
        ),
      ),
      body: Stack(
        children: [
          // Main Results View
          if (search.items.isNotEmpty)
            ArtworkWaterfall(
              items: search.items,
              isLoading: search.isLoading,
              errorMessage: search.errorMessage,
              onRefresh: () => notifier.search(search.query, refresh: true),
              onLoadMore: () => notifier.loadMore(),
              onBookmarkToggle: (id, bookmarked) =>
                  notifier.toggleItemBookmark(id, bookmarked),
              onBookmarkChanged: notifier.updateItemBookmarkLocally,
            )
          else if (search.isLoading)
            const Center(child: CircularProgressIndicator())
          else if (search.errorMessage != null)
            Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.error_outline, size: 48, color: Colors.grey),
                  const SizedBox(height: 12),
                  Text(
                    search.errorMessage!,
                    style: const TextStyle(color: Colors.grey),
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: () =>
                        notifier.search(search.query, refresh: true),
                    child: const Text('重试'),
                  ),
                ],
              ),
            )
          else if (search.query.isNotEmpty)
            Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text('当前批次没有符合条件的作品'),
                  if (!search.exhausted)
                    TextButton(
                      onPressed: notifier.loadMore,
                      child: const Text('继续筛选下一页'),
                    ),
                ],
              ),
            )
          else
            // Search history & popular keywords prompt
            ListView(
              padding: const EdgeInsets.all(20.0),
              children: [
                if (history.isNotEmpty) ...[
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        '搜索历史',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      TextButton(
                        onPressed: () => historyNotifier.clear(),
                        child: const Text('清空'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: history.map((item) {
                      return InputChip(
                        label: Text(item.value),
                        onPressed: () => _triggerSearch(item.value),
                        onDeleted: () => historyNotifier.delete(item.value),
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 24),
                ],
                Text(
                  '热门探索',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: _popularTags.map((tag) {
                    return ActionChip(
                      label: Text(tag),
                      onPressed: () => _triggerSearch(tag),
                    );
                  }).toList(),
                ),
              ],
            ),

          // Autocomplete Overlay Dropdown with AST completions
          if (search.suggestions.isNotEmpty ||
              search.filterCompletions.isNotEmpty)
            Container(
              color: theme.colorScheme.surface,
              child: ListView(
                children: [
                  // Filter syntax chips (pixeval_filters AST)
                  if (search.filterCompletions.isNotEmpty)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 8,
                      ),
                      color: theme.colorScheme.surfaceContainerHighest,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            '高级语法联想 (pixeval_filters)',
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                              color: Colors.grey,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Wrap(
                            spacing: 6,
                            children: search.filterCompletions.map((fc) {
                              return ActionChip(
                                label: Text(
                                  fc.displayText,
                                  style: const TextStyle(fontSize: 12),
                                ),
                                onPressed: () {
                                  _textController.value = TextEditingValue(
                                    text: fc.completedText,
                                    selection: TextSelection.collapsed(
                                      offset: fc.completedText.length,
                                    ),
                                  );
                                  notifier.updateAutocomplete(fc.completedText);
                                },
                              );
                            }).toList(),
                          ),
                        ],
                      ),
                    ),
                  // Mako tag suggestions
                  for (final suggestion in search.suggestions)
                    ListTile(
                      leading: const Icon(Icons.search, size: 20),
                      title: Text(suggestion.name),
                      subtitle:
                          suggestion.translatedName != null &&
                              suggestion.translatedName!.isNotEmpty
                          ? Text(
                              suggestion.translatedName!,
                              style: TextStyle(
                                color: theme.colorScheme.primary,
                                fontSize: 12,
                              ),
                            )
                          : null,
                      onTap: () {
                        _triggerSearch(suggestion.name);
                      },
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
