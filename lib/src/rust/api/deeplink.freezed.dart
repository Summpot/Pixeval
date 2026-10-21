// GENERATED CODE - DO NOT MODIFY BY HAND
// coverage:ignore-file
// ignore_for_file: type=lint, type=warning, deprecated_member_use, deprecated_member_use_from_same_package
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'deeplink.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format off
T _$identity<T>(T value) => value;
/// @nodoc
mixin _$ParsedPixivLink {





@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is ParsedPixivLink);
}


@override
int get hashCode => runtimeType.hashCode;

@override
String toString() {
    return 'ParsedPixivLink()';
}


}

/// @nodoc
class $ParsedPixivLinkCopyWith<$Res>  {
$ParsedPixivLinkCopyWith(ParsedPixivLink _, $Res Function(ParsedPixivLink) __);
}


/// Adds pattern-matching-related methods to [ParsedPixivLink].
extension ParsedPixivLinkPatterns on ParsedPixivLink {
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

@optionalTypeArgs TResult maybeMap<TResult extends Object?>({TResult Function( ParsedPixivLink_Artwork value)?  artwork,TResult Function( ParsedPixivLink_Novel value)?  novel,TResult Function( ParsedPixivLink_User value)?  user,TResult Function( ParsedPixivLink_Unknown value)?  unknown,required TResult orElse(),}){
final _that = this;
switch (_that) {
case ParsedPixivLink_Artwork() when artwork != null:
return artwork(_that);case ParsedPixivLink_Novel() when novel != null:
return novel(_that);case ParsedPixivLink_User() when user != null:
return user(_that);case ParsedPixivLink_Unknown() when unknown != null:
return unknown(_that);case _:
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

@optionalTypeArgs TResult map<TResult extends Object?>({required TResult Function( ParsedPixivLink_Artwork value)  artwork,required TResult Function( ParsedPixivLink_Novel value)  novel,required TResult Function( ParsedPixivLink_User value)  user,required TResult Function( ParsedPixivLink_Unknown value)  unknown,}){
final _that = this;
switch (_that) {
case ParsedPixivLink_Artwork():
return artwork(_that);case ParsedPixivLink_Novel():
return novel(_that);case ParsedPixivLink_User():
return user(_that);case ParsedPixivLink_Unknown():
return unknown(_that);}
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

@optionalTypeArgs TResult? mapOrNull<TResult extends Object?>({TResult? Function( ParsedPixivLink_Artwork value)?  artwork,TResult? Function( ParsedPixivLink_Novel value)?  novel,TResult? Function( ParsedPixivLink_User value)?  user,TResult? Function( ParsedPixivLink_Unknown value)?  unknown,}){
final _that = this;
switch (_that) {
case ParsedPixivLink_Artwork() when artwork != null:
return artwork(_that);case ParsedPixivLink_Novel() when novel != null:
return novel(_that);case ParsedPixivLink_User() when user != null:
return user(_that);case ParsedPixivLink_Unknown() when unknown != null:
return unknown(_that);case _:
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

@optionalTypeArgs TResult maybeWhen<TResult extends Object?>({TResult Function( PlatformInt64 id)?  artwork,TResult Function( PlatformInt64 id)?  novel,TResult Function( PlatformInt64 id)?  user,TResult Function( String rawUrl)?  unknown,required TResult orElse(),}) {final _that = this;
switch (_that) {
case ParsedPixivLink_Artwork() when artwork != null:
return artwork(_that.id);case ParsedPixivLink_Novel() when novel != null:
return novel(_that.id);case ParsedPixivLink_User() when user != null:
return user(_that.id);case ParsedPixivLink_Unknown() when unknown != null:
return unknown(_that.rawUrl);case _:
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

@optionalTypeArgs TResult when<TResult extends Object?>({required TResult Function( PlatformInt64 id)  artwork,required TResult Function( PlatformInt64 id)  novel,required TResult Function( PlatformInt64 id)  user,required TResult Function( String rawUrl)  unknown,}) {final _that = this;
switch (_that) {
case ParsedPixivLink_Artwork():
return artwork(_that.id);case ParsedPixivLink_Novel():
return novel(_that.id);case ParsedPixivLink_User():
return user(_that.id);case ParsedPixivLink_Unknown():
return unknown(_that.rawUrl);}
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

@optionalTypeArgs TResult? whenOrNull<TResult extends Object?>({TResult? Function( PlatformInt64 id)?  artwork,TResult? Function( PlatformInt64 id)?  novel,TResult? Function( PlatformInt64 id)?  user,TResult? Function( String rawUrl)?  unknown,}) {final _that = this;
switch (_that) {
case ParsedPixivLink_Artwork() when artwork != null:
return artwork(_that.id);case ParsedPixivLink_Novel() when novel != null:
return novel(_that.id);case ParsedPixivLink_User() when user != null:
return user(_that.id);case ParsedPixivLink_Unknown() when unknown != null:
return unknown(_that.rawUrl);case _:
  return null;

}
}

}

/// @nodoc


class ParsedPixivLink_Artwork extends ParsedPixivLink {
  const ParsedPixivLink_Artwork({required this.id}): super._();
  

 final  PlatformInt64 id;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ParsedPixivLink_ArtworkCopyWith<ParsedPixivLink_Artwork> get copyWith => _$ParsedPixivLink_ArtworkCopyWithImpl<ParsedPixivLink_Artwork>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is ParsedPixivLink_Artwork&&(identical(other.id, id) || other.id == id));
}


@override
int get hashCode {
    return Object.hash(runtimeType,id);
}

@override
String toString() {
    return 'ParsedPixivLink.artwork(id: $id)';
}


}

/// @nodoc
abstract mixin class $ParsedPixivLink_ArtworkCopyWith<$Res> implements $ParsedPixivLinkCopyWith<$Res> {
  factory $ParsedPixivLink_ArtworkCopyWith(ParsedPixivLink_Artwork value, $Res Function(ParsedPixivLink_Artwork) _then) = _$ParsedPixivLink_ArtworkCopyWithImpl;
@useResult
$Res call({
 PlatformInt64 id
});




}
/// @nodoc
class _$ParsedPixivLink_ArtworkCopyWithImpl<$Res>
    implements $ParsedPixivLink_ArtworkCopyWith<$Res> {
  _$ParsedPixivLink_ArtworkCopyWithImpl(this._self, this._then);

  final ParsedPixivLink_Artwork _self;
  final $Res Function(ParsedPixivLink_Artwork) _then;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') $Res call({Object? id = null,}) {
  return _then(ParsedPixivLink_Artwork(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as PlatformInt64,
  ));
}


}

/// @nodoc


class ParsedPixivLink_Novel extends ParsedPixivLink {
  const ParsedPixivLink_Novel({required this.id}): super._();
  

 final  PlatformInt64 id;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ParsedPixivLink_NovelCopyWith<ParsedPixivLink_Novel> get copyWith => _$ParsedPixivLink_NovelCopyWithImpl<ParsedPixivLink_Novel>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is ParsedPixivLink_Novel&&(identical(other.id, id) || other.id == id));
}


@override
int get hashCode {
    return Object.hash(runtimeType,id);
}

@override
String toString() {
    return 'ParsedPixivLink.novel(id: $id)';
}


}

/// @nodoc
abstract mixin class $ParsedPixivLink_NovelCopyWith<$Res> implements $ParsedPixivLinkCopyWith<$Res> {
  factory $ParsedPixivLink_NovelCopyWith(ParsedPixivLink_Novel value, $Res Function(ParsedPixivLink_Novel) _then) = _$ParsedPixivLink_NovelCopyWithImpl;
@useResult
$Res call({
 PlatformInt64 id
});




}
/// @nodoc
class _$ParsedPixivLink_NovelCopyWithImpl<$Res>
    implements $ParsedPixivLink_NovelCopyWith<$Res> {
  _$ParsedPixivLink_NovelCopyWithImpl(this._self, this._then);

  final ParsedPixivLink_Novel _self;
  final $Res Function(ParsedPixivLink_Novel) _then;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') $Res call({Object? id = null,}) {
  return _then(ParsedPixivLink_Novel(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as PlatformInt64,
  ));
}


}

/// @nodoc


class ParsedPixivLink_User extends ParsedPixivLink {
  const ParsedPixivLink_User({required this.id}): super._();
  

 final  PlatformInt64 id;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ParsedPixivLink_UserCopyWith<ParsedPixivLink_User> get copyWith => _$ParsedPixivLink_UserCopyWithImpl<ParsedPixivLink_User>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is ParsedPixivLink_User&&(identical(other.id, id) || other.id == id));
}


@override
int get hashCode {
    return Object.hash(runtimeType,id);
}

@override
String toString() {
    return 'ParsedPixivLink.user(id: $id)';
}


}

/// @nodoc
abstract mixin class $ParsedPixivLink_UserCopyWith<$Res> implements $ParsedPixivLinkCopyWith<$Res> {
  factory $ParsedPixivLink_UserCopyWith(ParsedPixivLink_User value, $Res Function(ParsedPixivLink_User) _then) = _$ParsedPixivLink_UserCopyWithImpl;
@useResult
$Res call({
 PlatformInt64 id
});




}
/// @nodoc
class _$ParsedPixivLink_UserCopyWithImpl<$Res>
    implements $ParsedPixivLink_UserCopyWith<$Res> {
  _$ParsedPixivLink_UserCopyWithImpl(this._self, this._then);

  final ParsedPixivLink_User _self;
  final $Res Function(ParsedPixivLink_User) _then;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') $Res call({Object? id = null,}) {
  return _then(ParsedPixivLink_User(
id: null == id ? _self.id : id // ignore: cast_nullable_to_non_nullable
as PlatformInt64,
  ));
}


}

/// @nodoc


class ParsedPixivLink_Unknown extends ParsedPixivLink {
  const ParsedPixivLink_Unknown({required this.rawUrl}): super._();
  

 final  String rawUrl;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@JsonKey(includeFromJson: false, includeToJson: false)
@pragma('vm:prefer-inline')
$ParsedPixivLink_UnknownCopyWith<ParsedPixivLink_Unknown> get copyWith => _$ParsedPixivLink_UnknownCopyWithImpl<ParsedPixivLink_Unknown>(this, _$identity);



@override
bool operator ==(Object other) {
    return identical(this, other) || (other.runtimeType == runtimeType&&other is ParsedPixivLink_Unknown&&(identical(other.rawUrl, rawUrl) || other.rawUrl == rawUrl));
}


@override
int get hashCode {
    return Object.hash(runtimeType,rawUrl);
}

@override
String toString() {
    return 'ParsedPixivLink.unknown(rawUrl: $rawUrl)';
}


}

/// @nodoc
abstract mixin class $ParsedPixivLink_UnknownCopyWith<$Res> implements $ParsedPixivLinkCopyWith<$Res> {
  factory $ParsedPixivLink_UnknownCopyWith(ParsedPixivLink_Unknown value, $Res Function(ParsedPixivLink_Unknown) _then) = _$ParsedPixivLink_UnknownCopyWithImpl;
@useResult
$Res call({
 String rawUrl
});




}
/// @nodoc
class _$ParsedPixivLink_UnknownCopyWithImpl<$Res>
    implements $ParsedPixivLink_UnknownCopyWith<$Res> {
  _$ParsedPixivLink_UnknownCopyWithImpl(this._self, this._then);

  final ParsedPixivLink_Unknown _self;
  final $Res Function(ParsedPixivLink_Unknown) _then;

/// Create a copy of ParsedPixivLink
/// with the given fields replaced by the non-null parameter values.
@pragma('vm:prefer-inline') $Res call({Object? rawUrl = null,}) {
  return _then(ParsedPixivLink_Unknown(
rawUrl: null == rawUrl ? _self.rawUrl : rawUrl // ignore: cast_nullable_to_non_nullable
as String,
  ));
}


}

// dart format on
