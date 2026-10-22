import '../pixeval.g.dart';
export '../pixeval.g.dart' show SauceNaoItemDto;

import '../native_api.dart';

Future<List<SauceNaoItemDto>> searchSaucenao({
  required List<int> imageBytes,
  String? apiKey,
}) => NativeApi.instance.api.searchSaucenao(
  imageBytes: imageBytes,
  apiKey: apiKey,
);
