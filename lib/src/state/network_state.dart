import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../rust/api/network.dart';

class NetworkState {
  final FlutterNetworkConfig? config;
  final bool isTesting;
  final ConnectivityCheckResult? lastTestResult;
  final String? errorMessage;

  const NetworkState({
    this.config,
    this.isTesting = false,
    this.lastTestResult,
    this.errorMessage,
  });

  NetworkState copyWith({
    FlutterNetworkConfig? config,
    bool? isTesting,
    ConnectivityCheckResult? lastTestResult,
    String? errorMessage,
  }) {
    return NetworkState(
      config: config ?? this.config,
      isTesting: isTesting ?? this.isTesting,
      lastTestResult: lastTestResult ?? this.lastTestResult,
      errorMessage: errorMessage,
    );
  }
}

class NetworkNotifier extends Notifier<NetworkState> {
  @override
  NetworkState build() {
    Future.microtask(() => loadConfig());
    return const NetworkState();
  }

  Future<void> loadConfig() async {
    try {
      final cfg = await getNetworkConfig();
      state = state.copyWith(config: cfg);
    } catch (e) {
      state = state.copyWith(errorMessage: e.toString());
    }
  }

  Future<void> toggleDomainFronting(bool enabled) async {
    final current = state.config;
    if (current == null) return;
    final updated = FlutterNetworkConfig(
      domainFrontingEnabled: enabled,
      splitDelayMs: current.splitDelayMs,
      proxyUrl: current.proxyUrl,
      mirrorHost: current.mirrorHost,
      dohUrl: current.dohUrl,
      hostIpsJson: current.hostIpsJson,
    );
    try {
      await updateNetworkConfig(config: updated);
      state = state.copyWith(config: updated);
    } catch (e) {
      state = state.copyWith(errorMessage: e.toString());
    }
  }

  Future<void> updateSplitDelay(int delayMs) async {
    final current = state.config;
    if (current == null) return;
    final updated = FlutterNetworkConfig(
      domainFrontingEnabled: current.domainFrontingEnabled,
      splitDelayMs: BigInt.from(delayMs),
      proxyUrl: current.proxyUrl,
      mirrorHost: current.mirrorHost,
      dohUrl: current.dohUrl,
      hostIpsJson: current.hostIpsJson,
    );
    try {
      await updateNetworkConfig(config: updated);
      state = state.copyWith(config: updated);
    } catch (e) {
      state = state.copyWith(errorMessage: e.toString());
    }
  }

  Future<void> updateHostIps(String hostIpsJson) async {
    final current = state.config;
    if (current == null) return;
    final updated = FlutterNetworkConfig(
      domainFrontingEnabled: current.domainFrontingEnabled,
      splitDelayMs: current.splitDelayMs,
      proxyUrl: current.proxyUrl,
      mirrorHost: current.mirrorHost,
      dohUrl: current.dohUrl,
      hostIpsJson: hostIpsJson,
    );
    try {
      await updateNetworkConfig(config: updated);
      state = state.copyWith(config: updated);
    } catch (e) {
      state = state.copyWith(errorMessage: e.toString());
    }
  }

  Future<void> testConnection() async {
    state = state.copyWith(isTesting: true, errorMessage: null);
    try {
      final res = await testConnectivity();
      state = state.copyWith(isTesting: false, lastTestResult: res);
    } catch (e) {
      state = state.copyWith(isTesting: false, errorMessage: e.toString());
    }
  }
}

final networkProvider = NotifierProvider<NetworkNotifier, NetworkState>(NetworkNotifier.new);
