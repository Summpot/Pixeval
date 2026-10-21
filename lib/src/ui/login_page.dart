import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../rust/api/auth.dart';
import '../state/auth_state.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  final _tokenController = TextEditingController();
  final _callbackUrlController = TextEditingController();
  final _formKeyToken = GlobalKey<FormState>();
  final _formKeyOAuth = GlobalKey<FormState>();

  PkceAuthParamsDto? _pkceParams;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    _tokenController.dispose();
    _callbackUrlController.dispose();
    super.dispose();
  }

  Future<void> _generatePkce() async {
    try {
      final params = await ref
          .read(authProvider.notifier)
          .createOAuthAuthorization();
      if (!mounted) return;
      setState(() {
        _pkceParams = params;
      });
      await Clipboard.setData(ClipboardData(text: params.authUrl));
      final opened = await launchUrl(
        Uri.parse(params.authUrl),
        mode: LaunchMode.externalApplication,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(opened ? '已打开浏览器授权，完成后返回应用。' : '授权链接已复制，请在浏览器打开。'),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('生成授权链接失败: $e')));
      }
    }
  }

  Future<void> _handleOAuthLogin() async {
    if (_pkceParams == null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('请先点击“生成授权链接”并完成浏览器授权')));
      return;
    }
    final rawInput = _callbackUrlController.text.trim();
    if (rawInput.isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('请输入回调链接或授权码')));
      return;
    }

    String code = rawInput;
    if (rawInput.contains('code=')) {
      final uri = Uri.tryParse(rawInput);
      if (uri != null && uri.queryParameters.containsKey('code')) {
        code = uri.queryParameters['code']!;
      } else {
        final match = RegExp(r'code=([^&]+)').firstMatch(rawInput);
        if (match != null) {
          code = match.group(1)!;
        }
      }
    }

    final success = await ref
        .read(authProvider.notifier)
        .loginWithCode(code, _pkceParams!.codeVerifier);
    if (success && mounted) {
      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('登录成功！已自动保存并轮转 Refresh Token。')),
      );
    }
  }

  Future<void> _handleTokenLogin() async {
    if (!_formKeyToken.currentState!.validate()) return;
    final token = _tokenController.text.trim();
    final success = await ref
        .read(authProvider.notifier)
        .loginWithRefreshToken(token);
    if (success && mounted) {
      Navigator.of(context).pop();
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('登录成功！')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Pixiv 账号登录'),
        bottom: TabBar(
          controller: _tabController,
          tabs: const [
            Tab(icon: Icon(Icons.open_in_browser), text: 'OAuth 浏览器登录'),
            Tab(icon: Icon(Icons.vpn_key), text: 'Refresh Token 登录'),
          ],
        ),
      ),
      body: SafeArea(
        child: TabBarView(
          controller: _tabController,
          children: [
            // Tab 1: Browser OAuth with PKCE
            SingleChildScrollView(
              padding: const EdgeInsets.all(24.0),
              child: Form(
                key: _formKeyOAuth,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Icon(
                      Icons.shield_outlined,
                      size: 64,
                      color: Color(0xFF0096FA),
                    ),
                    const SizedBox(height: 16),
                    Text(
                      '标准 Pixiv OAuth 2.0 PKCE 登录',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.titleLarge
                          ?.copyWith(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '1. 点击“生成授权链接”，链接将复制到剪贴板并在浏览器打开登录。\n2. 登录完成后浏览器会重定向到 callback 页面或报错页，复制该完整地址并粘贴在下方。',
                      style: TextStyle(
                        fontSize: 13,
                        color: Colors.grey[600],
                        height: 1.5,
                      ),
                    ),
                    const SizedBox(height: 20),
                    OutlinedButton.icon(
                      onPressed: authState.isLoading ? null : _generatePkce,
                      icon: const Icon(Icons.link),
                      label: Text(
                        _pkceParams == null ? '生成授权链接 (复制到剪贴板)' : '重新生成授权链接',
                      ),
                    ),
                    if (_pkceParams != null) ...[
                      const SizedBox(height: 12),
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: Colors.green.withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          '授权链接已就绪。Verifier: ${_pkceParams!.codeVerifier.substring(0, 8)}...',
                          style: const TextStyle(
                            fontSize: 12,
                            color: Colors.green,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: 20),
                    TextFormField(
                      controller: _callbackUrlController,
                      decoration: const InputDecoration(
                        labelText: '回调 URL 或 Code',
                        hintText: '粘贴包含 code=... 的完整重定向 URL 或单独授权码',
                        prefixIcon: Icon(Icons.input),
                        border: OutlineInputBorder(),
                      ),
                    ),
                    if (authState.errorMessage != null) ...[
                      const SizedBox(height: 12),
                      Text(
                        authState.errorMessage!,
                        style: const TextStyle(color: Colors.red, fontSize: 13),
                      ),
                    ],
                    const SizedBox(height: 24),
                    FilledButton.icon(
                      onPressed: authState.isLoading ? null : _handleOAuthLogin,
                      icon: authState.isLoading
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Icon(Icons.login),
                      label: Text(
                        authState.isLoading ? '正在验证并换取 Token...' : '完成授权并登录',
                      ),
                    ),
                  ],
                ),
              ),
            ),

            // Tab 2: Refresh Token
            SingleChildScrollView(
              padding: const EdgeInsets.all(24.0),
              child: Form(
                key: _formKeyToken,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Icon(
                      Icons.account_circle_outlined,
                      size: 64,
                      color: Color(0xFF0096FA),
                    ),
                    const SizedBox(height: 16),
                    Text(
                      '使用已有 Refresh Token 登录',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.titleLarge
                          ?.copyWith(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '如果您已有现成的 Pixiv Refresh Token，可直接粘贴登录。登录成功后新 Token 会自动更新与持久化。',
                      textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 13, color: Colors.grey[600]),
                    ),
                    const SizedBox(height: 24),
                    TextFormField(
                      controller: _tokenController,
                      decoration: const InputDecoration(
                        labelText: 'OAuth Refresh Token',
                        hintText: '在此粘贴 refresh_token',
                        prefixIcon: Icon(Icons.key_outlined),
                        border: OutlineInputBorder(),
                      ),
                      validator: (value) {
                        if (value == null || value.trim().isEmpty) {
                          return '请输入有效的 Refresh Token';
                        }
                        return null;
                      },
                    ),
                    if (authState.errorMessage != null) ...[
                      const SizedBox(height: 12),
                      Text(
                        authState.errorMessage!,
                        style: const TextStyle(color: Colors.red, fontSize: 13),
                      ),
                    ],
                    const SizedBox(height: 24),
                    FilledButton.icon(
                      onPressed: authState.isLoading ? null : _handleTokenLogin,
                      icon: authState.isLoading
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Icon(Icons.login),
                      label: Text(authState.isLoading ? '正在登录...' : '登录'),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
