import 'dart:convert';
import 'dart:typed_data';

/// Offline object reader. Addresses are translated through section tables;
/// arbitrary byte scanning is never used to discover UniFFI metadata.
final class NativeBinary {
  NativeBinary(this.bytes, {this.origin = '<memory>'});
  final Uint8List bytes;
  final String origin;
  final symbols = <String, Uint8List>{};
  final sections = <String, List<Uint8List>>{};
  final exports = <String>{};
  late final ByteData _data = ByteData.sublistView(bytes);
  int u16(int p, [Endian e = Endian.little]) => _data.getUint16(p, e);
  int u32(int p, [Endian e = Endian.little]) => _data.getUint32(p, e);
  int u64(int p, [Endian e = Endian.little]) {
    final n = _data.getUint64(p, e);
    if (n < 0) throw FormatException('$origin: oversized address at $p');
    return n;
  }

  Uint8List slice(int p, int n) {
    if (p < 0 || n < 0 || p > bytes.length - n)
      throw FormatException('$origin: invalid span $p+$n');
    return Uint8List.sublistView(bytes, p, p + n);
  }

  String cstring(int p, [int? max]) {
    if (p < 0 || p >= bytes.length)
      throw FormatException('$origin: invalid string offset $p');
    final limit = max == null ? bytes.length : p + max;
    var end = p;
    while (end < limit && end < bytes.length && bytes[end] != 0) {
      end++;
    }
    if (end == limit && max == null)
      throw FormatException('$origin: unterminated string at $p');
    return utf8.decode(slice(p, end - p), allowMalformed: false);
  }

  void _symbol(String rawName, int offset, int size) {
    final name = rawName.startsWith('_UNIFFI_')
        ? rawName.substring(1)
        : rawName;
    exports.add(name);
    if (!name.startsWith('UNIFFI_META')) return;
    final value = slice(offset, size);
    final previous = symbols[name];
    if (previous != null && !_equal(previous, value)) {
      // PE exports have no size. Metadata decoding checks semantic duplicates.
      symbols[name] = previous.length < value.length ? previous : value;
    } else {
      symbols[name] = value;
    }
  }

  void _section(String name, int p, int n) {
    if (name == '.boltffi' || name == '__boltffi') {
      (sections[name] ??= []).add(slice(p, n));
    }
  }

  NativeBinary parse() {
    try {
      if (bytes.length < 8)
        throw const FormatException('Truncated object header');
      if (ascii.decode(slice(0, 8), allowInvalid: true) == '!<arch>\n') {
        _archive();
      } else if (u32(0) == 0x464c457f) {
        _elf();
      } else if (u16(0) == 0x5a4d) {
        _pe();
      } else if ({
        0xfeedface,
        0xfeedfacf,
        0xcefaedfe,
        0xcffaedfe,
        0xbebafeca,
        0xcafebabe,
        0xbfbafeca,
        0xcafebabf,
      }.contains(u32(0))) {
        _macho();
      } else if ({0x8664, 0x14c, 0xaa64}.contains(u16(0)) ||
          (u16(0) == 0 && u16(2) == 0xffff)) {
        _coff();
      } else {
        throw const FormatException('Unsupported object format');
      }
      return this;
    } on RangeError catch (error) {
      throw FormatException('$origin: truncated object ($error)');
    }
  }

  void _merge(NativeBinary other) {
    exports.addAll(other.exports);
    for (final entry in other.symbols.entries) {
      final previous = symbols[entry.key];
      if (previous != null && !_equal(previous, entry.value))
        throw FormatException('$origin: conflicting metadata ${entry.key}');
      symbols[entry.key] = entry.value;
    }
    for (final entry in other.sections.entries) {
      (sections[entry.key] ??= []).addAll(entry.value);
    }
  }

  void _archive() {
    var p = 8;
    var longNames = '';
    while (p < bytes.length) {
      final header = ascii.decode(slice(p, 60));
      if (header.substring(58) != '`\n')
        throw FormatException('$origin: bad archive header at $p');
      var name = header.substring(0, 16).trim();
      final size = int.parse(header.substring(48, 58).trim());
      var start = p + 60;
      var length = size;
      slice(start, length);
      if (name == '//') {
        longNames = ascii.decode(slice(start, length));
      } else if (name != '/' && name != '/SYM64/') {
        if (name.startsWith('#1/')) {
          final n = int.parse(name.substring(3));
          name = utf8.decode(slice(start, n));
          start += n;
          length -= n;
        } else if (name.startsWith('/') &&
            int.tryParse(name.substring(1)) != null) {
          final n = int.parse(name.substring(1));
          if (n >= longNames.length)
            throw const FormatException('Invalid archive long name');
          name = longNames.substring(n).split('\n').first;
        }
        final member = slice(start, length);
        // Archives contain linker indexes, import records and LLVM bitcode too.
        // Only native object members can contain addressable metadata sections.
        if (member.length >= 20 && (_known(member))) {
          _merge(NativeBinary(member, origin: '$origin($name)').parse());
        }
      }
      p += 60 + size + (size & 1);
    }
  }

  static bool _known(Uint8List b) {
    final d = ByteData.sublistView(b);
    final m = d.getUint32(0, Endian.little);
    return m == 0x464c457f ||
        {0xfeedface, 0xfeedfacf, 0xcefaedfe, 0xcffaedfe}.contains(m) ||
        {0x8664, 0x14c, 0xaa64}.contains(d.getUint16(0, Endian.little)) ||
        (m == 0xffff0000 &&
            b.length >= 56 &&
            d.getUint16(4, Endian.little) == 2);
  }

  void _pe() {
    final pe = u32(0x3c);
    if (u32(pe) != 0x4550) throw const FormatException('Invalid PE signature');
    final count = u16(pe + 6), optional = pe + 24, size = u16(pe + 20);
    final table = optional + size;
    final entries = <({int va, int raw, int size})>[];
    for (var i = 0; i < count; i++) {
      final p = table + i * 40;
      final raw = u32(p + 20), n = u32(p + 16);
      entries.add((va: u32(p + 12), raw: raw, size: n));
      _section(cstring(p, 8), raw, n);
    }
    int address(int rva) {
      for (final s in entries) {
        if (rva >= s.va && rva - s.va < s.size) return s.raw + rva - s.va;
      }
      throw FormatException('Unmapped PE RVA $rva');
    }

    final magic = u16(optional);
    if (magic != 0x20b && magic != 0x10b)
      throw const FormatException('Unsupported PE optional header');
    final expRva = u32(optional + (magic == 0x20b ? 112 : 96));
    if (expRva == 0) return;
    final exp = address(expRva), names = u32(exp + 24);
    final funcs = address(u32(exp + 28)),
        nameTable = address(u32(exp + 32)),
        ords = address(u32(exp + 36));
    for (var i = 0; i < names; i++) {
      final name = cstring(address(u32(nameTable + i * 4)));
      final rva = u32(funcs + u16(ords + i * 2) * 4);
      exports.add(name);
      if (name.startsWith('UNIFFI_META')) {
        final offset = address(rva);
        final section = entries.firstWhere(
          (s) => rva >= s.va && rva - s.va < s.size,
        );
        _symbol(name, offset, section.size - (rva - section.va));
      }
    }
  }

  void _coff() {
    final big = u16(0) == 0 && u16(2) == 0xffff;
    final count = big ? u32(44) : u16(2),
        sym = big ? u32(48) : u32(8),
        symbolsCount = big ? u32(52) : u32(12);
    final entrySize = big ? 20 : 18, sectionBase = big ? 56 : 20 + u16(16);
    final strings = sym + symbolsCount * entrySize;
    final entries = <({int raw, int size})>[];
    for (var i = 0; i < count; i++) {
      final p = sectionBase + i * 40;
      var name = cstring(p, 8);
      if (name.startsWith('/'))
        name = cstring(strings + int.parse(name.substring(1)));
      final raw = u32(p + 20), size = u32(p + 16);
      entries.add((raw: raw, size: size));
      _section(name, raw, size);
    }
    for (var i = 0; i < symbolsCount; i++) {
      final p = sym + i * entrySize;
      final name = u32(p) == 0 ? cstring(strings + u32(p + 4)) : cstring(p, 8);
      final sec = big
          ? _data.getInt32(p + 12, Endian.little)
          : _data.getInt16(p + 12, Endian.little);
      if (sec > 0 && sec <= entries.length) {
        final s = entries[sec - 1], value = u32(p + 8);
        if (value <= s.size) _symbol(name, s.raw + value, s.size - value);
      }
      i += bytes[p + (big ? 19 : 17)];
    }
  }

  void _elf() {
    final wide = bytes[4] == 2;
    if (bytes[4] != 1 && !wide)
      throw const FormatException('Invalid ELF class');
    final e = switch (bytes[5]) {
      1 => Endian.little,
      2 => Endian.big,
      _ => throw const FormatException('Invalid ELF endian'),
    };
    final table = wide ? u64(40, e) : u32(32, e),
        stride = u16(wide ? 58 : 46, e),
        count = u16(wide ? 60 : 48, e),
        names = u16(wide ? 62 : 50, e);
    ({int name, int kind, int addr, int offset, int size, int link, int stride})
    section(int i) {
      final p = table + i * stride;
      return (
        name: u32(p, e),
        kind: u32(p + 4, e),
        addr: wide ? u64(p + 16, e) : u32(p + 12, e),
        offset: wide ? u64(p + 24, e) : u32(p + 16, e),
        size: wide ? u64(p + 32, e) : u32(p + 20, e),
        link: u32(p + (wide ? 40 : 24), e),
        stride: wide ? u64(p + 56, e) : u32(p + 36, e),
      );
    }

    final nameSection = section(names);
    for (var i = 0; i < count; i++) {
      final s = section(i);
      _section(cstring(nameSection.offset + s.name), s.offset, s.size);
      if (s.kind != 2 && s.kind != 11) continue;
      if (s.stride < (wide ? 24 : 16) || s.link >= count)
        throw const FormatException('Invalid ELF symbol table');
      final strings = section(s.link);
      for (var p = s.offset; p < s.offset + s.size; p += s.stride) {
        final name = cstring(strings.offset + u32(p, e));
        final index = u16(p + (wide ? 6 : 14), e);
        if (index == 0 || index >= count) continue;
        final target = section(index),
            value = wide ? u64(p + 8, e) : u32(p + 4, e),
            size = wide ? u64(p + 16, e) : u32(p + 8, e);
        final relative = value - target.addr;
        if (relative >= 0 && relative <= target.size)
          _symbol(
            name,
            target.offset + relative,
            size == 0 ? target.size - relative : size,
          );
      }
    }
  }

  void _macho() {
    final magic = u32(0);
    if ({0xbebafeca, 0xcafebabe, 0xbfbafeca, 0xcafebabf}.contains(magic)) {
      final e = (magic == 0xbebafeca || magic == 0xbfbafeca)
          ? Endian.big
          : Endian.little;
      final wide = magic == 0xbfbafeca || magic == 0xcafebabf;
      final count = u32(4, e);
      if (count == 0)
        throw const FormatException('Empty Mach-O architecture table');
      {
        const i = 0, p = 8;
        final offset = wide ? u64(p + 8, e) : u32(p + 8, e),
            size = wide ? u64(p + 16, e) : u32(p + 12, e);
        // Different slices legitimately have different addresses and layouts.
        // Read one architecture; callers validate target contracts separately.
        _merge(
          NativeBinary(
            slice(offset, size),
            origin: '$origin[slice $i]',
          ).parse(),
        );
      }
      return;
    }
    final wide = magic == 0xfeedfacf || magic == 0xcffaedfe;
    final e = (magic == 0xfeedface || magic == 0xfeedfacf)
        ? Endian.little
        : Endian.big;
    final count = u32(16, e);
    var p = wide ? 32 : 28;
    final entries = <({int addr, int raw, int size})>[];
    int? sym, strings;
    var nsyms = 0;
    for (var i = 0; i < count; i++) {
      final command = u32(p, e), length = u32(p + 4, e);
      if (length < 8) throw const FormatException('Invalid Mach-O command');
      slice(p, length);
      if (command == 2) {
        sym = u32(p + 8, e);
        nsyms = u32(p + 12, e);
        strings = u32(p + 16, e);
      }
      if (command == (wide ? 0x19 : 1)) {
        final sectionsCount = u32(p + (wide ? 64 : 48), e);
        for (var j = 0; j < sectionsCount; j++) {
          final s = p + (wide ? 72 : 56) + j * (wide ? 80 : 68);
          final addr = wide ? u64(s + 32, e) : u32(s + 32, e),
              size = wide ? u64(s + 40, e) : u32(s + 36, e),
              raw = u32(s + (wide ? 48 : 40), e);
          entries.add((addr: addr, raw: raw, size: size));
          _section(cstring(s, 16), raw, size);
        }
      }
      p += length;
    }
    if (sym == null || strings == null) return;
    for (var i = 0; i < nsyms; i++) {
      final p = sym + i * (wide ? 16 : 12), section = bytes[p + 5];
      if (section == 0 || section > entries.length) continue;
      final s = entries[section - 1],
          value = wide ? u64(p + 8, e) : u32(p + 8, e),
          relative = value - s.addr;
      if (relative >= 0 && relative < s.size)
        _symbol(
          cstring(strings + u32(p, e)),
          s.raw + relative,
          s.size - relative,
        );
    }
  }

  static bool _equal(Uint8List a, Uint8List b) {
    if (a.length != b.length) return false;
    for (var i = 0; i < a.length; i++) {
      if (a[i] != b[i]) return false;
    }
    return true;
  }
}
