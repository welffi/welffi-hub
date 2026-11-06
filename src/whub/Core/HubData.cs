using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Whub.Core;

public sealed class AccountRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tracker { get; set; } = "";
    public string SealedCookie { get; set; } = "";
}

public sealed record SavedServer(string Name, string Link);

public sealed record SwitchKeyBinding(int Modifiers, int VirtualKey, string Label);

public sealed class HubSettings
{
    public List<SavedServer> Servers { get; set; } = [];
    public string LastPrivateLink { get; set; } = "";
    public int LaunchGapMs { get; set; } = 2500;
    public bool FastFlags { get; set; }
    public SwitchKeyBinding? SwitchKey { get; set; } = new(0, 0x73, "F4");
}

public static class Paths
{
    public static string Root { get; } = Environment.GetEnvironmentVariable("WHUB_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "welffi hub");

    public static string Accounts => Path.Combine(Root, "accounts.json");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Portraits => Path.Combine(Root, "portraits");
    public static string Programs => Path.Combine(Root, "programs");
}

public static class HubData
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<AccountRecord> Accounts { get; private set; } = [];
    public static HubSettings Settings { get; private set; } = new();

    public static event Action? PortraitsUpdated;

    public static void Load()
    {
        Directory.CreateDirectory(Paths.Root);
        Accounts = Read(Paths.Accounts, () => new List<AccountRecord>());
        Settings = Read(Paths.Settings, () => new HubSettings());
    }

    public static void SaveAccounts() => Write(Paths.Accounts, Accounts);

    public static void SaveSettings() => Write(Paths.Settings, Settings);

    public static void RaisePortraitsUpdated() => PortraitsUpdated?.Invoke();

    static T Read<T>(string path, Func<T> fallback)
    {
        if (!File.Exists(path))
            return fallback();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback();
        }
        catch (JsonException)
        {
            File.Move(path, path + ".unreadable", true);
            return fallback();
        }
    }

    static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, true);
    }
}

public static class CookieSeal
{
    public static string Seal(string cookie) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(cookie), null, DataProtectionScope.CurrentUser));

    public static string Unseal(string sealedCookie)
    {
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(sealedCookie), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            throw new InvalidOperationException("Saved login can't be read on this Windows user. Add the account again.");
        }
    }
}
