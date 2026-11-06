using System.IO;
using System.Text.RegularExpressions;

namespace Whub.Core;

public sealed record ImportResult(int Accounts, int Servers);

public static partial class MacroImport
{
    public static Dictionary<string, Dictionary<string, string>> ParseIni(string text)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[line[1..^1]] = current;
                continue;
            }
            var split = line.IndexOf('=');
            if (current != null && split > 0)
                current[line[..split].Trim()] = line[(split + 1)..].Trim();
        }
        return sections;
    }

    public static ImportResult Merge(string accountsIni, string configIni, List<AccountRecord> accounts, HubSettings settings)
    {
        var config = ParseIni(configIni);
        var known = accounts.Select(a => a.Id).ToHashSet();
        var incoming = new List<AccountRecord>();
        foreach (var (id, values) in ParseIni(accountsIni))
        {
            if (!Digits().IsMatch(id) || known.Contains(id) || !values.TryGetValue("cookie", out var cookie) || cookie == "")
                continue;
            incoming.Add(new AccountRecord
            {
                Id = id,
                Name = values.GetValueOrDefault("name", id),
                Tracker = values.TryGetValue("tracker", out var tracker) && Digits().IsMatch(tracker)
                    ? tracker
                    : $"{Random.Shared.Next(100000, 175001)}{Random.Shared.Next(100000, 900001)}",
                SealedCookie = cookie,
            });
        }

        var order = config.GetValueOrDefault("accounts")?.GetValueOrDefault("order", "").Split(',').ToList() ?? [];
        var rank = (AccountRecord a) => order.IndexOf(a.Id) is var i && i >= 0 ? i : int.MaxValue;
        accounts.AddRange(incoming.OrderBy(rank));

        var servers = 0;
        foreach (var (name, link) in config.GetValueOrDefault("private_servers") ?? [])
        {
            if (settings.Servers.Any(s => s.Name == name) || !RobloxApi.IsPrivateLink(link))
                continue;
            settings.Servers.Add(new SavedServer(name, link));
            servers++;
        }

        if (settings.LastPrivateLink == "" && config.GetValueOrDefault("accounts")?.GetValueOrDefault("private_link") is { } last)
            settings.LastPrivateLink = last;
        if (config.GetValueOrDefault("fastflags")?.GetValueOrDefault("enabled") is "1" or "true")
            settings.FastFlags = true;
        return new ImportResult(incoming.Count, servers);
    }

    public static ImportResult FromFolder(string folder)
    {
        var accountsFile = Path.Combine(folder, "accounts.ini");
        if (!File.Exists(accountsFile))
            throw new InvalidOperationException("That folder has no accounts.ini");
        var configFile = Path.Combine(folder, "config.ini");
        var config = File.Exists(configFile) ? File.ReadAllText(configFile) : "";
        var result = Merge(File.ReadAllText(accountsFile), config, HubData.Accounts, HubData.Settings);
        HubData.SaveAccounts();
        HubData.SaveSettings();
        return result;
    }

    [GeneratedRegex("\\A[0-9]+\\z")] private static partial Regex Digits();
}
