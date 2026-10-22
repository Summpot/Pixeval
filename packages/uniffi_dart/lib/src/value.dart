/// Generated value types compare structural contents, including typed bytes.
abstract mixin class NativeValue {
  List<Object?> get nativeFields;
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is NativeValue &&
          runtimeType == other.runtimeType &&
          _equal(nativeFields, other.nativeFields);
  @override
  int get hashCode => Object.hash(runtimeType, _hash(nativeFields));
  static bool _equal(Object? a, Object? b) {
    if (identical(a, b)) return true;
    if (a is List && b is List) {
      if (a.length != b.length) return false;
      for (var i = 0; i < a.length; i++) {
        if (!_equal(a[i], b[i])) return false;
      }
      return true;
    }
    if (a is Map && b is Map) {
      if (a.length != b.length) return false;
      for (final k in a.keys) {
        if (!b.containsKey(k) || !_equal(a[k], b[k])) return false;
      }
      return true;
    }
    if (a is Set && b is Set)
      return a.length == b.length && a.every(b.contains);
    return a == b;
  }

  static int _hash(Object? v) {
    if (v is List) return Object.hashAll(v.map(_hash));
    if (v is Set) return Object.hashAllUnordered(v.map(_hash));
    if (v is Map)
      return Object.hashAllUnordered(
        v.entries.map((e) => Object.hash(_hash(e.key), _hash(e.value))),
      );
    return v.hashCode;
  }
}
