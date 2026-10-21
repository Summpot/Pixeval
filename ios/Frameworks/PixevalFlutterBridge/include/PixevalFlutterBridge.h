//
//  PixevalFlutterBridge.h
//  Pixeval
//
//  Umbrella header for Pixeval Rust Bridge FFI bindings on iOS.
//

#ifndef PixevalFlutterBridge_h
#define PixevalFlutterBridge_h

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

// Flutter Rust Bridge dummy export / symbol registration
void dummy_method_to_enforce_bundling(void);

#ifdef __cplusplus
}
#endif

#endif /* PixevalFlutterBridge_h */
