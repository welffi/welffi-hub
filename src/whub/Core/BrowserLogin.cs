using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Whub.Core;

public static class BrowserLogin
{
    static readonly string[] Browsers = ["brave.exe", "msedge.exe", "chrome.exe", "opera.exe"];

    public static string? FindBrowser()
    {
        foreach (var exe in Browsers)
        {
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
                var path = (key?.GetValue(null) as string)?.Trim('"');
                if (path != null && File.Exists(path))
                    return path;
            }
        }
        return null;
    }

    public static async Task<LaunchResult> AddAccount()
    {
        var browser = FindBrowser();
        if (browser == null)
            return new LaunchResult(false, "No Chromium browser found for the login window");

        try
        {
            var cookie = await Task.Run(() => CaptureCookie(browser));
            if (cookie == null)
                return new LaunchResult(false, "Login cancelled");

            var (id, name) = await RobloxApi.WhoAmI(cookie);
            var account = HubData.Accounts.FirstOrDefault(a => a.Id == id);
            if (account == null)
            {
                account = new AccountRecord { Id = id, Tracker = $"{Random.Shared.Next(100000, 175001)}{Random.Shared.Next(100000, 900001)}" };
                HubData.Accounts.Add(account);
            }
            account.Name = name;
            account.SealedCookie = CookieSeal.Seal(cookie);
            HubData.SaveAccounts();
            return new LaunchResult(true, "Added " + name);
        }
        catch (Exception e) when (RobloxClient.IsExpected(e))
        {
            return new LaunchResult(false, e.Message);
        }
    }

    static async Task<string?> CaptureCookie(string browser)
    {
        var profile = Path.Combine(Path.GetTempPath(), "whub_login_" + Guid.NewGuid());
        var info = new ProcessStartInfo(browser);
        info.ArgumentList.Add("--user-data-dir=" + profile);
        info.ArgumentList.Add("--remote-debugging-port=0");
        info.ArgumentList.Add("--no-first-run");
        info.ArgumentList.Add("--no-default-browser-check");
        info.ArgumentList.Add("https://www.roblox.com/login");
        Process.Start(info)?.Dispose();

        try
        {
            using var socket = await Connect(Path.Combine(profile, "DevToolsActivePort"));
            var deadline = DateTime.UtcNow.AddMinutes(15);
            string? cookie = null;
            while (cookie == null && DateTime.UtcNow < deadline)
            {
                cookie = await FindSessionCookie(socket);
                if (cookie == null)
                    await Task.Delay(1000);
            }
            await SendCdp(socket, "{\"id\":2,\"method\":\"Browser.close\"}");
            return cookie;
        }
        catch (Exception e) when (e is WebSocketException or IOException or TimeoutException)
        {
            return null;
        }
        finally
        {
            await RemoveProfile(profile);
        }
    }

    static async Task<ClientWebSocket> Connect(string portFile)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists(portFile))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Browser did not open");
            await Task.Delay(200);
        }
        await Task.Delay(200);
        var lines = await File.ReadAllLinesAsync(portFile);
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{lines[0]}{lines[1]}"), CancellationToken.None);
        return socket;
    }

    static Task SendCdp(ClientWebSocket socket, string json) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

    static async Task<string> ReceiveCdp(ClientWebSocket socket)
    {
        var buffer = new byte[65536];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Browser closed");
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    static async Task<string?> FindSessionCookie(ClientWebSocket socket)
    {
        await SendCdp(socket, "{\"id\":1,\"method\":\"Storage.getCookies\"}");
        return ParseSessionCookie(await ReceiveCdp(socket));
    }

    public static string? ParseSessionCookie(string reply)
    {
        using var doc = JsonDocument.Parse(reply);
        if (!doc.RootElement.TryGetProperty("result", out var result) || !result.TryGetProperty("cookies", out var cookies))
            return null;
        foreach (var cookie in cookies.EnumerateArray())
        {
            if (cookie.GetProperty("name").GetString() == ".ROBLOSECURITY"
                && cookie.GetProperty("domain").GetString()!.EndsWith("roblox.com", StringComparison.OrdinalIgnoreCase))
                return cookie.GetProperty("value").GetString();
        }
        return null;
    }

    static async Task RemoveProfile(string profile)
    {
        await Task.Delay(500);
        using (var search = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process"))
        {
            foreach (var item in search.Get())
            {
                if (item["CommandLine"] is not string line || !line.Contains(profile))
                    continue;
                try
                {
                    using var process = Process.GetProcessById(Convert.ToInt32(item["ProcessId"]));
                    process.Kill();
                }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
            }
        }
        for (var attempt = 0; attempt < 25 && Directory.Exists(profile); attempt++)
        {
            try
            {
                Directory.Delete(profile, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(400);
            }
        }
    }
}
