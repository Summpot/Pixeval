import '../pixeval.g.dart';
import '../models.dart';
export '../pixeval.g.dart'
    show FilterCompletionDto, FilterDiagnosticDto, FilterAnalysisResultDto;

import '../native_api.dart';

Future<FilterAnalysisResultDto> analyzeFilterQuery({
  required String query,
  required int caretPos,
}) =>
    NativeApi.instance.api.analyzeFilterQuery(query: query, caretPos: caretPos);

Future<List<IllustSummary>> filterArtworksByQuery({
  required String query,
  required List<IllustSummary> artworks,
}) => NativeApi.instance.api.filterArtworksByQuery(
  query: query,
  artworks: artworks,
);
