# welffi hub (whub)

A native Windows hub for all of welffi's programs. Rust and egui, like welffi animate. The hub is a launcher: each program has its own repo, and the hub downloads, updates and opens it. The tool list is `TOOLS` in `src\programs.rs`.

## Commands

`cargo` may not be on PATH. If so, use `%USERPROFILE%\.cargo\bin\cargo.exe`.

- Build: `cargo build`
- Checks: `cargo test`
- Release: `build.bat`, which makes `dist\welffi hub.exe`. Close the hub first, since a running exe can't be replaced.
- Publish: raise `version` in `Cargo.toml`, run `build.bat`, then make a GitHub release on welffi/welffi-hub with a tag like `v0.2.0`. Upload `dist\welffi hub.exe` named `welffi-hub.exe`. Running hubs see it and offer "Update to v0.2.0". The hub compares the tag with its own `Cargo.toml` version, so the two must match. See `src\self_update.rs`.
- The exe icon is `assets\hub.ico`. `build.rs` adds it with `rc.exe` from the Windows SDK. The window icon is `assets\icon64.rgba`.
- On start and after each install, the hub writes Start menu shortcuts for itself and every program it finds (`src\shortcuts.rs`). It skips this when `WHUB_DATA` is set, so test runs don't move them.

## Testing

welffi does the feel testing: how clicks, drags, animations and layout feel in use. Don't spend time on it.

For UI changes:

1. Build and run the checks.
2. Take one screenshot if the look changed.
3. Run `build.bat` and hand it over. Say what to try and what wasn't tested.

Don't write simulated mouse scripts or test every gesture. A quick try by welffi finds more, faster. If welffi reports a bug, fix it from the report.

Add a check for new logic that has real branches, like parsing or position math. Keep that logic in a plain function so it can be tested without a window.

When running the hub for a test, set `WHUB_DATA` to a scratch folder. Only one hub can run at a time, so a test copy exits straight away if welffi has the hub open.

## Keeping the code clean

- Make the smallest change that fully does the job. Don't add options, settings or layers for things that might come later.
- Use what's already here before writing something new.
- Delete code a change makes unused in that same change. Don't leave old versions behind.
- Fix bugs at the cause, once, in the shared code. Check every caller first.
- No comments in code. Use clear names, and split up anything that would need explaining.
- Keep files to one job. When a file passes about 400 lines, or holds two separate jobs, split it before adding more.
- Colors go in `src\theme.rs`.

## Programs

- Each program's release asset name and tag style must stay the same, or the hub can't find it. Tags look like `v0.2.0`.
- welffi accounts: latest release of welffi/welffi-accounts, asset `welffi-accounts.exe`.
- welffi animate: latest release of welffi/welffi-animate, asset `welffi-animate.exe`.
- welffi macros: latest release of welffi/welffi-macros, asset `welffi-macros.exe`.

## Style for welffi

- Plain voice in docs and notes: simple sentences, no em dashes.
- UI ideas shouldn't come from Tandem.
- Commit or push only when asked. The repo is welffi/welffi-hub on GitHub. Commits use the noreply email, so no personal address goes public.
