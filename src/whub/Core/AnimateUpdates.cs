using System.Text.Json;

namespace Whub.Core;

public sealed record ProgramRelease(string Tag, string DownloadUrl);

public static class AnimateUpdates
{
    const string LatestUrl = "https://api.github.com/repos/welffi/welffi-animate/releases/latest";
    const string AssetName = "welffi-animate.exe";

    static readonly HttpClient Http = CreateClient();

    public static ProgramRelease? Latest { get; private set; }
    public static bool Downloading { get; private set; }
    public static event Action? Changed;

    public static string InstalledExe => Path.Combine(Paths.Programs, "welffi animate", "welffi animate.exe");
    static string InstalledTagFile => Path.ChangeExtension(InstalledExe, ".version");

    static string? InstalledTag => File.Exists(InstalledTagFile) ? File.ReadAllText(InstalledTagFile).Trim() : null;

    public static bool UpdateAvailable => Latest != null && IsNewer(InstalledTag, Latest.Tag);

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("welffi-hub");
        return client;
    }

    public static bool IsNewer(string? installed, string latest)
    {
        if (!Version.TryParse(latest.TrimStart('v'), out var newest))
            return false;
        return !Version.TryParse(installed?.TrimStart('v'), out var current) || newest > current;
    }

    public static ProgramRelease? ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("tag_name", out var tag) || !root.TryGetProperty("assets", out var assets))
            return null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == AssetName)
                return new ProgramRelease(tag.GetString()!, asset.GetProperty("browser_download_url").GetString()!);
        }
        return null;
    }

    public static async Task Check()
    {
        try
        {
            Latest = ParseRelease(await Http.GetStringAsync(LatestUrl));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
        }
        Changed?.Invoke();
    }

    public static async Task InstallLatest()
    {
        if (Latest == null)
            await Check();
        var release = Latest ?? throw new InvalidOperationException("Couldn't reach GitHub to download welffi animate.");
        Downloading = true;
        Changed?.Invoke();
        try
        {
            await Download(release);
        }
        finally
        {
            Downloading = false;
            Changed?.Invoke();
        }
    }

    static async Task Download(ProgramRelease release)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InstalledExe)!);
        var temp = InstalledExe + ".download";
        using (var response = await Http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(temp);
            await response.Content.CopyToAsync(file);
        }
        try
        {
            File.Move(temp, InstalledExe, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            File.Delete(temp);
            throw new IOException("Close welffi animate first, then try again.");
        }
        File.WriteAllText(InstalledTagFile, release.Tag);
    }
}
