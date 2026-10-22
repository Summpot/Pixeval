import '../pixeval.g.dart';
export '../pixeval.g.dart' show SessionEvent;

import '../native_api.dart';

Stream<SessionEvent> createSessionEventStream({String? streamId}) =>
    NativeApi.instance.api.createSessionEventStream(streamId: streamId);

Future<void> cancelSessionEventStream({String? streamId}) =>
    NativeApi.instance.api.cancelSessionEventStream(streamId: streamId);
