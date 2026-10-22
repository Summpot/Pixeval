import '../pixeval.g.dart';
export '../pixeval.g.dart' show ParsedPixivLink;

import '../native_api.dart';

Future<ParsedPixivLink> parsePixivUrl({required String url}) =>
    NativeApi.instance.api.parsePixivUrl(url: url);
