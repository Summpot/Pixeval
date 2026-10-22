fn main() {
    println!("cargo:rustc-check-cfg=cfg(boltffi_metadata)");
    println!("cargo:rustc-cfg=boltffi_metadata");
}
