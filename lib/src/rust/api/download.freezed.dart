// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'download.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;
/// @nodoc
mixin _$DownloadProgressEvent {

 String get destination;
/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$DownloadProgressEventCopyWith<DownloadProgressEvent> get copyWith => _$DownloadProgressEventCopyWithImpl<DownloadProgressEvent>(this as DownloadProgressEvent, _$identity);



@override
bool operator ==(Object other) {
  final _this = this as DownloadProgressEvent;
  return identical(this, other) || (other.runtimeType == runtimeType&&other is DownloadProgressEvent&&(identical(other.destination, _this.destination) || other.destination == _this.destination));
}


@override
int get hashCode {
  final _this = this as DownloadProgressEvent;
  return Object.hash(runtimeType,_this.destination);
}

@override
String toString() {
  final _this = this as DownloadProgressEvent;
  return 'DownloadProgressEvent(destination: ${_this.destination})';
}


}

/// @nodoc
abstract mixin class $DownloadProgressEventCopyWith<$Res>  {
  factory $DownloadProgressEventCopyWith(DownloadProgressEvent value, $Res Function(DownloadProgressEvent) _then) = _$DownloadProgressEventCopyWithImpl;
@useResult
$Res call({
 String destination
});




}
/// @nodoc
class _$DownloadProgressEventCopyWithImpl<$Res>
    implements $DownloadProgressEventCopyWith<$Res> {
  _$DownloadProgressEventCopyWithImpl(this._self, this._then);

  final DownloadProgressEvent _self;
  final $Res Function(DownloadProgressEvent) _then;

/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') @override $Res call({Object? destination = null,}) {
  return _then(_self.copyWith(
destination: null == destination ? _self.destination : destination // ignore: cast_nullable_to_non_nullable
as String,
  ));
}

}


/// Adds pattern-matching-related methods to [DownloadProgressEvent].
extension DownloadProgressEventPatterns on DownloadProgressEvent {
/// A variant of `map` that fallback to returning `orElse`.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case _:
///     return orElse();
/// }
/// ```

@optionalTypeArgs TResult maybeMap<TResult extends Object?>({TResult Function( DownloadProgressEvent_ProgressUpdated value)?  progressUpdated,TResult Function( DownloadProgressEvent_StateChanged value)?  stateChanged,TResult Function( DownloadProgressEvent_Completed value)?  completed,required TResult orElse(),}){
final _that = this;
switch (_that) {
case DownloadProgressEvent_ProgressUpdated() when progressUpdated != null:
return progressUpdated(_that);case DownloadProgressEvent_StateChanged() when stateChanged != null:
return stateChanged(_that);case DownloadProgressEvent_Completed() when completed != null:
return completed(_that);case _:
  return orElse();

}
}
/// A `switch`-like method, using callbacks.
///
/// Callbacks receives the raw object, upcasted.
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case final Subclass2 value:
///     return ...;
/// }
/// ```

@optionalTypeArgs TResult map<TResult extends Object?>({required TResult Function( DownloadProgressEvent_ProgressUpdated value)  progressUpdated,required TResult Function( DownloadProgressEvent_StateChanged value)  stateChanged,required TResult Function( DownloadProgressEvent_Completed value)  completed,}){
final _that = this;
switch (_that) {
case DownloadProgressEvent_ProgressUpdated():
return progressUpdated(_that);case DownloadProgressEvent_StateChanged():
return stateChanged(_that);case DownloadProgressEvent_Completed():
return completed(_that);}
}
/// A variant of `map` that fallback to returning `null`.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case final Subclass value:
///     return ...;
///   case _:
///     return null;
/// }
/// ```

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>({TResult? Function( DownloadProgressEvent_ProgressUpdated value)?  progressUpdated,TResult? Function( DownloadProgressEvent_StateChanged value)?  stateChanged,TResult? Function( DownloadProgressEvent_Completed value)?  completed,}){
final _that = this;
switch (_that) {
case DownloadProgressEvent_ProgressUpdated() when progressUpdated != null:
return progressUpdated(_that);case DownloadProgressEvent_StateChanged() when stateChanged != null:
return stateChanged(_that);case DownloadProgressEvent_Completed() when completed != null:
return completed(_that);case _:
  return null;

}
}
/// A variant of `when` that fallback to an `orElse` callback.
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case _:
///     return orElse();
/// }
/// ```

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>({TResult Function( String destination,  double progressPercentage,  BigInt downloadedBytes,  BigInt totalBytes)?  progressUpdated,TResult Function( String destination,  String state,  String? errorMessage)?  stateChanged,TResult Function( String destination)?  completed,required TResult orElse(),}) {final _that = this;
switch (_that) {
case DownloadProgressEvent_ProgressUpdated() when progressUpdated != null:
return progressUpdated(_that.destination,_that.progressPercentage,_that.downloadedBytes,_that.totalBytes);case DownloadProgressEvent_StateChanged() when stateChanged != null:
return stateChanged(_that.destination,_that.state,_that.errorMessage);case DownloadProgressEvent_Completed() when completed != null:
return completed(_that.destination);case _:
  return orElse();

}
}
/// A `switch`-like method, using callbacks.
///
/// As opposed to `map`, this offers destructuring.
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case Subclass2(:final field2):
///     return ...;
/// }
/// ```

@optionalTypeArgs TResult when<TResult extends Object?>({required TResult Function( String destination,  double progressPercentage,  BigInt downloadedBytes,  BigInt totalBytes)  progressUpdated,required TResult Function( String destination,  String state,  String? errorMessage)  stateChanged,required TResult Function( String destination)  completed,}) {final _that = this;
switch (_that) {
case DownloadProgressEvent_ProgressUpdated():
return progressUpdated(_that.destination,_that.progressPercentage,_that.downloadedBytes,_that.totalBytes);case DownloadProgressEvent_StateChanged():
return stateChanged(_that.destination,_that.state,_that.errorMessage);case DownloadProgressEvent_Completed():
return completed(_that.destination);}
}
/// A variant of `when` that fallback to returning `null`
///
/// It is equivalent to doing:
/// ```dart
/// switch (sealedClass) {
///   case Subclass(:final field):
///     return ...;
///   case _:
///     return null;
/// }
/// ```

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>({TResult? Function( String destination,  double progressPercentage,  BigInt downloadedBytes,  BigInt totalBytes)?  progressUpdated,TResult? Function( String destination,  String state,  String? errorMessage)?  stateChanged,TResult? Function( String destination)?  completed,}) {final _that = this;
switch (_that) {
case DownloadProgressEvent_ProgressUpdated() when progressUpdated != null:
return progressUpdated(_that.destination,_that.progressPercentage,_that.downloadedBytes,_that.totalBytes);case DownloadProgressEvent_StateChanged() when stateChanged != null:
return stateChanged(_that.destination,_that.state,_that.errorMessage);case DownloadProgressEvent_Completed() when completed != null:
return completed(_that.destination);case _:
  return null;

}
}

}

/// @nodoc


class DownloadProgressEvent_ProgressUpdated extends DownloadProgressEvent {
  const DownloadProgressEvent_ProgressUpdated({required this.destination, required this.progressPercentage, required this.downloadedBytes, required this.totalBytes}): super._();
  

@override final  String destination;
 final  double progressPercentage;
 final  BigInt downloadedBytes;
 final  BigInt totalBytes;

/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$DownloadProgressEvent_ProgressUpdatedCopyWith<DownloadProgressEvent_ProgressUpdated> get copyWith => _$DownloadProgressEvent_ProgressUpdatedCopyWithImpl<DownloadProgressEvent_ProgressUpdated>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is DownloadProgressEvent_ProgressUpdated&&(identical(other.destination, destination) || other.destination == destination)&&(identical(other.progressPercentage, progressPercentage) || other.progressPercentage == progressPercentage)&&(identical(other.downloadedBytes, downloadedBytes) || other.downloadedBytes == downloadedBytes)&&(identical(other.totalBytes, totalBytes) || other.totalBytes == totalBytes));
}


@override
int get hashCode {
    return Object.hash(runtimeType,destination,progressPercentage,downloadedBytes,totalBytes);
}

@override
String toString() {
    return 'DownloadProgressEvent.progressUpdated(destination: $destination, progressPercentage: $progressPercentage, downloadedBytes: $downloadedBytes, totalBytes: $totalBytes)';
}


}

/// @nodoc
abstract mixin class $DownloadProgressEvent_ProgressUpdatedCopyWith<$Res> implements $DownloadProgressEventCopyWith<$Res> {
  factory $DownloadProgressEvent_ProgressUpdatedCopyWith(DownloadProgressEvent_ProgressUpdated value, $Res Function(DownloadProgressEvent_ProgressUpdated) _then) = _$DownloadProgressEvent_ProgressUpdatedCopyWithImpl;
@override @useResult
$Res call({
 String destination, double progressPercentage, BigInt downloadedBytes, BigInt totalBytes
});




}
/// @nodoc
class _$DownloadProgressEvent_ProgressUpdatedCopyWithImpl<$Res>
    implements $DownloadProgressEvent_ProgressUpdatedCopyWith<$Res> {
  _$DownloadProgressEvent_ProgressUpdatedCopyWithImpl(this._self, this._then);

  final DownloadProgressEvent_ProgressUpdated _self;
  final $Res Function(DownloadProgressEvent_ProgressUpdated) _then;

/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? destination = null,Object? progressPercentage = null,Object? downloadedBytes = null,Object? totalBytes = null,}) {
  return _then(DownloadProgressEvent_ProgressUpdated(
destination: null == destination ? _self.destination : destination // ignore: cast_nullable_to_non_nullable
as String,progressPercentage: null == progressPercentage ? _self.progressPercentage : progressPercentage // ignore: cast_nullable_to_non_nullable
as double,downloadedBytes: null == downloadedBytes ? _self.downloadedBytes : downloadedBytes // ignore: cast_nullable_to_non_nullable
as BigInt,totalBytes: null == totalBytes ? _self.totalBytes : totalBytes // ignore: cast_nullable_to_non_nullable
as BigInt,
  ));
}


}

/// @nodoc


class DownloadProgressEvent_StateChanged extends DownloadProgressEvent {
  const DownloadProgressEvent_StateChanged({required this.destination, required this.state, this.errorMessage}): super._();
  

@override final  String destination;
 final  String state;
 final  String? errorMessage;

/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$DownloadProgressEvent_StateChangedCopyWith<DownloadProgressEvent_StateChanged> get copyWith => _$DownloadProgressEvent_StateChangedCopyWithImpl<DownloadProgressEvent_StateChanged>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is DownloadProgressEvent_StateChanged&&(identical(other.destination, destination) || other.destination == destination)&&(identical(other.state, state) || other.state == state)&&(identical(other.errorMessage, errorMessage) || other.errorMessage == errorMessage));
}


@override
int get hashCode {
    return Object.hash(runtimeType,destination,state,errorMessage);
}

@override
String toString() {
    return 'DownloadProgressEvent.stateChanged(destination: $destination, state: $state, errorMessage: $errorMessage)';
}


}

/// @nodoc
abstract mixin class $DownloadProgressEvent_StateChangedCopyWith<$Res> implements $DownloadProgressEventCopyWith<$Res> {
  factory $DownloadProgressEvent_StateChangedCopyWith(DownloadProgressEvent_StateChanged value, $Res Function(DownloadProgressEvent_StateChanged) _then) = _$DownloadProgressEvent_StateChangedCopyWithImpl;
@override @useResult
$Res call({
 String destination, String state, String? errorMessage
});




}
/// @nodoc
class _$DownloadProgressEvent_StateChangedCopyWithImpl<$Res>
    implements $DownloadProgressEvent_StateChangedCopyWith<$Res> {
  _$DownloadProgressEvent_StateChangedCopyWithImpl(this._self, this._then);

  final DownloadProgressEvent_StateChanged _self;
  final $Res Function(DownloadProgressEvent_StateChanged) _then;

/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? destination = null,Object? state = null,Object? errorMessage = freezed,}) {
  return _then(DownloadProgressEvent_StateChanged(
destination: null == destination ? _self.destination : destination // ignore: cast_nullable_to_non_nullable
as String,state: null == state ? _self.state : state // ignore: cast_nullable_to_non_nullable
as String,errorMessage: freezed == errorMessage ? _self.errorMessage : errorMessage // ignore: cast_nullable_to_non_nullable
as String?,
  ));
}


}

/// @nodoc


class DownloadProgressEvent_Completed extends DownloadProgressEvent {
  const DownloadProgressEvent_Completed({required this.destination}): super._();
  

@override final  String destination;

/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@override @JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$DownloadProgressEvent_CompletedCopyWith<DownloadProgressEvent_Completed> get copyWith => _$DownloadProgressEvent_CompletedCopyWithImpl<DownloadProgressEvent_Completed>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is DownloadProgressEvent_Completed&&(identical(other.destination, destination) || other.destination == destination));
}


@override
int get hashCode {
    return Object.hash(runtimeType,destination);
}

@override
String toString() {
    return 'DownloadProgressEvent.completed(destination: $destination)';
}


}

/// @nodoc
abstract mixin class $DownloadProgressEvent_CompletedCopyWith<$Res> implements $DownloadProgressEventCopyWith<$Res> {
  factory $DownloadProgressEvent_CompletedCopyWith(DownloadProgressEvent_Completed value, $Res Function(DownloadProgressEvent_Completed) _then) = _$DownloadProgressEvent_CompletedCopyWithImpl;
@override @useResult
$Res call({
 String destination
});




}
/// @nodoc
class _$DownloadProgressEvent_CompletedCopyWithImpl<$Res>
    implements $DownloadProgressEvent_CompletedCopyWith<$Res> {
  _$DownloadProgressEvent_CompletedCopyWithImpl(this._self, this._then);

  final DownloadProgressEvent_Completed _self;
  final $Res Function(DownloadProgressEvent_Completed) _then;

/// Create a copy of DownloadProgressEvent
/// with the given fields replaced by the non-null parameter values.
@override @pragma('vm:prefer-inline') $Res call({Object? destination = null,}) {
  return _then(DownloadProgressEvent_Completed(
destination: null == destination ? _self.destination : destination // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}

// dart format on
