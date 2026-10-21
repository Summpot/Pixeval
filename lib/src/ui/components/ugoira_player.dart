import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:path_provider/path_provider.dart';
import '../../rust/api/image.dart';
import '../../rust/api/ugoira.dart';

class UgoiraPlayerWidget extends StatefulWidget {
  final int illustId;
  final String previewUrl;

  const UgoiraPlayerWidget({
    super.key,
    required this.illustId,
    required this.previewUrl,
  });

  @override
  State<UgoiraPlayerWidget> createState() => _UgoiraPlayerWidgetState();
}

class _UgoiraPlayerWidgetState extends State<UgoiraPlayerWidget> {
  bool _isLoading = true;
  String? _errorMessage;
  List<Uint8List> _frames = [];
  List<int> _delays = [];
  int _currentFrame = 0;
  bool _isPlaying = true;
  double _speed = 1.0;
  Timer? _timer;
  UgoiraMetadataDto? _metadata;
  Uint8List? _zipBytes;

  @override
  void initState() {
    super.initState();
    _loadUgoira();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _loadUgoira() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final meta = await getUgoiraMetadata(illustId: widget.illustId);
      _metadata = meta;
      final delays = meta.frames.map((f) => f.delay).toList();

      final bytes = await fetchImage(
        url: meta.zipUrl,
        referer: 'https://app-api.pixiv.net/',
      );
      _zipBytes = bytes;

      final extracted = await extractUgoiraFramesFromZipBytes(
        zipBytes: bytes,
        delays: delays,
      );

      if (mounted) {
        setState(() {
          _frames = extracted.frameBytes.map((b) => Uint8List.fromList(b)).toList();
          _delays = extracted.delays;
          _isLoading = false;
        });
        _scheduleNextFrame();
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = e.toString();
        });
      }
    }
  }

  void _scheduleNextFrame() {
    _timer?.cancel();
    if (!_isPlaying || _frames.isEmpty) return;

    final delayMs = (_delays.isNotEmpty && _currentFrame < _delays.length)
        ? (_delays[_currentFrame] / _speed).round().clamp(10, 1000)
        : (100 / _speed).round();

    _timer = Timer(Duration(milliseconds: delayMs), () {
      if (!mounted || !_isPlaying) return;
      setState(() {
        _currentFrame = (_currentFrame + 1) % _frames.length;
      });
      _scheduleNextFrame();
    });
  }

  void _togglePlay() {
    setState(() {
      _isPlaying = !_isPlaying;
    });
    if (_isPlaying) {
      _scheduleNextFrame();
    } else {
      _timer?.cancel();
    }
  }

  void _stepFrame(int delta) {
    if (_frames.isEmpty) return;
    setState(() {
      _isPlaying = false;
      _timer?.cancel();
      _currentFrame = (_currentFrame + delta + _frames.length) % _frames.length;
    });
  }

  Future<void> _export(String format) async {
    if (_zipBytes == null || _metadata == null) return;
    final messenger = ScaffoldMessenger.of(context);
    messenger.showSnackBar(
      SnackBar(content: Text('正在合成并导出为 ${format.toUpperCase()}...')),
    );

    try {
      final tempDir = await getTemporaryDirectory();
      final zipFile = File('${tempDir.path}/ugoira_${widget.illustId}.zip');
      await zipFile.writeAsBytes(_zipBytes!);

      final outFile = '${tempDir.path}/ugoira_${widget.illustId}.${format.toLowerCase()}';
      final delays = _metadata!.frames.map((f) => f.delay).toList();

      await exportUgoira(
        zipPath: zipFile.path,
        outputPath: outFile,
        formatName: format,
        frameDelaysMs: delays,
      );

      messenger.showSnackBar(
        SnackBar(
          content: Text('导出成功！已保存至: $outFile'),
          backgroundColor: Colors.green[800],
        ),
      );
    } catch (e) {
      messenger.showSnackBar(
        SnackBar(
          content: Text('导出失败: $e'),
          backgroundColor: Colors.red[800],
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return Container(
        color: Colors.black,
        child: const Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(color: Colors.white),
              SizedBox(height: 12),
              Text(
                '正在下载并解压动图数据...',
                style: TextStyle(color: Colors.white70, fontSize: 13),
              ),
            ],
          ),
        ),
      );
    }

    if (_errorMessage != null) {
      return Container(
        color: Colors.black,
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, color: Colors.red, size: 40),
              const SizedBox(height: 8),
              Text('加载动图失败: $_errorMessage', style: const TextStyle(color: Colors.white70)),
              const SizedBox(height: 8),
              FilledButton.tonal(
                onPressed: _loadUgoira,
                child: const Text('重试'),
              ),
            ],
          ),
        ),
      );
    }

    if (_frames.isEmpty) {
      return const SizedBox.shrink();
    }

    return Container(
      color: Colors.black,
      child: Column(
        children: [
          Expanded(
            child: Center(
              child: Image.memory(
                _frames[_currentFrame],
                fit: BoxFit.contain,
                gaplessPlayback: true,
              ),
            ),
          ),
          // Player Controls
          Container(
            color: Colors.black87,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // Scrubber Slider
                Row(
                  children: [
                    Text(
                      '${_currentFrame + 1} / ${_frames.length}',
                      style: const TextStyle(color: Colors.white70, fontSize: 12),
                    ),
                    Expanded(
                      child: Slider(
                        value: _currentFrame.toDouble(),
                        min: 0,
                        max: (_frames.length - 1).toDouble(),
                        onChanged: (val) {
                          setState(() {
                            _isPlaying = false;
                            _timer?.cancel();
                            _currentFrame = val.toInt();
                          });
                        },
                      ),
                    ),
                  ],
                ),
                // Playback Buttons & Speed & Export
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                  children: [
                    IconButton(
                      icon: const Icon(Icons.skip_previous, color: Colors.white),
                      onPressed: () => _stepFrame(-1),
                      tooltip: '上一帧',
                    ),
                    IconButton(
                      icon: Icon(
                        _isPlaying ? Icons.pause_circle : Icons.play_circle,
                        color: Colors.white,
                        size: 32,
                      ),
                      onPressed: _togglePlay,
                    ),
                    IconButton(
                      icon: const Icon(Icons.skip_next, color: Colors.white),
                      onPressed: () => _stepFrame(1),
                      tooltip: '下一帧',
                    ),
                    // Speed Dropdown
                    DropdownButton<double>(
                      value: _speed,
                      dropdownColor: Colors.grey[900],
                      style: const TextStyle(color: Colors.white, fontSize: 13),
                      underline: const SizedBox.shrink(),
                      items: const [
                        DropdownMenuItem(value: 0.5, child: Text('0.5x')),
                        DropdownMenuItem(value: 1.0, child: Text('1.0x')),
                        DropdownMenuItem(value: 1.5, child: Text('1.5x')),
                        DropdownMenuItem(value: 2.0, child: Text('2.0x')),
                      ],
                      onChanged: (val) {
                        if (val != null) {
                          setState(() => _speed = val);
                          if (_isPlaying) _scheduleNextFrame();
                        }
                      },
                    ),
                    // Export Menu
                    PopupMenuButton<String>(
                      icon: const Icon(Icons.file_download, color: Colors.white),
                      tooltip: '导出动图',
                      onSelected: _export,
                      itemBuilder: (_) => [
                        const PopupMenuItem(value: 'gif', child: Text('导出为 GIF')),
                        const PopupMenuItem(value: 'webp', child: Text('导出为 WebP')),
                        if (!Platform.isAndroid && !Platform.isIOS)
                          const PopupMenuItem(value: 'mp4', child: Text('导出为 MP4 视频 (桌面)')),
                      ],
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
