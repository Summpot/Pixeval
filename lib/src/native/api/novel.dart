import '../pixeval.g.dart';
export '../pixeval.g.dart'
    show NovelDetailDto, NovelChapterDto, NovelContentDto;

import '../native_api.dart';

Future<NovelDetailDto> getNovelDetail({required int novelId}) =>
    NativeApi.instance.api.getNovelDetail(novelId: novelId);

Future<NovelContentDto> getNovelContentRendered({required int novelId}) =>
    NativeApi.instance.api.getNovelContentRendered(novelId: novelId);
