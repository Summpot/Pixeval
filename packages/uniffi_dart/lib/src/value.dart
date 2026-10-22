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

  static bool _equal(Object? first, Object? second) {
    if (identical(first, second)) {
      return true;
    }
    if (first is List && second is List) {
      if (first.length != second.length) {
        return false;
      }
      for (var i = 0; i < first.length; i++) {
        if (!_equal(first[i], second[i])) {
          return false;
        }
      }
      return true;
    }
    if (first is Map && second is Map) {
      if (first.length != second.length) {
        return false;
      }
      for (final key in first.keys) {
        if (!second.containsKey(key) || !_equal(first[key], second[key])) {
          return false;
        }
      }
      return true;
    }
    if (first is Set && second is Set) {
      return first.length == second.length && first.every(second.contains);
    }
    return first == second;
  }

  static int _hash(Object? value) {
    if (value is List) {
      return Object.hashAll(value.map(_hash));
    }
    if (value is Set) {
      return Object.hashAllUnordered(value.map(_hash));
    }
    if (value is Map) {
      return Object.hashAllUnordered(
        value.entries.map(
          (entry) => Object.hash(_hash(entry.key), _hash(entry.value)),
        ),
      );
    }
    return value.hashCode;
  }
}
