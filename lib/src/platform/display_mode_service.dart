import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

/// Service to query physical display modes and enforce peak physical refresh rates (90Hz / 120Hz / 144Hz)
/// on Android devices.
class DisplayModeService {
  DisplayModeService._();

  static const MethodChannel _channel = MethodChannel(
    'io.github.pixeval.pixeval_flutter/display',
  );

  /// Requests the device to run at its highest supported physical refresh rate.
  /// Returns the configured refresh rate in Hz (e.g. 120.0), or 60.0 on fallback.
  static Future<double> setHighRefreshRate() async {
    if (kIsWeb || !Platform.isAndroid) {
      return 60.0;
    }
    try {
      final rate = await _channel.invokeMethod<double>('setHighRefreshRate');
      return rate ?? 60.0;
    } catch (e) {
      debugPrint('DisplayModeService.setHighRefreshRate failed: $e');
      return 60.0;
    }
  }

  /// Queries all hardware-supported display modes on the current display.
  static Future<List<Map<String, dynamic>>> getSupportedModes() async {
    if (kIsWeb || !Platform.isAndroid) {
      return const [];
    }
    try {
      final modes = await _channel.invokeListMethod<Map<dynamic, dynamic>>(
        'getSupportedModes',
      );
      if (modes == null) return const [];
      return modes.map((m) => Map<String, dynamic>.from(m)).toList();
    } catch (e) {
      debugPrint('DisplayModeService.getSupportedModes failed: $e');
      return const [];
    }
  }
}
