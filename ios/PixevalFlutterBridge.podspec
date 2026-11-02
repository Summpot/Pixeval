#
# CocoaPods Podspec for PixevalFlutterBridge
# Links the compiled Rust XCFramework into iOS Runner target
#

Pod::Spec.new do |s|
  s.name             = 'PixevalFlutterBridge'
  s.version          = '0.1.0'
  s.summary          = 'Pixeval Rust Core Engine Flutter Bridge for iOS'
  s.description      = <<-DESC
Encapsulates Pixeval modular Rust core crates (Mako, Maho, Cache, Storage, Filters, Media)
for Flutter iOS via flutter_rust_bridge v2.
                       DESC
  s.homepage         = 'https://github.com/Pixeval/Pixeval'
  s.license          = { :type => 'GPL-3.0' }
  s.author           = { 'Pixeval Team' => 'dev@pixeval.org' }
  s.source           = { :path => '.' }

  s.ios.deployment_target = '13.0'

  s.vendored_frameworks = 'Frameworks/PixevalFlutterBridge.xcframework'
  s.public_header_files = 'Frameworks/PixevalFlutterBridge/include/*.h'
  s.source_files        = 'Frameworks/PixevalFlutterBridge/include/*.h'

  s.frameworks = 'Security', 'SystemConfiguration', 'Network'
  s.libraries  = 'resolv', 'c++'

  s.pod_target_xcconfig = {
    'DEFINES_MODULE' => 'YES',
    'EXCLUDED_ARCHS[sdk=iphonesimulator*]' => 'i386'
  }
end
