use crate::programs::TOOLS;
use crate::win;
use std::env;
use std::os::windows::process::CommandExt;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::thread;

const CREATE_NO_WINDOW: u32 = 0x0800_0000;

pub fn refresh() {
    if env::var_os("WHUB_DATA").is_some() {
        return;
    }
    let Some(appdata) = env::var_os("APPDATA") else {
        return;
    };
    let menu = PathBuf::from(appdata).join(r"Microsoft\Windows\Start Menu\Programs");
    thread::spawn(move || {
        let script = script(&menu, &targets());
        let _ = Command::new("powershell").args(["-NoProfile", "-NonInteractive", "-Command", &script]).creation_flags(CREATE_NO_WINDOW).status();
    });
}

fn targets() -> Vec<(String, PathBuf)> {
    let hub = env::current_exe().ok().map(|exe| (win::TITLE.to_string(), exe));
    let programs = TOOLS.iter().filter_map(|tool| Some((tool.name.to_string(), tool.find()?)));
    hub.into_iter().chain(programs).collect()
}

fn quote(text: &str) -> String {
    format!("'{}'", text.replace('\'', "''"))
}

pub fn script(menu: &Path, targets: &[(String, PathBuf)]) -> String {
    let mut script = String::from("$shell = New-Object -ComObject WScript.Shell");
    for (name, target) in targets {
        let link = menu.join(format!("{name}.lnk"));
        let folder = target.parent().unwrap_or(target);
        script += &format!(
            "; $link = $shell.CreateShortcut({}); $link.TargetPath = {}; $link.WorkingDirectory = {}; $link.IconLocation = {}; $link.Save()",
            quote(&link.display().to_string()),
            quote(&target.display().to_string()),
            quote(&folder.display().to_string()),
            quote(&format!("{},0", target.display())),
        );
    }
    script
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn script_makes_one_shortcut_per_target() {
        let menu = Path::new(r"C:\Menu");
        let targets = [("welffi hub".to_string(), PathBuf::from(r"C:\Apps\welffi hub.exe")), ("welffi animate".to_string(), PathBuf::from(r"C:\Apps\animate\welffi animate.exe"))];
        let script = script(menu, &targets);
        assert_eq!(script.matches(".Save()").count(), 2);
        assert!(script.contains(r"CreateShortcut('C:\Menu\welffi hub.lnk')"));
        assert!(script.contains(r"WorkingDirectory = 'C:\Apps\animate'"));
        assert!(script.contains(r"IconLocation = 'C:\Apps\welffi hub.exe,0'"));
    }

    #[test]
    fn apostrophes_in_paths_stay_quoted() {
        let targets = [("welffi hub".to_string(), PathBuf::from(r"C:\Users\O'Neil\welffi hub.exe"))];
        assert!(script(Path::new(r"C:\Menu"), &targets).contains(r"TargetPath = 'C:\Users\O''Neil\welffi hub.exe'"));
    }
}
