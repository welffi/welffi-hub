use crate::programs::{Release, is_newer};
use crate::updates::{Latest, ProgramState, Shared, begin_download, fetch_to};
use crate::win;
use eframe::egui;
use std::env;
use std::fs;
use std::path::PathBuf;
use std::process::Command;
use std::thread;

pub const VERSION: &str = env!("CARGO_PKG_VERSION");

pub fn available(state: &ProgramState) -> Option<&Release> {
    match &state.latest {
        Latest::Found(release) if is_newer(Some(VERSION), &release.tag) => Some(release),
        _ => None,
    }
}

pub fn remove_old_copy() {
    if let Ok(exe) = env::current_exe() {
        let _ = fs::remove_file(exe.with_extension("exe.old"));
    }
}

pub fn install(release: Release, state: Shared, ctx: egui::Context) {
    begin_download(&state);
    thread::spawn(move || {
        let error = match replace_running_exe(&release, &state, &ctx) {
            Ok(exe) => {
                win::release_single_instance();
                if Command::new(&exe).spawn().is_ok() {
                    ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                    return;
                }
                "Updated. Open welffi hub again to finish.".to_string()
            }
            Err(error) => error,
        };
        let mut current = state.lock().unwrap();
        current.downloading = false;
        current.error = Some(error);
        ctx.request_repaint();
    });
}

fn replace_running_exe(release: &Release, state: &Shared, ctx: &egui::Context) -> Result<PathBuf, String> {
    let exe = env::current_exe().map_err(|_| "Couldn't find welffi hub's own file.".to_string())?;
    let temp = exe.with_extension("exe.download");
    let old = exe.with_extension("exe.old");
    if !fetch_to(&release.url, &temp, state, ctx) {
        return Err("Couldn't download the update.".into());
    }
    if fs::rename(&exe, &old).is_err() {
        let _ = fs::remove_file(&temp);
        return Err("Couldn't make room for the update.".into());
    }
    if fs::rename(&temp, &exe).is_err() {
        let _ = fs::rename(&old, &exe);
        let _ = fs::remove_file(&temp);
        return Err("Couldn't install the update.".into());
    }
    Ok(exe)
}
