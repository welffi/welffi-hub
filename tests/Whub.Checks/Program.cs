using Whub.Core;
using Whub.Pages;

var failures = 0;

void Check(string name, bool passed)
{
    Console.WriteLine((passed ? "ok    " : "FAIL  ") + name);
    if (!passed)
        failures++;
}

var account = new AccountRecord { Id = "42", Name = "alice", Tracker = "123456" };

var recent = RobloxApi.ParseRecentGame("""
{"sorts":[
  {"topicId":1,"recommendationList":[{"contentType":"Game","contentId":7}]},
  {"topicId":100000003,"recommendationList":[{"contentType":"Ad","contentId":8},{"contentType":"Game","contentId":9}]}
],"contentMetadata":{"Game":{"7":{"rootPlaceId":70,"name":"Wrong"},"9":{"rootPlaceId":90,"name":"Right"}}}}
""");
Check("recent game comes from the continue sort", recent is { PlaceId: "90", Name: "Right" });

var byTopic = RobloxApi.ParseRecentGame("""
{"sorts":[{"topicId":5,"topic":"Continue Playing","recommendationList":[{"contentType":"Game","contentId":1}]}],
 "contentMetadata":{"Game":{"1":{"rootPlaceId":11,"name":"A"}}}}
""");
Check("recent game falls back to the topic name", byTopic?.PlaceId == "11");
Check("recent game is null when no sort matches", RobloxApi.ParseRecentGame("""{"sorts":[],"contentMetadata":{"Game":{}}}""") == null);

var release = AnimateUpdates.ParseRelease("""{"tag_name":"v0.2.0","assets":[{"name":"notes.txt","browser_download_url":"x"},{"name":"welffi-animate.exe","browser_download_url":"https://example/a.exe"}]}""");
Check("release picks the exe asset", release is { Tag: "v0.2.0", DownloadUrl: "https://example/a.exe" });
Check("release without the exe is null", AnimateUpdates.ParseRelease("""{"tag_name":"v1","assets":[]}""") == null);
Check("release without a tag is null", AnimateUpdates.ParseRelease("""{"message":"Not Found"}""") == null);
Check("newer tag is an update", AnimateUpdates.IsNewer("v0.1.0", "v0.2.0") && AnimateUpdates.IsNewer("v0.9.0", "v0.10.0"));
Check("same or older tag is not an update", !AnimateUpdates.IsNewer("v0.2.0", "v0.2.0") && !AnimateUpdates.IsNewer("v0.3.0", "v0.2.0"));
Check("unknown installed version updates", AnimateUpdates.IsNewer(null, "v0.1.0") && AnimateUpdates.IsNewer("junk", "v0.1.0"));
Check("unreadable latest tag never updates", !AnimateUpdates.IsNewer("v0.1.0", "nightly"));

var storage ="""{"AccountBlob":"sec\"ret","PreviousAccountsList":"{\"a\":1}","Other":"keep","Last":1}""";
var cleared = RobloxClient.ClearSavedAccounts(storage);
Check("client storage drops the account blob", !cleared.Contains("AccountBlob") && !cleared.Contains("sec"));
Check("client storage empties previous accounts", cleared.Contains("\"PreviousAccountsList\":\"{}\""));
Check("client storage keeps other keys", cleared.Contains("\"Other\":\"keep\"") && cleared.Contains("\"Last\":1"));
Check("client storage stays valid json", System.Text.Json.JsonDocument.Parse(cleared) != null);
var trailing = RobloxClient.ClearSavedAccounts("""{"Other":1,"AccountBlob":"x"}""");
Check("client storage handles a trailing blob", trailing == """{"Other":1}""");

var appUri = RobloxApi.LaunchUri(account, "TICKET", "");
Check("app launch has no place url", appUri.StartsWith("roblox-player:1+launchmode:app+gameinfo:TICKET+launchtime:") && !appUri.Contains("placelauncherurl"));
var playUri = RobloxApi.LaunchUri(account, "TICKET", RobloxApi.JoinQuery(account, "90"));
Check("play launch has the place url", playUri.Contains("launchmode:play") && playUri.Contains("PlaceLauncher.ashx%3Frequest%3DRequestGame%26browserTrackerId%3D123456%26placeId%3D90%26isPlayTogetherGame%3Dfalse+launchtime:"));
Check("launch keeps the tracker", playUri.Contains("+browsertrackerid:123456+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp"));
Check("private query", RobloxApi.PrivateQuery(account, new("90", "abc")) == "request%3DRequestPrivateGame%26browserTrackerId%3D123456%26placeId%3D90%26linkCode%3Dabc");
Check("follow query", RobloxApi.FollowQuery("7") == "request%3DRequestFollowUser%26userId%3D7");

Check("empty post still has a content type", RobloxApi.ContentFor(HttpMethod.Post, null)?.Headers.ContentType?.MediaType == "text/plain");
Check("json post is json", RobloxApi.ContentFor(HttpMethod.Post, "{}")?.Headers.ContentType?.MediaType == "application/json");
Check("get has no body", RobloxApi.ContentFor(HttpMethod.Get, null) == null);

const string direct = "https://www.roblox.com/games/123/Some-Game?privateServerLinkCode=abc123";
const string share = "https://www.roblox.com/share?code=abc123&type=Server";
Check("direct link is valid", RobloxApi.IsPrivateLink(direct));
Check("share link is valid", RobloxApi.IsPrivateLink(share));
Check("plain game link is not", !RobloxApi.IsPrivateLink("https://www.roblox.com/games/123/Some-Game"));
Check("direct link parses", RobloxApi.ParseDirectPrivateLink(direct) == new PrivateServerTarget("123", "abc123"));
Check("share link does not parse as direct", RobloxApi.ParseDirectPrivateLink(share) == null);

Check("portrait host accepts rbxcdn", RobloxApi.IsPortraitHost("https://tr.rbxcdn.com/x.png"));
Check("portrait host rejects http", !RobloxApi.IsPortraitHost("http://tr.rbxcdn.com/x.png"));
Check("portrait host rejects lookalike", !RobloxApi.IsPortraitHost("https://rbxcdn.com.evil.test/x.png"));

Check("session cookie is found", BrowserLogin.ParseSessionCookie("""{"id":1,"result":{"cookies":[{"name":"a","value":"1","domain":".roblox.com"},{"name":".ROBLOSECURITY","value":"V","domain":".roblox.com"}]}}""") == "V");
Check("session cookie ignores other domains", BrowserLogin.ParseSessionCookie("""{"id":1,"result":{"cookies":[{"name":".ROBLOSECURITY","value":"V","domain":".evil.test"}]}}""") == null);

var accounts = new List<AccountRecord> { new() { Id = "1", Name = "kept", SealedCookie = "KEEP" } };
var settings = new HubSettings();
var imported = MacroImport.Merge(
    "[1]\nname=dup\ncookie=NEW\n[2]\nname=two\ntracker=222\ncookie=C2\n[3]\nname=three\ntracker=333\ncookie=C3\n[4]\nname=nocookie\n",
    "[accounts]\norder=3,2,1\nprivate_link=https://x\n[private_servers]\nGood=" + direct + "\nBad=https://example.com\n[fastflags]\nenabled=1\n",
    accounts, settings);
Check("import counts new accounts and valid servers", imported == new ImportResult(2, 1));
Check("import keeps saved order", string.Join(",", accounts.Select(a => a.Id)) == "1,3,2");
Check("import does not overwrite an existing login", accounts[0].SealedCookie == "KEEP");
Check("import copies the sealed cookie", accounts[1].SealedCookie == "C3" && accounts[1].Tracker == "333");
Check("import brings settings", settings.FastFlags && settings.LastPrivateLink == "https://x" && settings.Servers.Count == 1);

var sealedValue = CookieSeal.Seal("secret");
Check("seal round trips", CookieSeal.Unseal(sealedValue) == "secret" && !sealedValue.Contains("secret"));

Check("next window wraps", RobloxWindows.NextIndex(2, 3) == 0 && RobloxWindows.NextIndex(0, 3) == 1 && RobloxWindows.NextIndex(-1, 3) == 0);

Check("narrow list still has one column", AccountsPage.ColumnsFor(100) == 1);
Check("default list has three columns", AccountsPage.ColumnsFor(488) == 3);
Check("tile under the pointer", AccountsPage.TileIndexAt(350, 150, 450, 3, 6) == 5);
Check("second row first column", AccountsPage.TileIndexAt(10, 130, 450, 3, 6) == 3);
Check("empty cell in the last row picks the last tile", AccountsPage.TileIndexAt(440, 200, 450, 3, 4) == 3);
Check("pointer outside the grid is clamped", AccountsPage.TileIndexAt(-50, 900, 450, 3, 6) == 3);

var root = Path.Combine(Path.GetTempPath(), "whub_checks_" + Guid.NewGuid());
try
{
    var version = Path.Combine(root, "version-a");
    var settingsFile = Path.Combine(version, "ClientSettings", "ClientAppSettings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
    File.WriteAllText(Path.Combine(version, "RobloxPlayerBeta.exe"), "");
    File.WriteAllText(settingsFile, "{\"mine\":1}");

    FastFlags.Apply(true, root);
    Check("flags on writes the file", File.ReadAllText(settingsFile) == FastFlags.Json);
    Check("flags on backs up the old file", File.ReadAllText(settingsFile + ".welffi.bak") == "{\"mine\":1}");
    FastFlags.Apply(false, root);
    Check("flags off restores the backup", File.ReadAllText(settingsFile) == "{\"mine\":1}" && !File.Exists(settingsFile + ".welffi.bak"));

    File.Delete(settingsFile);
    FastFlags.Apply(true, root);
    FastFlags.Apply(false, root);
    Check("flags off deletes our own file", !File.Exists(settingsFile));
}
finally
{
    Directory.Delete(root, true);
}

if (args.Length == 1)
{
    var dryAccounts = new List<AccountRecord>();
    var drySettings = new HubSettings();
    var configPath = Path.Combine(args[0], "config.ini");
    var dry = MacroImport.Merge(
        File.ReadAllText(Path.Combine(args[0], "accounts.ini")),
        File.Exists(configPath) ? File.ReadAllText(configPath) : "",
        dryAccounts, drySettings);
    var readable = dryAccounts.Count(a =>
    {
        try { return CookieSeal.Unseal(a.SealedCookie).Length > 0; }
        catch (InvalidOperationException) { return false; }
    });
    Console.WriteLine($"macro import dry run: {dry.Accounts} accounts, {readable} logins readable, {dry.Servers} servers");
    Check("every macro login can be read by the hub", readable == dry.Accounts);
}

Console.WriteLine(failures == 0 ? "All checks passed" : $"{failures} failed");
return failures == 0 ? 0 : 1;
