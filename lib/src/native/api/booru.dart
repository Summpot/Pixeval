import '../pixeval.g.dart';
export '../pixeval.g.dart' show BooruPostSummaryDto, BooruSearchResultDto;

import '../native_api.dart';

Future<BooruSearchResultDto> searchBooru({
  required String platformName,
  required String tags,
  required int page,
}) => NativeApi.instance.api.searchBooru(
  platformName: platformName,
  tags: tags,
  page: page,
);
