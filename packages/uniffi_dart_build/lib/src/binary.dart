import 'dart:convert';
import 'dart:typed_data';

/// Magic numbers and binary header identifiers.
abstract final class BinaryMagic {
  static const arArchiveHeader = '!<arch>\n';

  // ELF
  static const elfSignature = 0x464c457f; // '\x7fELF' in little-endian

  // PE / COFF
  static const dosSignature = 0x5a4d; // 'MZ'
  static const ntSignature = 0x4550; // 'PE\0\0'
  static const pe32Magic = 0x10b;
  static const pe32PlusMagic = 0x20b;
  static const coffMachineX64 = 0x8664;
  static const coffMachineI386 = 0x14c;
  static const coffMachineArm64 = 0xaa64;

  // Mach-O
  static const macho32 = 0xfeedface;
  static const macho64 = 0xfeedfacf;
  static const macho32Swapped = 0xcefaedfe;
  static const macho64Swapped = 0xcffaedfe;
  static const machoFat = 0xcafebabe;
  static const machoFatSwapped = 0xbebafeca;
  static const machoFat64 = 0xcafebabf;
  static const machoFat64Swapped = 0xbfbafeca;
}

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

  int u16(int offset, [Endian endian = Endian.little]) =>
      _data.getUint16(offset, endian);

  int u32(int offset, [Endian endian = Endian.little]) =>
      _data.getUint32(offset, endian);

  int u64(int offset, [Endian endian = Endian.little]) {
    final value = _data.getUint64(offset, endian);
    if (value < 0) {
      throw FormatException('$origin: oversized address at $offset');
    }
    return value;
  }

  Uint8List slice(int offset, int length) {
    if (offset < 0 || length < 0 || offset > bytes.length - length) {
      throw FormatException('$origin: invalid span $offset+$length');
    }
    return Uint8List.sublistView(bytes, offset, offset + length);
  }

  String cstring(int offset, [int? maxLength]) {
    if (offset < 0 || offset >= bytes.length) {
      throw FormatException('$origin: invalid string offset $offset');
    }
    final limit = maxLength == null ? bytes.length : offset + maxLength;
    var end = offset;
    while (end < limit && end < bytes.length && bytes[end] != 0) {
      end++;
    }
    if (end == limit && maxLength == null) {
      throw FormatException('$origin: unterminated string at $offset');
    }
    return utf8.decode(slice(offset, end - offset), allowMalformed: false);
  }

  void _symbol(String rawName, int offset, int size) {
    final name = rawName.startsWith('_UNIFFI_')
        ? rawName.substring(1)
        : rawName;
    exports.add(name);
    if (!name.startsWith('UNIFFI_META')) {
      return;
    }
    final value = slice(offset, size);
    final previous = symbols[name];
    if (previous != null && !_equal(previous, value)) {
      // PE exports have no size. Metadata decoding checks semantic duplicates.
      symbols[name] = previous.length < value.length ? previous : value;
    } else {
      symbols[name] = value;
    }
  }

  void _section(String name, int offset, int size) {
    if (name == '.boltffi' || name == '__boltffi') {
      (sections[name] ??= []).add(slice(offset, size));
    }
  }

  NativeBinary parse() {
    try {
      if (bytes.length < 8) {
        throw const FormatException('Truncated object header');
      }
      if (ascii.decode(slice(0, 8), allowInvalid: true) ==
          BinaryMagic.arArchiveHeader) {
        _archive();
      } else if (u32(0) == BinaryMagic.elfSignature) {
        _elf();
      } else if (u16(0) == BinaryMagic.dosSignature) {
        _pe();
      } else if ({
        BinaryMagic.macho32,
        BinaryMagic.macho64,
        BinaryMagic.macho32Swapped,
        BinaryMagic.macho64Swapped,
        BinaryMagic.machoFat,
        BinaryMagic.machoFatSwapped,
        BinaryMagic.machoFat64,
        BinaryMagic.machoFat64Swapped,
      }.contains(u32(0))) {
        _macho();
      } else if ({
            BinaryMagic.coffMachineX64,
            BinaryMagic.coffMachineI386,
            BinaryMagic.coffMachineArm64,
          }.contains(u16(0)) ||
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
      if (previous != null && !_equal(previous, entry.value)) {
        throw FormatException('$origin: conflicting metadata ${entry.key}');
      }
      symbols[entry.key] = entry.value;
    }
    for (final entry in other.sections.entries) {
      (sections[entry.key] ??= []).addAll(entry.value);
    }
  }

  void _archive() {
    var archiveOffset = 8;
    var longNames = '';
    while (archiveOffset < bytes.length) {
      final header = ascii.decode(slice(archiveOffset, 60));
      if (header.substring(58) != '`\n') {
        throw FormatException(
          '$origin: bad archive header at $archiveOffset',
        );
      }
      var name = header.substring(0, 16).trim();
      final size = int.parse(header.substring(48, 58).trim());
      var start = archiveOffset + 60;
      var length = size;
      slice(start, length);
      if (name == '//') {
        longNames = ascii.decode(slice(start, length));
      } else if (name != '/' && name != '/SYM64/') {
        if (name.startsWith('#1/')) {
          final count = int.parse(name.substring(3));
          name = utf8.decode(slice(start, count));
          start += count;
          length -= count;
        } else if (name.startsWith('/') &&
            int.tryParse(name.substring(1)) != null) {
          final longNameOffset = int.parse(name.substring(1));
          if (longNameOffset >= longNames.length) {
            throw const FormatException('Invalid archive long name');
          }
          name = longNames.substring(longNameOffset).split('\n').first;
        }
        final member = slice(start, length);
        // Archives contain linker indexes, import records and LLVM bitcode too.
        // Only native object members can contain addressable metadata sections.
        if (member.length >= 20 && _isKnownObject(member)) {
          _merge(NativeBinary(member, origin: '$origin($name)').parse());
        }
      }
      archiveOffset += 60 + size + (size & 1);
    }
  }

  static bool _isKnownObject(Uint8List objectBytes) {
    final byteData = ByteData.sublistView(objectBytes);
    final magic = byteData.getUint32(0, Endian.little);
    return magic == BinaryMagic.elfSignature ||
        {
          BinaryMagic.macho32,
          BinaryMagic.macho64,
          BinaryMagic.macho32Swapped,
          BinaryMagic.macho64Swapped,
        }.contains(magic) ||
        {
          BinaryMagic.coffMachineX64,
          BinaryMagic.coffMachineI386,
          BinaryMagic.coffMachineArm64,
        }.contains(byteData.getUint16(0, Endian.little)) ||
        (magic == 0xffff0000 &&
            objectBytes.length >= 56 &&
            byteData.getUint16(4, Endian.little) == 2);
  }

  void _pe() {
    final peHeaderOffset = u32(0x3c);
    if (u32(peHeaderOffset) != BinaryMagic.ntSignature) {
      throw const FormatException('Invalid PE signature');
    }
    final numberOfSections = u16(peHeaderOffset + 6);
    final optionalHeaderOffset = peHeaderOffset + 24;
    final sizeOfOptionalHeader = u16(peHeaderOffset + 20);
    final sectionTableOffset = optionalHeaderOffset + sizeOfOptionalHeader;

    final entries = <({int va, int raw, int size})>[];
    for (var i = 0; i < numberOfSections; i++) {
      final entryOffset = sectionTableOffset + i * 40;
      final rawAddress = u32(entryOffset + 20);
      final rawSize = u32(entryOffset + 16);
      entries.add((
        va: u32(entryOffset + 12),
        raw: rawAddress,
        size: rawSize,
      ));
      _section(cstring(entryOffset, 8), rawAddress, rawSize);
    }

    int address(int rva) {
      for (final section in entries) {
        if (rva >= section.va && rva - section.va < section.size) {
          return section.raw + rva - section.va;
        }
      }
      throw FormatException('Unmapped PE RVA $rva');
    }

    final magic = u16(optionalHeaderOffset);
    if (magic != BinaryMagic.pe32PlusMagic && magic != BinaryMagic.pe32Magic) {
      throw const FormatException('Unsupported PE optional header');
    }
    final exportDirectoryRva = u32(
      optionalHeaderOffset +
          (magic == BinaryMagic.pe32PlusMagic ? 112 : 96),
    );
    if (exportDirectoryRva == 0) {
      return;
    }
    final exportDirectoryOffset = address(exportDirectoryRva);
    final numberOfNames = u32(exportDirectoryOffset + 24);
    final addressOfFunctions = address(u32(exportDirectoryOffset + 28));
    final addressOfNames = address(u32(exportDirectoryOffset + 32));
    final addressOfNameOrdinals = address(u32(exportDirectoryOffset + 36));

    for (var i = 0; i < numberOfNames; i++) {
      final name = cstring(address(u32(addressOfNames + i * 4)));
      final ordinal = u16(addressOfNameOrdinals + i * 2);
      final functionRva = u32(addressOfFunctions + ordinal * 4);
      exports.add(name);
      if (name.startsWith('UNIFFI_META')) {
        final symbolOffset = address(functionRva);
        final section = entries.firstWhere(
          (entry) =>
              functionRva >= entry.va && functionRva - entry.va < entry.size,
        );
        _symbol(
          name,
          symbolOffset,
          section.size - (functionRva - section.va),
        );
      }
    }
  }

  void _coff() {
    final isBigCoff = u16(0) == 0 && u16(2) == 0xffff;
    final numberOfSections = isBigCoff ? u32(44) : u16(2);
    final symbolTableOffset = isBigCoff ? u32(48) : u32(8);
    final symbolCount = isBigCoff ? u32(52) : u32(12);
    final entrySize = isBigCoff ? 20 : 18;
    final sectionBase = isBigCoff ? 56 : 20 + u16(16);
    final stringTableOffset = symbolTableOffset + symbolCount * entrySize;

    final entries = <({int raw, int size})>[];
    for (var i = 0; i < numberOfSections; i++) {
      final headerOffset = sectionBase + i * 40;
      var name = cstring(headerOffset, 8);
      if (name.startsWith('/')) {
        name = cstring(
          stringTableOffset + int.parse(name.substring(1)),
        );
      }
      final rawAddress = u32(headerOffset + 20);
      final rawSize = u32(headerOffset + 16);
      entries.add((raw: rawAddress, size: rawSize));
      _section(name, rawAddress, rawSize);
    }

    for (var i = 0; i < symbolCount; i++) {
      final entryOffset = symbolTableOffset + i * entrySize;
      final name = u32(entryOffset) == 0
          ? cstring(stringTableOffset + u32(entryOffset + 4))
          : cstring(entryOffset, 8);
      final sectionIndex = isBigCoff
          ? _data.getInt32(entryOffset + 12, Endian.little)
          : _data.getInt16(entryOffset + 12, Endian.little);
      if (sectionIndex > 0 && sectionIndex <= entries.length) {
        final section = entries[sectionIndex - 1];
        final value = u32(entryOffset + 8);
        if (value <= section.size) {
          _symbol(name, section.raw + value, section.size - value);
        }
      }
      i += bytes[entryOffset + (isBigCoff ? 19 : 17)];
    }
  }

  void _elf() {
    final is64Bit = bytes[4] == 2;
    if (bytes[4] != 1 && !is64Bit) {
      throw const FormatException('Invalid ELF class');
    }
    final endian = switch (bytes[5]) {
      1 => Endian.little,
      2 => Endian.big,
      _ => throw const FormatException('Invalid ELF endian'),
    };
    final sectionHeaderOffset =
        is64Bit ? u64(40, endian) : u32(32, endian);
    final sectionHeaderEntrySize =
        u16(is64Bit ? 58 : 46, endian);
    final sectionHeaderCount =
        u16(is64Bit ? 60 : 48, endian);
    final shstrndx = u16(is64Bit ? 62 : 50, endian);

    ({int name, int kind, int addr, int offset, int size, int link, int stride})
    readSection(int index) {
      final offset = sectionHeaderOffset + index * sectionHeaderEntrySize;
      return (
        name: u32(offset, endian),
        kind: u32(offset + 4, endian),
        addr: is64Bit ? u64(offset + 16, endian) : u32(offset + 12, endian),
        offset: is64Bit ? u64(offset + 24, endian) : u32(offset + 16, endian),
        size: is64Bit ? u64(offset + 32, endian) : u32(offset + 20, endian),
        link: u32(offset + (is64Bit ? 40 : 24), endian),
        stride: is64Bit ? u64(offset + 56, endian) : u32(offset + 36, endian),
      );
    }

    final nameSection = readSection(shstrndx);
    for (var i = 0; i < sectionHeaderCount; i++) {
      final section = readSection(i);
      _section(
        cstring(nameSection.offset + section.name),
        section.offset,
        section.size,
      );
      if (section.kind != 2 && section.kind != 11) {
        continue;
      }
      if (section.stride < (is64Bit ? 24 : 16) ||
          section.link >= sectionHeaderCount) {
        throw const FormatException('Invalid ELF symbol table');
      }
      final strings = readSection(section.link);
      for (
        var entryOffset = section.offset;
        entryOffset < section.offset + section.size;
        entryOffset += section.stride
      ) {
        final name = cstring(strings.offset + u32(entryOffset, endian));
        final symbolSectionIndex =
            u16(entryOffset + (is64Bit ? 6 : 14), endian);
        if (symbolSectionIndex == 0 ||
            symbolSectionIndex >= sectionHeaderCount) {
          continue;
        }
        final target = readSection(symbolSectionIndex);
        final value = is64Bit
            ? u64(entryOffset + 8, endian)
            : u32(entryOffset + 4, endian);
        final size = is64Bit
            ? u64(entryOffset + 16, endian)
            : u32(entryOffset + 8, endian);
        final relative = value - target.addr;
        if (relative >= 0 && relative <= target.size) {
          _symbol(
            name,
            target.offset + relative,
            size == 0 ? target.size - relative : size,
          );
        }
      }
    }
  }

  void _macho() {
    final magic = u32(0);
    if ({
      BinaryMagic.machoFat,
      BinaryMagic.machoFatSwapped,
      BinaryMagic.machoFat64,
      BinaryMagic.machoFat64Swapped,
    }.contains(magic)) {
      final endian = (magic == BinaryMagic.machoFatSwapped ||
              magic == BinaryMagic.machoFat64Swapped)
          ? Endian.big
          : Endian.little;
      final is64Bit = magic == BinaryMagic.machoFat64Swapped ||
          magic == BinaryMagic.machoFat64;
      final architectureCount = u32(4, endian);
      if (architectureCount == 0) {
        throw const FormatException('Empty Mach-O architecture table');
      }
      {
        const sliceIndex = 0;
        const sliceHeaderOffset = 8;
        final offset = is64Bit
            ? u64(sliceHeaderOffset + 8, endian)
            : u32(sliceHeaderOffset + 8, endian);
        final size = is64Bit
            ? u64(sliceHeaderOffset + 16, endian)
            : u32(sliceHeaderOffset + 12, endian);
        // Different slices legitimately have different addresses and layouts.
        // Read one architecture; callers validate target contracts separately.
        _merge(
          NativeBinary(
            slice(offset, size),
            origin: '$origin[slice $sliceIndex]',
          ).parse(),
        );
      }
      return;
    }

    final is64Bit = magic == BinaryMagic.macho64 ||
        magic == BinaryMagic.macho64Swapped;
    final endian = (magic == BinaryMagic.macho32 ||
            magic == BinaryMagic.macho64)
        ? Endian.little
        : Endian.big;
    final commandCount = u32(16, endian);
    var commandOffset = is64Bit ? 32 : 28;
    final entries = <({int addr, int raw, int size})>[];
    int? symbolTableOffset;
    int? stringTableOffset;
    var numberOfSymbols = 0;

    for (var i = 0; i < commandCount; i++) {
      final command = u32(commandOffset, endian);
      final commandLength = u32(commandOffset + 4, endian);
      if (commandLength < 8) {
        throw const FormatException('Invalid Mach-O command');
      }
      slice(commandOffset, commandLength);
      if (command == 2) {
        // LC_SYMTAB
        symbolTableOffset = u32(commandOffset + 8, endian);
        numberOfSymbols = u32(commandOffset + 12, endian);
        stringTableOffset = u32(commandOffset + 16, endian);
      }
      if (command == (is64Bit ? 0x19 : 1)) {
        // LC_SEGMENT_64 or LC_SEGMENT
        final sectionCount = u32(commandOffset + (is64Bit ? 64 : 48), endian);
        for (var j = 0; j < sectionCount; j++) {
          final sectionOffset = commandOffset +
              (is64Bit ? 72 : 56) +
              j * (is64Bit ? 80 : 68);
          final address = is64Bit
              ? u64(sectionOffset + 32, endian)
              : u32(sectionOffset + 32, endian);
          final size = is64Bit
              ? u64(sectionOffset + 40, endian)
              : u32(sectionOffset + 36, endian);
          final raw = u32(sectionOffset + (is64Bit ? 48 : 40), endian);
          entries.add((addr: address, raw: raw, size: size));
          _section(cstring(sectionOffset, 16), raw, size);
        }
      }
      commandOffset += commandLength;
    }

    if (symbolTableOffset == null || stringTableOffset == null) {
      return;
    }
    for (var i = 0; i < numberOfSymbols; i++) {
      final entryOffset =
          symbolTableOffset + i * (is64Bit ? 16 : 12);
      final section = bytes[entryOffset + 5];
      if (section == 0 || section > entries.length) {
        continue;
      }
      final targetSection = entries[section - 1];
      final value = is64Bit
          ? u64(entryOffset + 8, endian)
          : u32(entryOffset + 8, endian);
      final relative = value - targetSection.addr;
      if (relative >= 0 && relative < targetSection.size) {
        _symbol(
          cstring(stringTableOffset + u32(entryOffset, endian)),
          targetSection.raw + relative,
          targetSection.size - relative,
        );
      }
    }
  }

  static bool _equal(Uint8List first, Uint8List second) {
    if (first.length != second.length) {
      return false;
    }
    for (var i = 0; i < first.length; i++) {
      if (first[i] != second[i]) {
        return false;
      }
    }
    return true;
  }
}
