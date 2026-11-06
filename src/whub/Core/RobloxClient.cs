using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace Whub.Core;

public sealed record LaunchResult(bool Ok, string Message);

public static partial class RobloxClient
{
    static readonly string ClientStorage = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "LocalStorage", "appStorage.json");

    static Mutex? _instanceMutex;

    public static bool HoldMultiInstance(int waitMs = 0)
    {
        _instanceMutex ??= new Mutex(false, "ROBLOX_singletonMutex");
        try
        {
            return _instanceMutex.WaitOne(waitMs);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    static void EnsureMultiInstance()
    {
        if (HoldMultiInstance())
            return;
        CloseTrayProcesses();
        if (!HoldMultiInstance(2000))
            throw new InvalidOperationException("A Roblox or the macro is holding the Roblox lock. Close them and try again.");
    }

    static void CloseTrayProcesses()
    {
        using var search = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='RobloxPlayerBeta.exe'");
        foreach (var item in search.Get())
        {
            if (item["CommandLine"] is not string line || !line.Contains("--launch-to-tray"))
                continue;
            try
            {
                using var process = Process.GetProcessById(Convert.ToInt32(item["ProcessId"]));
                if (process.MainWindowHandle == 0)
                    process.Kill();
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }

    public static string ClearSavedAccounts(string text)
    {
        var cleared = PreviousAccountsField().Replace(text, "\"PreviousAccountsList\":\"{}\"");
        return AccountBlobField().Replace(cleared, "");
    }

    static async Task ForgetOtherAccounts()
    {
        if (!File.Exists(ClientStorage))
            return;
        Exception? failure = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var text = await File.ReadAllTextAsync(ClientStorage);
                var cleared = ClearSavedAccounts(text);
                if (cleared != text)
                {
                    File.SetAttributes(ClientStorage, File.GetAttributes(ClientStorage) & ~FileAttributes.ReadOnly);
                    await File.WriteAllTextAsync(ClientStorage, cleared, new UTF8Encoding(false));
                }
                File.SetAttributes(ClientStorage, File.GetAttributes(ClientStorage) | FileAttributes.ReadOnly);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failure = e;
                await Task.Delay(100);
            }
        }
        throw new InvalidOperationException("Couldn't prepare Roblox's account storage: " + failure?.Message);
    }

    public static void ReleaseClientStorage()
    {
        try
        {
            File.SetAttributes(ClientStorage, File.GetAttributes(ClientStorage) & ~FileAttributes.ReadOnly);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    static async Task<string> Prepare(AccountRecord account)
    {
        EnsureMultiInstance();
        await ForgetOtherAccounts();
        return CookieSeal.Unseal(account.SealedCookie);
    }

    static void Start(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })?.Dispose();

    public static async Task<LaunchResult> Open(AccountRecord account, string followName)
    {
        try
        {
            var cookie = await Prepare(account);
            var query = followName == "" ? "" : RobloxApi.FollowQuery(await RobloxApi.UserIdOf(followName));
            Start(RobloxApi.LaunchUri(account, await RobloxApi.AuthTicket(cookie), query));
            return new LaunchResult(true, followName == "" ? "Opened Roblox on " + account.Name : $"{account.Name} is following {followName}");
        }
        catch (Exception e) when (IsExpected(e))
        {
            return new LaunchResult(false, e.Message);
        }
    }

    public static async Task<LaunchResult> JoinMostRecent(AccountRecord account)
    {
        try
        {
            var cookie = await Prepare(account);
            var game = await RobloxApi.FindRecentGame(cookie);
            Start(RobloxApi.LaunchUri(account, await RobloxApi.AuthTicket(cookie), RobloxApi.JoinQuery(account, game.PlaceId)));
            return new LaunchResult(true, $"{account.Name} is joining {game.Name}");
        }
        catch (Exception e) when (IsExpected(e))
        {
            return new LaunchResult(false, e.Message);
        }
    }

    public static async Task<LaunchResult> JoinPrivate(AccountRecord account, string link)
    {
        try
        {
            var cookie = await Prepare(account);
            var server = await RobloxApi.ResolvePrivateServer(cookie, link);
            Start(RobloxApi.LaunchUri(account, await RobloxApi.AuthTicket(cookie), RobloxApi.PrivateQuery(account, server)));
            return new LaunchResult(true, account.Name + " is joining the private server");
        }
        catch (Exception e) when (IsExpected(e))
        {
            return new LaunchResult(false, e.Message);
        }
    }

    public static bool IsExpected(Exception e) =>
        e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException
            or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException
            or ManagementException or KeyNotFoundException;

    [GeneratedRegex("\"PreviousAccountsList\"\\s*:\\s*\"(?>[^\"\\\\]+|\\\\.)*\"")] private static partial Regex PreviousAccountsField();
    [GeneratedRegex(",\\s*\"AccountBlob\"\\s*:\\s*\"(?>[^\"\\\\]+|\\\\.)*\"|\"AccountBlob\"\\s*:\\s*\"(?>[^\"\\\\]+|\\\\.)*\"\\s*,?")] private static partial Regex AccountBlobField();
}
