import 'dart:convert';
import 'dart:ffi';
import 'dart:io';

import '../test/support/callback_probe.dart';

Future<void> main(List<String> args) async {
  if (args.length != 1) {
    stderr.writeln(
      'Usage: dart tool/probe_callbacks.dart <native fixture path>',
    );
    exitCode = 64;
    return;
  }
  try {
    final passed = await runCallbackProbe(DynamicLibrary.open(args.single));
    stdout.writeln(
      jsonEncode({
        'sdk': Platform.version,
        'os': Platform.operatingSystem,
        'executableArguments': Platform.executableArguments,
        'passed': passed,
      }),
    );
  } catch (error, trace) {
    stderr.writeln(error);
    stderr.writeln(trace);
    exitCode = 1;
  }
}
