import '../pixeval.g.dart';
export '../pixeval.g.dart' show FlutterNetworkConfig, ConnectivityCheckResult;

import '../native_api.dart';

Future<FlutterNetworkConfig> getNetworkConfig() =>
    NativeApi.instance.api.getNetworkConfig();

Future<void> updateNetworkConfig({required FlutterNetworkConfig config}) =>
    NativeApi.instance.api.updateNetworkConfig(config: config);

Future<ConnectivityCheckResult> testConnectivity() =>
    NativeApi.instance.api.testConnectivity();
