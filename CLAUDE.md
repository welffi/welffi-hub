# welffi hub (whub)

A native Windows hub for all of welffi's programs. Rust and egui, like welffi animate. The hub is a launcher: each program has its own repo, and the hub downloads, updates and opens it. The tool list is `TOOLS` in `src\programs.rs`.

The account manager is moving out into its own program, welffi accounts (repo welffi/welffi-accounts, asset `welffi-accounts.exe`). Until that's done, the old C# hub stays in `src\whub` and `tests\Whub.Checks` as the source to port from. Delete it, `whub.sln` and the C# notes below once welffi accounts works.

## Commands

`cargo` may not be on PATH. If so, use `%USERPROFILE%\.cargo\bin\cargo.exe`.

- Build: `cargo build`
- Checks: `cargo test`
- Release: `build.bat`, which makes `dist\welffi hub.exe`. Close the hub first, since a running exe can't be replaced.
- Publish: raise `version` in `Cargo.toml`, run `build.bat`, then make a GitHub release on welffi/welffi-hub with a tag like `v0.2.0`. Upload `dist\welffi hub.exe` named `welffi-hub.exe`. Running hubs see it and offer "Update to v0.2.0". The hub compares the tag with its own `Cargo.toml` version, so the two must match. See `src\self_update.rs`.
- The exe icon is `assets\hub.ico`. `build.rs` adds it with `rc.exe` from the Windows SDK. The window icon is `assets\icon64.rgba`.
- On start and after each install, the hub writes Start menu shortcuts for itself and every program it finds (`src\shortcuts.rs`). It skips this when `WHUB_DATA` is set, so test runs don't move them.
- Old C# hub: `"C:\Program Files\dotnet\dotnet.exe" build src\whub\whub.csproj`

## Testing

welffi does the feel testing: how clicks, drags, animations and layout feel in use. Don't spend time on it.

For UI changes:

1. Build and run the checks.
2. Take one screenshot if the look changed.
3. Run `build.bat` and hand it over. Say what to try and what wasn't tested.

Don't write simulated mouse scripts or test every gesture. A quick try by welffi finds more, faster. If welffi reports a bug, fix it from the report.

Test fully before handing over anything that could lose data or break logins: saving accounts or settings, cookies, import, launching Roblox, the Roblox lock and FastFlags.

Add a check to `tests\Whub.Checks` for new logic that has real branches, like parsing or position math. Move that logic into a static method so it can be tested without a window.

When running the app for a test:

- Set `WHUB_DATA` to a scratch folder so real accounts aren't touched.
- Starting the app still releases the real Roblox `appStorage.json` and holds the Roblox lock, whatever `WHUB_DATA` says.
- Only one hub can run at a time, so a test copy exits straight away if welffi has the hub open.
- Never launch Roblox, or use a saved account, without a reason tied to the current task.

## Where code goes

- `src\whub\Core`: logic with no pages or controls. Talks to Roblox, Windows and files.
- `src\whub\Pages`: one page per tool, plus the small classes only that page uses.
- `src\whub\Theme.xaml`: every color and shared style.
- `src\whub\HubTools.cs`: the tool list. One entry adds the sidebar item and the Home tile.

Page code should handle what's on screen and call into Core. If it's working out how something behaves rather than how it's shown, it belongs in Core. Two Core files use WPF types on purpose: `Portraits.cs` makes images and `GlobalHotkey.cs` needs a window handle. Don't add more without a reason like those.

## Keeping the code clean

- Make the smallest change that fully does the job. Don't add options, settings or layers for things that might come later.
- Use what's already here before writing something new. Check Core and Theme.xaml first.
- Delete code a change makes unused in that same change. Don't leave old versions behind.
- Fix bugs at the cause, once, in the shared code. Check every caller first.
- No comments in code. Use clear names, and split up anything that would need explaining.
- No new NuGet package for something a few lines or .NET can do.
- Keep files to one job. When a file passes about 400 lines, or holds two separate jobs, split it before adding more. `AccountsPage.xaml.cs` is already past that, so move a piece out (servers, or the switch key) the next time it grows.
- If two tools need the same code, move it to Core. Tools don't reach into each other's pages.
- Colors and shared styles go in Theme.xaml, never written directly in a page.

## Adding a tool

1. Put its logic in `Core` and its page in `Pages`.
2. Add one entry to `HubTools.All`.
3. Save its data through `HubData` and `Paths`, so it follows `WHUB_DATA`.

## Safety

- Never print, log or send saved cookies or auth tickets. When checking them, print counts or yes/no only.
- Cookies stay sealed with DPAPI on disk. Only send them to Roblox.
- Nothing in here changes the macro's files in `Downloads\AutoHotKey\welffis macros`. Import only reads them.
- Keep `MacroImport` able to read the macro's files until welffi retires the macro.

## Things that have caught us out

- Theme.xaml must not have an implicit `TextBlock` style. It overrides text inside buttons. Text gets its color and font from the Window.
- A style for a `Button` needs its own `Foreground`, or the text comes out black.
- `.NET` sends no Content-Type on an empty POST, but Roblox needs one and answers 415 without it. `RobloxApi.ContentFor` handles this. When porting from the macro, remember that WinHttp adds headers by itself.
- `TilePitch` in `AccountsPage.xaml.cs` must equal the tile's height plus its margins in the XAML. The list stretches to fill the panel, so don't work out row height from its size.
- The Roblox lock is a Mutex, which belongs to the thread that took it. Keep it on the UI thread.
- Roblox open before the hub starts keeps the lock, and the hub and the macro can't both hold it.

## Style for welffi

- Plain voice in docs and notes: simple sentences, no em dashes.
- UI ideas shouldn't come from Tandem.
- Commit or push only when asked. The repo is welffi/welffi-hub on GitHub. Commits use the noreply email, so no personal address goes public.
- The hub downloads welffi animate from the latest release of welffi/welffi-animate. The release asset must stay named `welffi-animate.exe`, with a tag like `v0.2.0`. See `Core\AnimateUpdates.cs`.
