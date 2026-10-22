import '../native_api.dart';

Future<String> version() => NativeApi.instance.api.version();

Future<String> ping({required String message}) =>
    NativeApi.instance.api.ping(message: message);

Future<String> asyncPing({required String message}) =>
    NativeApi.instance.api.asyncPing(message: message);
