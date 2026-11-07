use std::env;
use std::fs;
use std::path::PathBuf;

pub struct Tool {
    pub name: &'static str,
    pub blurb: &'static str,
    pub icon: char,
    pub repo: &'static str,
}

pub static TOOLS: [Tool; 3] = [
    Tool {
        name: "welffi accounts",
        blurb: "Log in to your Roblox accounts and launch them together.",
        icon: '\u{E716}',
        repo: "welffi/welffi-accounts",
    },
    Tool {
        name: "welffi animate",
        blurb: "Animate Roblox rigs and copy them straight into Studio.",
        icon: '\u{E714}',
        repo: "welffi/welffi-animate",
    },
    Tool {
        name: "welffi macros",
        blurb: "Run the CTD loop, or record your inputs and play them back.",
        icon: '\u{E945}',
        repo: "welffi/welffi-macros",
    },
];

#[derive(Clone, Debug, PartialEq)]
pub struct Release {
    pub tag: String,
    pub url: String,
}

pub fn data_root() -> PathBuf {
    match env::var_os("WHUB_DATA") {
        Some(path) => PathBuf::from(path),
        None => PathBuf::from(env::var_os("LOCALAPPDATA").unwrap_or_default()).join("welffi hub"),
    }
}

pub const HUB_REPO: &str = "welffi/welffi-hub";

pub fn asset_for(repo: &str) -> String {
    format!("{}.exe", repo.rsplit('/').next().unwrap_or(repo))
}

impl Tool {

    fn exe_name(&self) -> String {
        format!("{}.exe", self.name)
    }

    pub fn installed_exe(&self) -> PathBuf {
        data_root().join("programs").join(self.name).join(self.exe_name())
    }

    pub fn version_file(&self) -> PathBuf {
        self.installed_exe().with_extension("version")
    }

    pub fn installed_tag(&self) -> Option<String> {
        fs::read_to_string(self.version_file()).ok().map(|tag| tag.trim().to_string())
    }

    pub fn find(&self) -> Option<PathBuf> {
        let beside_hub = env::current_exe().ok().and_then(|hub| Some(hub.parent()?.join(self.exe_name())));
        let dev_build = env::var_os("USERPROFILE")
            .map(|home| PathBuf::from(home).join("Documents").join("ProgramProjects").join(self.name).join("dist").join(self.exe_name()));
        [beside_hub, dev_build, Some(self.installed_exe())].into_iter().flatten().find(|path| path.is_file())
    }
}

pub fn parse_release(json: &str, asset: &str) -> Option<Release> {
    let root: serde_json::Value = serde_json::from_str(json).ok()?;
    let tag = root.get("tag_name")?.as_str()?;
    let found = root.get("assets")?.as_array()?.iter().find(|entry| entry.get("name").and_then(|name| name.as_str()) == Some(asset))?;
    let url = found.get("browser_download_url")?.as_str()?;
    Some(Release { tag: tag.to_string(), url: url.to_string() })
}

fn version(tag: &str) -> Option<Vec<u64>> {
    let mut parts: Vec<u64> = tag.trim().trim_start_matches('v').split('.').map(|part| part.parse().ok()).collect::<Option<_>>()?;
    while parts.len() > 1 && parts.last() == Some(&0) {
        parts.pop();
    }
    Some(parts)
}

pub fn is_newer(installed: Option<&str>, latest: &str) -> bool {
    let Some(newest) = version(latest) else {
        return false;
    };
    installed.and_then(version).is_none_or(|current| newest > current)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn release_picks_the_exe_asset() {
        let json = r#"{"tag_name":"v0.2.0","assets":[{"name":"notes.txt","browser_download_url":"x"},{"name":"welffi-animate.exe","browser_download_url":"https://example/a.exe"}]}"#;
        assert_eq!(parse_release(json, "welffi-animate.exe"), Some(Release { tag: "v0.2.0".into(), url: "https://example/a.exe".into() }));
    }

    #[test]
    fn release_without_the_exe_or_tag_is_none() {
        assert_eq!(parse_release(r#"{"tag_name":"v1","assets":[]}"#, "welffi-animate.exe"), None);
        assert_eq!(parse_release(r#"{"message":"Not Found"}"#, "welffi-animate.exe"), None);
        assert_eq!(parse_release("not json", "welffi-animate.exe"), None);
    }

    #[test]
    fn asset_comes_from_the_repo_name() {
        assert_eq!(asset_for("welffi/welffi-accounts"), "welffi-accounts.exe");
        assert_eq!(asset_for(HUB_REPO), "welffi-hub.exe");
    }

    #[test]
    fn newer_tag_is_an_update() {
        assert!(is_newer(Some("v0.1.0"), "v0.2.0"));
        assert!(is_newer(Some("v0.9.0"), "v0.10.0"));
        assert!(is_newer(Some("v1.0"), "v1.0.1"));
    }

    #[test]
    fn same_or_older_tag_is_not_an_update() {
        assert!(!is_newer(Some("v0.2.0"), "v0.2.0"));
        assert!(!is_newer(Some("v0.3.0"), "v0.2.0"));
        assert!(!is_newer(Some("v1.0"), "v1.0.0"));
    }

    #[test]
    fn unknown_installed_version_updates() {
        assert!(is_newer(None, "v0.1.0"));
        assert!(is_newer(Some("junk"), "v0.1.0"));
    }

    #[test]
    fn unreadable_latest_tag_never_updates() {
        assert!(!is_newer(Some("v0.1.0"), "nightly"));
        assert!(!is_newer(None, ""));
    }
}
