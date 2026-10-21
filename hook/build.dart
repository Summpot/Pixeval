import 'dart:io';

import 'package:code_assets/code_assets.dart';
import 'package:flutter_rust_bridge_hooks/flutter_rust_bridge_hooks.dart';

import 'src/build_config.dart';

Future<void> main(List<String> args) async {
  await build(args, (input, output) async {
    if (!input.config.buildCodeAssets) return;

    final code = input.config.code;
    await FlutterRustBridgeNativeAssetsBuilder(
      cratePath: 'crates/pixeval_flutter_bridge',
      extraCargoBuildArgs: const ['--locked'],
      extraCargoEnvironmentVariables: {
        'CARGO_PROFILE_RELEASE_LTO': 'thin',
        'CARGO_PROFILE_RELEASE_CODEGEN_UNITS': '1',
        'CARGO_PROFILE_RELEASE_STRIP': 'symbols',
        if (code.targetOS == OS.android)
          ...androidCompilerEnvironment(
            architecture: code.targetArchitecture,
            api: code.android.targetNdkApi,
            compiler: code.cCompiler!.compiler,
            windowsHost: Platform.isWindows,
          ),
      },
    ).run(input: input, output: output);

    output.dependencies.addAll(workspaceConfigurationFiles(input.packageRoot));
  });
}
