import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../state/network_state.dart';

class NetworkSettingsPage extends ConsumerStatefulWidget {
  const NetworkSettingsPage({super.key});

  @override
  ConsumerState<NetworkSettingsPage> createState() =>
      _NetworkSettingsPageState();
}

class _NetworkSettingsPageState extends ConsumerState<NetworkSettingsPage> {
  final _hostIpsController = TextEditingController();

  @override
  void dispose() {
    _hostIpsController.dispose();
    super.dispose();
  }

  void _showDelayDialog(BuildContext context, int currentDelay) {
    int selected = currentDelay;
    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('设置 SNI 分片延迟 (ms)'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                '$selected ms',
                style: const TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                ),
              ),
              Slider(
                value: selected.toDouble().clamp(0.0, 500.0),
                min: 0,
                max: 500,
                divisions: 10,
                label: '$selected ms',
                onChanged: (val) {
                  setDialogState(() => selected = val.toInt());
                },
              ),
              const Text(
                '延迟用于在 TCP ClientHello 分片发送之间引入时间差，防止 GFW 重组探测。推荐 50–150ms。',
                style: TextStyle(fontSize: 12, color: Colors.grey),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('取消'),
            ),
            FilledButton(
              onPressed: () {
                ref.read(networkProvider.notifier).updateSplitDelay(selected);
                Navigator.pop(ctx);
              },
              child: const Text('保存'),
            ),
          ],
        ),
      ),
    );
  }

  void _showHostIpsDialog(BuildContext context, String currentJson) {
    _hostIpsController.text = currentJson.isEmpty
        ? '{\n  "app-api.pixiv.net": ["210.140.92.140"],\n  "i.pximg.net": ["210.140.92.142"]\n}'
        : currentJson;
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('自定义 DNS / Host IP 映射'),
        content: SizedBox(
          width: double.maxFinite,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                '配置域名到静态直连 IP 列表的 JSON 映射：',
                style: TextStyle(fontSize: 12, color: Colors.grey),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: _hostIpsController,
                maxLines: 6,
                style: const TextStyle(fontFamily: 'monospace', fontSize: 12),
                decoration: const InputDecoration(border: OutlineInputBorder()),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('取消'),
          ),
          FilledButton(
            onPressed: () {
              ref
                  .read(networkProvider.notifier)
                  .updateHostIps(_hostIpsController.text.trim());
              Navigator.pop(ctx);
            },
            child: const Text('保存'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final netState = ref.watch(networkProvider);
    final cfg = netState.config;
    final delay = (cfg?.splitDelayMs ?? BigInt.from(100)).toInt();

    return Scaffold(
      appBar: AppBar(title: const Text('网络与抗封锁配置')),
      body: ListView(
        padding: const EdgeInsets.all(16.0),
        children: [
          Card(
            elevation: 1,
            child: Padding(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Icon(Icons.security, color: Color(0xFF0096FA)),
                      const SizedBox(width: 8),
                      Text(
                        'Pixeval Maho 核心引擎',
                        style: Theme.of(context).textTheme.titleMedium
                            ?.copyWith(fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    '原生内置 TCP ClientHello 分片拆包与域前置技术，无需第三方代理直接绕过 Pixiv API 及图片 CDN 阻断。',
                    style: TextStyle(fontSize: 13, color: Colors.grey[600]),
                  ),
                  const Divider(height: 24),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('域前置与 SNI 伪装绕过'),
                    subtitle: const Text(
                      '拦截并分片 app-api.pixiv.net 及 i.pximg.net 的 TLS 握手',
                    ),
                    value: cfg?.domainFrontingEnabled ?? true,
                    onChanged: (val) {
                      ref
                          .read(networkProvider.notifier)
                          .toggleDomainFronting(val);
                    },
                  ),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('ClientHello 分片间隔 (点击调整)'),
                    subtitle: Text('$delay ms'),
                    trailing: const Icon(Icons.edit_outlined),
                    onTap: () => _showDelayDialog(context, delay),
                  ),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('自定义 DNS / Host 静态 IP 映射'),
                    subtitle: Text(
                      cfg?.hostIpsJson.isNotEmpty == true &&
                              cfg?.hostIpsJson != '{}'
                          ? '已配置静态 IP 映射'
                          : '未配置（点击添加）',
                    ),
                    trailing: const Icon(Icons.dns_outlined),
                    onTap: () =>
                        _showHostIpsDialog(context, cfg?.hostIpsJson ?? '{}'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Card(
            elevation: 1,
            child: Padding(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      const Icon(Icons.network_check, color: Colors.green),
                      const SizedBox(width: 8),
                      Text(
                        '连通性与延迟诊断',
                        style: Theme.of(context).textTheme.titleMedium
                            ?.copyWith(fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  if (netState.lastTestResult != null) ...[
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: netState.lastTestResult!.success
                            ? Colors.green.withValues(alpha: 0.1)
                            : Colors.red.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Icon(
                                netState.lastTestResult!.success
                                    ? Icons.check_circle
                                    : Icons.cancel,
                                color: netState.lastTestResult!.success
                                    ? Colors.green
                                    : Colors.red,
                                size: 20,
                              ),
                              const SizedBox(width: 8),
                              Text(
                                netState.lastTestResult!.success
                                    ? '连接成功 (${netState.lastTestResult!.latencyMs} ms)'
                                    : '连接失败',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  color: netState.lastTestResult!.success
                                      ? Colors.green[800]
                                      : Colors.red[800],
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 4),
                          Text(
                            netState.lastTestResult!.message,
                            style: const TextStyle(fontSize: 12),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 16),
                  ],
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.tonalIcon(
                      onPressed: netState.isTesting
                          ? null
                          : () => ref
                                .read(networkProvider.notifier)
                                .testConnection(),
                      icon: netState.isTesting
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.refresh),
                      label: Text(
                        netState.isTesting ? '正在诊断直连中...' : '测试 Pixiv API 连通性',
                      ),
                    ),
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
