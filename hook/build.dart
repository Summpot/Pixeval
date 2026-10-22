import 'package:uniffi_dart_build/hooks.dart';

Future<void> main(List<String> args) async {
  await build(args, (input, output) async {
    await buildNativeFfi(
      input: input,
      output: output,
      contractPath: 'lib/src/native/pixeval.contract.json',
    );
  });
}
