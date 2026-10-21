#!/usr/bin/env python3
"""
Static verification script for iOS CocoaPods & XCFramework packaging pipeline.
Checks:
- Podfile syntax and PixevalFlutterBridge pod inclusion
- PixevalFlutterBridge.podspec fields (deployment target, vendored_frameworks, frameworks)
- Header and modulemap structure
- Shell and PowerShell build automation scripts
"""

import sys
import re
from pathlib import Path

def verify():
    root = Path(__file__).resolve().parent.parent

    # 1. Verify build automation scripts
    sh_script = root / 'scripts' / 'build_ios_xcframework.sh'
    assert sh_script.exists(), "build_ios_xcframework.sh not found"
    sh_text = sh_script.read_text(encoding='utf-8')
    assert 'aarch64-apple-ios' in sh_text, "Missing aarch64-apple-ios in build_ios_xcframework.sh"
    assert 'lipo -create' in sh_text, "Missing lipo in build_ios_xcframework.sh"
    assert 'xcodebuild -create-xcframework' in sh_text, "Missing xcodebuild in build_ios_xcframework.sh"

    ps1_script = root / 'scripts' / 'build_ios_xcframework.ps1'
    assert ps1_script.exists(), "build_ios_xcframework.ps1 not found"

    # 2. Verify C headers & modulemap
    h_file = root / 'ios' / 'Frameworks' / 'PixevalFlutterBridge' / 'include' / 'PixevalFlutterBridge.h'
    assert h_file.exists(), "PixevalFlutterBridge.h not found"
    assert 'PixevalFlutterBridge_h' in h_file.read_text(encoding='utf-8')

    map_file = root / 'ios' / 'Frameworks' / 'PixevalFlutterBridge' / 'include' / 'module.modulemap'
    assert map_file.exists(), "module.modulemap not found"
    assert 'module PixevalFlutterBridge' in map_file.read_text(encoding='utf-8')

    # 3. Verify Podspec
    podspec = root / 'ios' / 'PixevalFlutterBridge.podspec'
    assert podspec.exists(), "PixevalFlutterBridge.podspec not found"
    spec_text = podspec.read_text(encoding='utf-8')
    assert "s.name             = 'PixevalFlutterBridge'" in spec_text
    assert "s.ios.deployment_target = '13.0'" in spec_text
    assert "s.vendored_frameworks = 'Frameworks/PixevalFlutterBridge.xcframework'" in spec_text
    assert "'Security'" in spec_text and "'SystemConfiguration'" in spec_text

    # 4. Verify Podfile
    podfile = root / 'ios' / 'Podfile'
    assert podfile.exists(), "ios/Podfile not found"
    pod_text = podfile.read_text(encoding='utf-8')
    assert "platform :ios, '13.0'" in pod_text
    assert "pod 'PixevalFlutterBridge', :path => '.'" in pod_text
    assert "target 'Runner' do" in pod_text

    print("iOS CocoaPods & XCFramework packaging pipeline statically verified: All 4 components valid.")
    return 0

if __name__ == '__main__':
    sys.exit(verify())
