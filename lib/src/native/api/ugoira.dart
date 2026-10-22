import '../pixeval.g.dart';
export '../pixeval.g.dart'
    show UgoiraFrameDto, UgoiraMetadataDto, UgoiraFramesDto;

import '../native_api.dart';

Future<UgoiraMetadataDto> getUgoiraMetadata({required int illustId}) =>
    NativeApi.instance.api.getUgoiraMetadata(illustId: illustId);

Future<UgoiraFramesDto> extractUgoiraFramesFromZipBytes({
  required List<int> zipBytes,
  required List<int> delays,
}) => NativeApi.instance.api.extractUgoiraFramesFromZipBytes(
  zipBytes: zipBytes,
  delays: delays,
);

Future<void> exportUgoira({
  required String zipPath,
  required String outputPath,
  required String formatName,
  required List<int> frameDelaysMs,
}) => NativeApi.instance.api.exportUgoira(
  zipPath: zipPath,
  outputPath: outputPath,
  formatName: formatName,
  frameDelaysMs: frameDelaysMs,
);
