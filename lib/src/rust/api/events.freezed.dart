// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'events.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;
/// @nodoc
mixin _$SessionEvent {





@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is SessionEvent);
}


@override
int get hashCode => runtimeType.hashCode;

@override
String toString() {
    return 'SessionEvent()';
}


}

/// @nodoc
class $SessionEventCopyWith<$Res>  {
$SessionEventCopyWith(SessionEvent _, $Res Function(SessionEvent) __);
}


/// Adds pattern-matching-related methods to [SessionEvent].
extension SessionEventPatterns on SessionEvent {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>({TResult Function( SessionEvent_AuthInvalidated value)?  authInvalidated,TResult Function( SessionEvent_RateLimited value)?  rateLimited,required TResult orElse(),}){
final _that = this;
switch (_that) {
case SessionEvent_AuthInvalidated() when authInvalidated != null:
return authInvalidated(_that);case SessionEvent_RateLimited() when rateLimited != null:
return rateLimited(_that);case _:
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

@optionalTypeArgs TResult map<TResult extends Object?>({required TResult Function( SessionEvent_AuthInvalidated value)  authInvalidated,required TResult Function( SessionEvent_RateLimited value)  rateLimited,}){
final _that = this;
switch (_that) {
case SessionEvent_AuthInvalidated():
return authInvalidated(_that);case SessionEvent_RateLimited():
return rateLimited(_that);}
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>({TResult? Function( SessionEvent_AuthInvalidated value)?  authInvalidated,TResult? Function( SessionEvent_RateLimited value)?  rateLimited,}){
final _that = this;
switch (_that) {
case SessionEvent_AuthInvalidated() when authInvalidated != null:
return authInvalidated(_that);case SessionEvent_RateLimited() when rateLimited != null:
return rateLimited(_that);case _:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>({TResult Function( String reason)?  authInvalidated,TResult Function( BigInt retryAfterSeconds)?  rateLimited,required TResult orElse(),}) {final _that = this;
switch (_that) {
case SessionEvent_AuthInvalidated() when authInvalidated != null:
return authInvalidated(_that.reason);case SessionEvent_RateLimited() when rateLimited != null:
return rateLimited(_that.retryAfterSeconds);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>({required TResult Function( String reason)  authInvalidated,required TResult Function( BigInt retryAfterSeconds)  rateLimited,}) {final _that = this;
switch (_that) {
case SessionEvent_AuthInvalidated():
return authInvalidated(_that.reason);case SessionEvent_RateLimited():
return rateLimited(_that.retryAfterSeconds);}
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>({TResult? Function( String reason)?  authInvalidated,TResult? Function( BigInt retryAfterSeconds)?  rateLimited,}) {final _that = this;
switch (_that) {
case SessionEvent_AuthInvalidated() when authInvalidated != null:
return authInvalidated(_that.reason);case SessionEvent_RateLimited() when rateLimited != null:
return rateLimited(_that.retryAfterSeconds);case _:
  return null;

}
}

}

/// @nodoc


class SessionEvent_AuthInvalidated extends SessionEvent {
  const SessionEvent_AuthInvalidated({required this.reason}): super._();
  

 final  String reason;

/// Create a copy of SessionEvent
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SessionEvent_AuthInvalidatedCopyWith<SessionEvent_AuthInvalidated> get copyWith => _$SessionEvent_AuthInvalidatedCopyWithImpl<SessionEvent_AuthInvalidated>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is SessionEvent_AuthInvalidated&&(identical(other.reason, reason) || other.reason == reason));
}


@override
int get hashCode {
    return Object.hash(runtimeType,reason);
}

@override
String toString() {
    return 'SessionEvent.authInvalidated(reason: $reason)';
}


}

/// @nodoc
abstract mixin class $SessionEvent_AuthInvalidatedCopyWith<$Res> implements $SessionEventCopyWith<$Res> {
  factory $SessionEvent_AuthInvalidatedCopyWith(SessionEvent_AuthInvalidated value, $Res Function(SessionEvent_AuthInvalidated) _then) = _$SessionEvent_AuthInvalidatedCopyWithImpl;
@useResult
$Res call({
 String reason
});




}
/// @nodoc
class _$SessionEvent_AuthInvalidatedCopyWithImpl<$Res>
    implements $SessionEvent_AuthInvalidatedCopyWith<$Res> {
  _$SessionEvent_AuthInvalidatedCopyWithImpl(this._self, this._then);

  final SessionEvent_AuthInvalidated _self;
  final $Res Function(SessionEvent_AuthInvalidated) _then;

/// Create a copy of SessionEvent
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') $Res call({Object? reason = null,}) {
  return _then(SessionEvent_AuthInvalidated(
reason: null == reason ? _self.reason : reason // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}

/// @nodoc


class SessionEvent_RateLimited extends SessionEvent {
  const SessionEvent_RateLimited({required this.retryAfterSeconds}): super._();
  

 final  BigInt retryAfterSeconds;

/// Create a copy of SessionEvent
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$SessionEvent_RateLimitedCopyWith<SessionEvent_RateLimited> get copyWith => _$SessionEvent_RateLimitedCopyWithImpl<SessionEvent_RateLimited>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is SessionEvent_RateLimited&&(identical(other.retryAfterSeconds, retryAfterSeconds) || other.retryAfterSeconds == retryAfterSeconds));
}


@override
int get hashCode {
    return Object.hash(runtimeType,retryAfterSeconds);
}

@override
String toString() {
    return 'SessionEvent.rateLimited(retryAfterSeconds: $retryAfterSeconds)';
}


}

/// @nodoc
abstract mixin class $SessionEvent_RateLimitedCopyWith<$Res> implements $SessionEventCopyWith<$Res> {
  factory $SessionEvent_RateLimitedCopyWith(SessionEvent_RateLimited value, $Res Function(SessionEvent_RateLimited) _then) = _$SessionEvent_RateLimitedCopyWithImpl;
@useResult
$Res call({
 BigInt retryAfterSeconds
});




}
/// @nodoc
class _$SessionEvent_RateLimitedCopyWithImpl<$Res>
    implements $SessionEvent_RateLimitedCopyWith<$Res> {
  _$SessionEvent_RateLimitedCopyWithImpl(this._self, this._then);

  final SessionEvent_RateLimited _self;
  final $Res Function(SessionEvent_RateLimited) _then;

/// Create a copy of SessionEvent
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') $Res call({Object? retryAfterSeconds = null,}) {
  return _then(SessionEvent_RateLimited(
retryAfterSeconds: null == retryAfterSeconds ? _self.retryAfterSeconds : retryAfterSeconds // ignore: cast_nullable_to_non_nullable
as BigInt,
  ));
}


}

// dart format on
