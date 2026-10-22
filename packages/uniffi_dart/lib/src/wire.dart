import 'dart:convert';
import 'dart:typed_data';

/// Dart time values retain microseconds; native sub-microsecond precision is
/// rejected rather than silently rounded. UniFFI timestamps use signed seconds
/// and an unsigned nanosecond magnitude.
DateTime readNativeTimestamp(WireReader reader) {
  final seconds = reader.i64();
  final nanos = reader.u32();
  if (nanos >= 1000000000 || nanos % 1000 != 0) {
    throw const FormatException(
      'Timestamp cannot be represented in Dart microseconds',
    );
  }
  final magnitude = seconds.abs() * 1000000 + nanos ~/ 1000;
  return DateTime.fromMicrosecondsSinceEpoch(
    seconds < 0 ? -magnitude : magnitude,
    isUtc: true,
  );
}

void writeNativeTimestamp(WireWriter writer, DateTime value) {
  final micros = value.microsecondsSinceEpoch;
  if (micros < 0 && micros > -1000000) {
    throw ArgumentError('UniFFI cannot encode a negative subsecond timestamp');
  }
  writer.i64((micros.abs() ~/ 1000000) * (micros < 0 ? -1 : 1));
  writer.u32((micros.abs() % 1000000) * 1000);
}

Duration readNativeDuration(WireReader reader) {
  final seconds = reader.u64();
  final nanos = reader.u32();
  final micros = seconds * BigInt.from(1000000) + BigInt.from(nanos ~/ 1000);
  if (nanos >= 1000000000 ||
      nanos % 1000 != 0 ||
      micros > BigInt.from(0x7fffffffffffffff)) {
    throw const FormatException(
      'Duration cannot be represented in Dart microseconds',
    );
  }
  return Duration(microseconds: micros.toInt());
}

void writeNativeDuration(WireWriter writer, Duration value) {
  if (value.isNegative) {
    throw ArgumentError('Native duration cannot be negative');
  }
  writer.u64(BigInt.from(value.inMicroseconds ~/ 1000000));
  writer.u32((value.inMicroseconds % 1000000) * 1000);
}

/// UniFFI uses big endian wire values; BoltFFI uses little endian values.
final class WireReader {
  WireReader(this.bytes, {this.endian = Endian.big})
    : data = ByteData.sublistView(bytes);

  final Uint8List bytes;
  final ByteData data;
  final Endian endian;
  int position = 0;

  int get remaining => bytes.length - position;

  int take(int count) {
    if (count < 0 || count > remaining) {
      throw FormatException(
        'Truncated wire value at $position: need $count, have $remaining',
      );
    }
    final start = position;
    position += count;
    return start;
  }

  int u8() => data.getUint8(take(1));
  int i8() => data.getInt8(take(1));
  int u16() => data.getUint16(take(2), endian);
  int i16() => data.getInt16(take(2), endian);
  int u32() => data.getUint32(take(4), endian);
  int i32() => data.getInt32(take(4), endian);
  int i64() => data.getInt64(take(8), endian);

  BigInt u64() {
    final start = take(8);
    final low = data.getUint32(
      start + (endian == Endian.little ? 0 : 4),
      endian,
    );
    final high = data.getUint32(
      start + (endian == Endian.little ? 4 : 0),
      endian,
    );
    return (BigInt.from(high) << 32) | BigInt.from(low);
  }

  double f32() => data.getFloat32(take(4), endian);
  double f64() => data.getFloat64(take(8), endian);

  bool boolean() => switch (u8()) {
    0 => false,
    1 => true,
    final value => throw FormatException('Invalid bool $value'),
  };

  int length() {
    final count = u32();
    // Empty records occupy zero bytes. Bound collection allocation separately
    // from byte spans, which are checked by take().
    if (count > 0x1000000) {
      throw FormatException('Excessive collection length $count at $position');
    }
    return count;
  }

  Uint8List raw(int length) {
    final start = take(length);
    return Uint8List.fromList(bytes.sublist(start, start + length));
  }

  Uint8List buffer() => raw(u32());
  String string() => utf8.decode(buffer(), allowMalformed: false);

  T? optional<T>(T Function() read) => switch (u8()) {
    0 => null,
    1 => read(),
    final value => throw FormatException('Invalid option tag $value'),
  };

  List<T> sequence<T>(T Function() read) =>
      List.generate(length(), (_) => read());

  Set<T> set<T>(T Function() read) => sequence(read).toSet();

  Map<K, V> map<K, V>(K Function() readKey, V Function() readValue) {
    final count = length();
    return {for (var i = 0; i < count; i++) readKey(): readValue()};
  }

  void finish() {
    if (remaining != 0) {
      throw FormatException('Trailing $remaining wire bytes');
    }
  }
}

final class WireWriter {
  WireWriter({this.endian = Endian.big});

  final Endian endian;
  final _bytes = BytesBuilder(copy: false);

  void _write(int count, void Function(ByteData) write) {
    final bytes = Uint8List(count);
    write(ByteData.sublistView(bytes));
    _bytes.add(bytes);
  }

  void u8(int value) {
    _range(value, 0, 255);
    _bytes.addByte(value);
  }

  void i8(int value) {
    _range(value, -128, 127);
    _write(1, (byteData) => byteData.setInt8(0, value));
  }

  void u16(int value) {
    _range(value, 0, 65535);
    _write(2, (byteData) => byteData.setUint16(0, value, endian));
  }

  void i16(int value) {
    _range(value, -32768, 32767);
    _write(2, (byteData) => byteData.setInt16(0, value, endian));
  }

  void u32(int value) {
    _range(value, 0, 0xffffffff);
    _write(4, (byteData) => byteData.setUint32(0, value, endian));
  }

  void i32(int value) {
    _range(value, -2147483648, 2147483647);
    _write(4, (byteData) => byteData.setInt32(0, value, endian));
  }

  void i64(int value) =>
      _write(8, (byteData) => byteData.setInt64(0, value, endian));

  void u64(BigInt value) {
    if (value.isNegative || value.bitLength > 64) {
      throw RangeError('Value is not u64: $value');
    }
    final low = (value & BigInt.from(0xffffffff)).toInt();
    final high = (value >> 32).toInt();
    if (endian == Endian.little) {
      u32(low);
      u32(high);
    } else {
      u32(high);
      u32(low);
    }
  }

  void f32(double value) =>
      _write(4, (byteData) => byteData.setFloat32(0, value, endian));

  void f64(double value) =>
      _write(8, (byteData) => byteData.setFloat64(0, value, endian));

  void boolean(bool value) => u8(value ? 1 : 0);
  void raw(List<int> value) => _bytes.add(value);

  void buffer(List<int> value) {
    u32(value.length);
    raw(value);
  }

  void string(String value) => buffer(utf8.encode(value));

  void optional<T>(T? value, void Function(T) write) {
    u8(value == null ? 0 : 1);
    if (value != null) {
      write(value);
    }
  }

  void sequence<T>(Iterable<T> values, void Function(T) write) {
    u32(values.length);
    for (final item in values) {
      write(item);
    }
  }

  void map<K, V>(
    Map<K, V> mapValues,
    void Function(K) writeKey,
    void Function(V) writeValue,
  ) {
    u32(mapValues.length);
    mapValues.forEach((key, value) {
      writeKey(key);
      writeValue(value);
    });
  }

  Uint8List takeBytes() => _bytes.takeBytes();

  static void _range(int value, int min, int max) {
    if (value < min || value > max) {
      throw RangeError.range(value, min, max);
    }
  }
}
