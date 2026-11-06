use crate::programs::{Release, Tool, asset_for, is_newer, parse_release};
use crate::shortcuts;
use eframe::egui;
use rfd::{MessageButtons, MessageDialog, MessageDialogResult, MessageLevel};
use std::fs::{self, File};
use std::io::{self, Read, Write};
use std::path::Path;
use std::process::Command;
use std::sync::{Arc, Mutex};
use std::thread;
use std::time::Duration;

#[derive(Clone, Default)]
pub enum Latest {
    #[default]
    Checking,
    Found(Release),
    NotReleased,
    Offline,
}

#[derive(Default)]
pub struct ProgramState {
    pub latest: Latest,
    pub downloading: bool,
    pub progress: Option<f32>,
    pub error: Option<String>,
}

pub type Shared = Arc<Mutex<ProgramState>>;

fn get(url: &str) -> Result<ureq::http::Response<ureq::Body>, ureq::Error> {
    let agent: ureq::Agent = ureq::Agent::config_builder().timeout_global(Some(Duration::from_secs(120))).build().into();
    agent.get(url).header("User-Agent", "welffi-hub").call()
}

fn fetch_latest(repo: &str) -> Latest {
    let url = format!("https://api.github.com/repos/{repo}/releases/latest");
    match get(&url) {
        Ok(mut response) => match response.body_mut().read_to_string() {
            Ok(json) => parse_release(&json, &asset_for(repo)).map_or(Latest::NotReleased, Latest::Found),
            Err(_) => Latest::Offline,
        },
        Err(ureq::Error::StatusCode(404)) => Latest::NotReleased,
        Err(_) => Latest::Offline,
    }
}

pub fn check(repo: &'static str, state: Shared, ctx: egui::Context) {
    {
        let mut current = state.lock().unwrap();
        current.latest = Latest::Checking;
        current.error = None;
    }
    thread::spawn(move || {
        let latest = fetch_latest(repo);
        state.lock().unwrap().latest = latest;
        ctx.request_repaint();
    });
}

pub fn pending_update<'a>(tool: &Tool, latest: &'a Latest, path: &Path) -> Option<&'a Release> {
    match latest {
        Latest::Found(release) if path == tool.installed_exe() && is_newer(tool.installed_tag().as_deref(), &release.tag) => Some(release),
        _ => None,
    }
}

pub fn open(tool: &'static Tool, state: Shared, ctx: egui::Context) {
    let latest = {
        let mut current = state.lock().unwrap();
        current.error = None;
        current.latest.clone()
    };
    let Some(path) = tool.find() else {
        if let Latest::Found(release) = latest {
            install_and_open(tool, release, state, ctx);
        }
        return;
    };
    if let Some(release) = pending_update(tool, &latest, &path) {
        match ask_to_update(tool, &release.tag) {
            MessageDialogResult::Yes => return install_and_open(tool, release.clone(), state, ctx),
            MessageDialogResult::No => {}
            _ => return,
        }
    }
    state.lock().unwrap().error = launch(tool, &path).err();
}

fn ask_to_update(tool: &Tool, tag: &str) -> MessageDialogResult {
    MessageDialog::new()
        .set_title("welffi hub")
        .set_description(format!("{} {} is out. Update before opening?", tool.name, tag))
        .set_level(MessageLevel::Info)
        .set_buttons(MessageButtons::YesNoCancel)
        .show()
}

fn install_and_open(tool: &'static Tool, release: Release, state: Shared, ctx: egui::Context) {
    begin_download(&state);
    thread::spawn(move || {
        let result = download(tool, &release, &state, &ctx).inspect(|()| shortcuts::refresh()).and_then(|()| launch(tool, &tool.installed_exe()));
        let mut current = state.lock().unwrap();
        current.downloading = false;
        current.progress = None;
        current.error = result.err();
        ctx.request_repaint();
    });
}

pub fn begin_download(state: &Shared) {
    let mut current = state.lock().unwrap();
    current.downloading = true;
    current.progress = None;
    current.error = None;
}

pub fn fetch_to(url: &str, temp: &Path, state: &Shared, ctx: &egui::Context) -> bool {
    let saved = get(url).is_ok_and(|mut response| save_body(&mut response, temp, state, ctx).is_ok());
    if !saved {
        let _ = fs::remove_file(temp);
    }
    saved
}

fn save_body(response: &mut ureq::http::Response<ureq::Body>, temp: &Path, state: &Shared, ctx: &egui::Context) -> io::Result<()> {
    let total = response.body().content_length().filter(|&total| total > 0);
    let mut reader = response.body_mut().as_reader();
    let mut file = File::create(temp)?;
    let mut buffer = vec![0; 1 << 16];
    let mut done = 0u64;
    loop {
        let read = reader.read(&mut buffer)?;
        if read == 0 {
            return Ok(());
        }
        file.write_all(&buffer[..read])?;
        done += read as u64;
        if let Some(total) = total {
            state.lock().unwrap().progress = Some(done as f32 / total as f32);
            ctx.request_repaint();
        }
    }
}

fn download(tool: &Tool, release: &Release, state: &Shared, ctx: &egui::Context) -> Result<(), String> {
    let failed = |_| format!("Couldn't download {}.", tool.name);
    let exe = tool.installed_exe();
    fs::create_dir_all(exe.parent().unwrap()).map_err(failed)?;
    let temp = exe.with_extension("exe.download");
    if !fetch_to(&release.url, &temp, state, ctx) {
        return Err(format!("Couldn't download {}.", tool.name));
    }
    if fs::rename(&temp, &exe).is_err() {
        let _ = fs::remove_file(&temp);
        return Err(format!("Close {} first, then try again.", tool.name));
    }
    fs::write(tool.version_file(), &release.tag).map_err(failed)
}

fn launch(tool: &Tool, path: &Path) -> Result<(), String> {
    let folder = path.parent().unwrap_or(Path::new("."));
    Command::new(path).current_dir(folder).spawn().map(|_| ()).map_err(|_| format!("Couldn't open {}.", tool.name))
}
