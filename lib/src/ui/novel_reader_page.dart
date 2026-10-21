import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../rust/api/novel.dart';
import '../state/history_state.dart';

enum NovelReaderTheme {
  light(Color(0xFFFAF9F6), Color(0xFF222222), 'Light'),
  sepia(Color(0xFFF4ECD8), Color(0xFF4A3525), 'Sepia'),
  dark(Color(0xFF1E1E1E), Color(0xFFD6D6D6), 'Dark'),
  black(Color(0xFF000000), Color(0xFFCCCCCC), 'Black');

  final Color background;
  final Color text;
  final String label;

  const NovelReaderTheme(this.background, this.text, this.label);
}

enum NovelFontFamily {
  system('System', null),
  serif('Serif', 'serif'),
  sansSerif('Sans', 'sans-serif'),
  monospace('Mono', 'monospace');

  final String label;
  final String? family;

  const NovelFontFamily(this.label, this.family);
}

class NovelReaderPage extends ConsumerStatefulWidget {
  final int novelId;
  final String? initialTitle;

  const NovelReaderPage({
    super.key,
    required this.novelId,
    this.initialTitle,
  });

  @override
  ConsumerState<NovelReaderPage> createState() => _NovelReaderPageState();
}

class _NovelReaderPageState extends ConsumerState<NovelReaderPage> {
  late PageController _pageController;
  int _currentPage = 0;
  bool _showControls = true;
  double _fontSize = 17.0;
  double _lineHeight = 1.85;
  NovelFontFamily _fontFamily = NovelFontFamily.system;
  NovelReaderTheme _theme = NovelReaderTheme.sepia;

  NovelDetailDto? _detail;
  NovelContentDto? _content;
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _pageController = PageController();
    _loadNovel();
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  Future<void> _loadNovel() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final detail = await getNovelDetail(novelId: widget.novelId);
      final content = await getNovelContentRendered(novelId: widget.novelId);
      if (mounted) {
        setState(() {
          _detail = detail;
          _content = content;
          _isLoading = false;
        });
        ref.read(browseHistoryProvider.notifier).recordNovel(
              novelId: widget.novelId,
              title: detail.title,
              coverUrl: detail.coverUrl,
              authorName: detail.userName,
            );
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = e.toString();
          _isLoading = false;
        });
      }
    }
  }

  void _toggleControls() {
    setState(() {
      _showControls = !_showControls;
    });
  }

  void _showChaptersSheet() {
    final chapters = _content?.chapters ?? [];
    final pagesCount = _content?.pages.length ?? 0;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: _theme.background,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) {
        return DraggableScrollableSheet(
          expand: false,
          initialChildSize: 0.5,
          minChildSize: 0.3,
          maxChildSize: 0.85,
          builder: (context, scrollController) {
            return Column(
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                  decoration: BoxDecoration(
                    border: Border(bottom: BorderSide(color: _theme.text.withValues(alpha: 0.12))),
                  ),
                  child: Row(
                    children: [
                      Icon(Icons.format_list_bulleted, color: _theme.text),
                      const SizedBox(width: 10),
                      Text(
                        'Chapters & Sections',
                        style: TextStyle(
                          color: _theme.text,
                          fontSize: 17,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const Spacer(),
                      Text(
                        chapters.isNotEmpty
                            ? '${chapters.length} chapter(s)'
                            : '$pagesCount page(s)',
                        style: TextStyle(
                          color: _theme.text.withValues(alpha: 0.6),
                          fontSize: 13,
                        ),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: chapters.isNotEmpty
                      ? ListView.separated(
                          controller: scrollController,
                          itemCount: chapters.length,
                          separatorBuilder: (_, _) => Divider(
                            height: 1,
                            color: _theme.text.withValues(alpha: 0.08),
                          ),
                          itemBuilder: (context, idx) {
                            final ch = chapters[idx];
                            final isCurrent = ch.pageIndex == _currentPage;
                            return ListTile(
                              leading: Text(
                                '${idx + 1}',
                                style: TextStyle(
                                  color: isCurrent
                                      ? Theme.of(context).colorScheme.primary
                                      : _theme.text.withValues(alpha: 0.5),
                                  fontWeight: isCurrent ? FontWeight.bold : FontWeight.normal,
                                ),
                              ),
                              title: Text(
                                ch.title.isEmpty ? 'Chapter ${idx + 1}' : ch.title,
                                style: TextStyle(
                                  color: isCurrent
                                      ? Theme.of(context).colorScheme.primary
                                      : _theme.text,
                                  fontWeight: isCurrent ? FontWeight.bold : FontWeight.normal,
                                ),
                              ),
                              trailing: Text(
                                'Page ${ch.pageIndex + 1}',
                                style: TextStyle(
                                  color: _theme.text.withValues(alpha: 0.4),
                                  fontSize: 12,
                                ),
                              ),
                              onTap: () {
                                Navigator.pop(ctx);
                                _pageController.jumpToPage(ch.pageIndex);
                              },
                            );
                          },
                        )
                      : ListView.separated(
                          controller: scrollController,
                          itemCount: pagesCount,
                          separatorBuilder: (_, _) => Divider(
                            height: 1,
                            color: _theme.text.withValues(alpha: 0.08),
                          ),
                          itemBuilder: (context, idx) {
                            final isCurrent = idx == _currentPage;
                            return ListTile(
                              title: Text(
                                'Page ${idx + 1}',
                                style: TextStyle(
                                  color: isCurrent
                                      ? Theme.of(context).colorScheme.primary
                                      : _theme.text,
                                  fontWeight: isCurrent ? FontWeight.bold : FontWeight.normal,
                                ),
                              ),
                              onTap: () {
                                Navigator.pop(ctx);
                                _pageController.jumpToPage(idx);
                              },
                            );
                          },
                        ),
                ),
              ],
            );
          },
        );
      },
    );
  }

  void _showTypographySheet() {
    showModalBottomSheet(
      context: context,
      backgroundColor: _theme.background,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            return Padding(
              padding: EdgeInsets.only(
                left: 20,
                right: 20,
                top: 18,
                bottom: MediaQuery.of(context).padding.bottom + 16,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Typography Settings',
                    style: TextStyle(
                      color: _theme.text,
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Font Family
                  Text(
                    'Font Family',
                    style: TextStyle(color: _theme.text.withValues(alpha: 0.7), fontSize: 13),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: NovelFontFamily.values.map((f) {
                      final selected = _fontFamily == f;
                      return ChoiceChip(
                        label: Text(f.label),
                        selected: selected,
                        onSelected: (val) {
                          if (val) {
                            setState(() => _fontFamily = f);
                            setSheetState(() {});
                          }
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 16),

                  // Line Height
                  Text(
                    'Line Spacing',
                    style: TextStyle(color: _theme.text.withValues(alpha: 0.7), fontSize: 13),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: [1.4, 1.7, 1.85, 2.2].map((lh) {
                      final selected = (_lineHeight - lh).abs() < 0.05;
                      return ChoiceChip(
                        label: Text('${lh}x'),
                        selected: selected,
                        onSelected: (val) {
                          if (val) {
                            setState(() => _lineHeight = lh);
                            setSheetState(() {});
                          }
                        },
                      );
                    }).toList(),
                  ),
                ],
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final title = _detail?.title ?? widget.initialTitle ?? 'Pixiv Novel';
    final pages = _content?.pages ?? [];

    return Scaffold(
      backgroundColor: _theme.background,
      body: Stack(
        children: [
          // Novel Reader PageView
          if (_isLoading)
            const Center(child: CircularProgressIndicator())
          else if (_errorMessage != null)
            Center(
              child: Padding(
                padding: const EdgeInsets.all(24.0),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline, size: 48, color: Colors.grey),
                    const SizedBox(height: 12),
                    Text(
                      'Failed to load novel',
                      style: TextStyle(color: _theme.text, fontSize: 16, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      _errorMessage!,
                      textAlign: TextAlign.center,
                      style: TextStyle(color: _theme.text.withValues(alpha: 0.7)),
                    ),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: _loadNovel,
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
            )
          else if (pages.isEmpty)
            Center(
              child: Text(
                'No content available for this novel.',
                style: TextStyle(color: _theme.text),
              ),
            )
          else
            GestureDetector(
              onTap: _toggleControls,
              child: PageView.builder(
                controller: _pageController,
                itemCount: pages.length,
                onPageChanged: (idx) {
                  setState(() {
                    _currentPage = idx;
                  });
                },
                itemBuilder: (context, index) {
                  return SafeArea(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 20.0),
                      child: SingleChildScrollView(
                        physics: const BouncingScrollPhysics(),
                        child: Text(
                          pages[index],
                          style: TextStyle(
                            fontSize: _fontSize,
                            height: _lineHeight,
                            color: _theme.text,
                            letterSpacing: 0.5,
                            fontFamily: _fontFamily.family,
                          ),
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),

          // Top Navigation Controls
          AnimatedPositioned(
            duration: const Duration(milliseconds: 200),
            top: _showControls ? 0 : -100,
            left: 0,
            right: 0,
            child: Container(
              padding: EdgeInsets.only(
                top: MediaQuery.of(context).padding.top + 8,
                left: 8,
                right: 8,
                bottom: 8,
              ),
              decoration: BoxDecoration(
                color: _theme.background.withValues(alpha: 0.95),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.08),
                    blurRadius: 4,
                  ),
                ],
              ),
              child: Row(
                children: [
                  IconButton(
                    icon: Icon(Icons.arrow_back, color: _theme.text),
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            color: _theme.text,
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        if (_detail != null)
                          Text(
                            _detail!.userName,
                            style: TextStyle(
                              color: _theme.text.withValues(alpha: 0.6),
                              fontSize: 12,
                            ),
                          ),
                      ],
                    ),
                  ),
                  IconButton(
                    icon: Icon(Icons.format_list_bulleted, color: _theme.text),
                    tooltip: 'Chapters',
                    onPressed: _showChaptersSheet,
                  ),
                  IconButton(
                    icon: Icon(Icons.text_fields, color: _theme.text),
                    tooltip: 'Typography',
                    onPressed: _showTypographySheet,
                  ),
                ],
              ),
            ),
          ),

          // Bottom Reader Settings Controls
          AnimatedPositioned(
            duration: const Duration(milliseconds: 200),
            bottom: _showControls ? 0 : -160,
            left: 0,
            right: 0,
            child: Container(
              padding: EdgeInsets.only(
                left: 20,
                right: 20,
                top: 14,
                bottom: MediaQuery.of(context).padding.bottom + 12,
              ),
              decoration: BoxDecoration(
                color: _theme.background.withValues(alpha: 0.95),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.1),
                    blurRadius: 8,
                  ),
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  // Page slider & indicator
                  if (pages.isNotEmpty)
                    Row(
                      children: [
                        Text(
                          '${_currentPage + 1}',
                          style: TextStyle(color: _theme.text, fontSize: 13, fontWeight: FontWeight.bold),
                        ),
                        Expanded(
                          child: Slider(
                            value: _currentPage.toDouble(),
                            min: 0,
                            max: (pages.length - 1).toDouble().clamp(0.0, double.infinity),
                            divisions: pages.length > 1 ? pages.length - 1 : 1,
                            onChanged: (val) {
                              final target = val.round();
                              _pageController.jumpToPage(target);
                            },
                          ),
                        ),
                        Text(
                          '${pages.length}',
                          style: TextStyle(color: _theme.text, fontSize: 13, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                  const SizedBox(height: 6),
                  // Font Size & Reading Theme Selector
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      // Font size controls
                      Row(
                        children: [
                          IconButton(
                            icon: Icon(Icons.text_decrease, color: _theme.text),
                            onPressed: _fontSize > 13
                                ? () => setState(() => _fontSize -= 1.5)
                                : null,
                          ),
                          Text(
                            '${_fontSize.round()}',
                            style: TextStyle(color: _theme.text, fontSize: 14, fontWeight: FontWeight.bold),
                          ),
                          IconButton(
                            icon: Icon(Icons.text_increase, color: _theme.text),
                            onPressed: _fontSize < 28
                                ? () => setState(() => _fontSize += 1.5)
                                : null,
                          ),
                        ],
                      ),
                      // Theme Pills
                      Row(
                        children: NovelReaderTheme.values.map((t) {
                          final isSelected = _theme == t;
                          return Padding(
                            padding: const EdgeInsets.only(left: 6.0),
                            child: ChoiceChip(
                              label: Text(t.label),
                              selected: isSelected,
                              onSelected: (val) {
                                if (val) setState(() => _theme = t);
                              },
                            ),
                          );
                        }).toList(),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
