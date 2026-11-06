use std::env;
use std::fs;
use std::path::PathBuf;
use std::process::Command;

fn newest_rc() -> PathBuf {
    let kits = PathBuf::from(r"C:\Program Files (x86)\Windows Kits\10\bin");
    let mut found: Vec<PathBuf> = fs::read_dir(kits).into_iter().flatten().flatten().map(|entry| entry.path().join("x64").join("rc.exe")).filter(|path| path.is_file()).collect();
    found.sort();
    found.pop().unwrap_or_else(|| PathBuf::from("rc.exe"))
}

fn main() {
    println!("cargo:rerun-if-changed=assets/hub.rc");
    println!("cargo:rerun-if-changed=assets/hub.ico");
    let res = PathBuf::from(env::var("OUT_DIR").unwrap()).join("hub.res");
    let status = Command::new(newest_rc()).arg("/nologo").arg("/fo").arg(&res).arg("assets/hub.rc").status().expect("rc.exe from the Windows SDK is needed to add the icon");
    assert!(status.success(), "rc.exe couldn't build the icon");
    println!("cargo:rustc-link-arg-bins={}", res.display());
}
