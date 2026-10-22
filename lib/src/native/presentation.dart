import 'models.dart';
import 'pixeval.g.dart';

/// Frontend presentation policy stays in Dart; the native contract exposes the
/// shared domain model, also consumed by the .NET frontend.
IllustSummary presentIllustration(PixevalMakoIllustration image) {
  final preview = image.imageUrls.large ?? image.imageUrls.medium ?? '';
  final original = image.metaSinglePage.originalImageUrl ?? preview;
  final pages = image.pageCount > 1 && image.metaPages.isNotEmpty
      ? image.metaPages
            .map((page) => page.imageUrls.large ?? page.imageUrls.medium ?? '')
            .where((url) => url.isNotEmpty)
            .toList()
      : [preview];
  final originals = image.pageCount > 1 && image.metaPages.isNotEmpty
      ? image.metaPages
            .map(
              (page) => page.imageUrls.original ?? page.imageUrls.large ?? '',
            )
            .where((url) => url.isNotEmpty)
            .toList()
      : [original];
  final restriction = image.xRestrict.ordinal;
  return IllustSummary(
    id: image.id,
    title: image.title,
    caption: image.caption,
    userId: image.user.id,
    userName: image.user.name,
    userAccount: image.user.account,
    userAvatarUrl:
        image.user.profileImageUrls.medium ??
        image.user.profileImageUrls.px50x50 ??
        image.user.profileImageUrls.px16x16 ??
        '',
    previewUrl: preview,
    originalUrl: original,
    width: image.width,
    height: image.height,
    pageCount: image.pageCount,
    isBookmarked: image.isBookmarked,
    totalBookmarks: image.totalBookmarks,
    totalViews: image.totalView,
    tags: image.tags.map((tag) => tag.name).toList(),
    isR18:
        restriction != 0 ||
        image.tags.any(
          (tag) =>
              tag.name.toLowerCase().contains('r-18') ||
              tag.name.contains('R-18G'),
        ),
    isUgoira: image.illustType.ordinal == 2,
    pageUrls: pages,
    originalPageUrls: originals,
    thumbnailUrl: image.imageUrls.squareMedium,
    filterMetadata: FilterMetadata(
      createDateTimestamp:
          (DateTime.tryParse(image.createDate)?.millisecondsSinceEpoch ?? 0) ~/
          1000,
      aiType: image.illustAiType == 2 ? 1 : 0,
      xRestrict: restriction,
      illustrationType: image.illustType.ordinal,
      translatedTags: image.tags.map((tag) => tag.translatedName).toList(),
    ),
  );
}

PixevalFiltersArtworkMetadata filterMetadata(IllustSummary image) {
  final metadata = image.filterMetadata;
  return PixevalFiltersArtworkMetadata(
    id: image.id.toString(),
    title: image.title,
    authorName: image.userName,
    authorAccount: image.userAccount,
    tags: [
      for (var i = 0; i < image.tags.length; i++)
        PixevalFiltersArtworkTag(
          name: image.tags[i],
          translatedName: metadata != null && i < metadata.translatedTags.length
              ? metadata.translatedTags[i]
              : null,
        ),
    ],
    totalBookmarks: image.totalBookmarks,
    createDateTimestamp: metadata?.createDateTimestamp ?? 0,
    width: image.width,
    height: image.height,
    xRestrict: metadata?.xRestrict ?? (image.isR18 ? 1 : 0),
    aiType: metadata?.aiType ?? 0,
    illustrationType: metadata?.illustrationType ?? (image.isUgoira ? 2 : 0),
  );
}
