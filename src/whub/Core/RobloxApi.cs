using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Whub.Core;

public sealed record RecentGame(string PlaceId, string Name);

public sealed record PrivateServerTarget(string PlaceId, string Code);

public static partial class RobloxApi
{
    const string SessionExpired = "Session expired. Remove the account and add it again.";
    const string Referer = "https://www.roblox.com/";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        return client;
    }

    static async Task<HttpResponseMessage> Send(HttpMethod method, string url, string? cookie = null, string? body = null, string? csrf = null, bool referer = false)
    {
        using var request = new HttpRequestMessage(method, url);
        if (cookie != null)
            request.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
        if (csrf != null)
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
        if (referer)
            request.Headers.Referrer = new Uri(Referer);
        request.Content = ContentFor(method, body);
        return await Http.SendAsync(request);
    }

    public static HttpContent? ContentFor(HttpMethod method, string? body)
    {
        if (body != null)
            return new StringContent(body, Encoding.UTF8, "application/json");
        return method == HttpMethod.Post ? new StringContent("") : null;
    }

    static string? HeaderValue(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    static async Task<HttpResponseMessage> PostWithCsrf(string url, string cookie, string body)
    {
        var response = await Send(HttpMethod.Post, url, cookie, body, referer: true);
        if (response.StatusCode != HttpStatusCode.Forbidden)
            return response;
        var token = HeaderValue(response.Headers, "x-csrf-token");
        return await Send(HttpMethod.Post, url, cookie, body, token, true);
    }

    public static async Task<(string Id, string Name)> WhoAmI(string cookie)
    {
        var response = await Send(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated", cookie);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Roblox rejected the login ({(int)response.StatusCode})");
        var text = await response.Content.ReadAsStringAsync();
        var id = IdField().Match(text);
        var name = NameField().Match(text);
        if (!id.Success || !name.Success)
            throw new InvalidOperationException("Unexpected reply from Roblox");
        return (id.Groups[1].Value, name.Groups[1].Value);
    }

    public static async Task<string> AuthTicket(string cookie)
    {
        const string url = "https://auth.roblox.com/v1/authentication-ticket/";
        var first = await Send(HttpMethod.Post, url, cookie, referer: true);
        if (first.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(SessionExpired);
        var token = HeaderValue(first.Headers, "x-csrf-token");
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException($"No CSRF token from Roblox ({(int)first.StatusCode})");

        var second = await Send(HttpMethod.Post, url, cookie, csrf: token, referer: true);
        var ticket = HeaderValue(second.Headers, "rbx-authentication-ticket");
        if (string.IsNullOrEmpty(ticket))
            throw new InvalidOperationException($"No auth ticket from Roblox ({(int)second.StatusCode})");
        return ticket;
    }

    public static async Task<string> UserIdOf(string username)
    {
        if (!UsernameFormat().IsMatch(username))
            throw new InvalidOperationException("That isn't a valid Roblox username");
        var body = JsonSerializer.Serialize(new { usernames = new[] { username }, excludeBannedUsers = true });
        var response = await Send(HttpMethod.Post, "https://users.roblox.com/v1/usernames/users", body: body);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Username lookup failed ({(int)response.StatusCode})");
        var id = IdField().Match(await response.Content.ReadAsStringAsync());
        if (!id.Success)
            throw new InvalidOperationException("No Roblox user named " + username);
        return id.Groups[1].Value;
    }

    public static async Task<RecentGame> FindRecentGame(string cookie)
    {
        var body = JsonSerializer.Serialize(new { pageType = "Home", sessionId = Guid.NewGuid().ToString() });
        var response = await PostWithCsrf("https://apis.roblox.com/discovery-api/omni-recommendation", cookie, body);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(SessionExpired);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Recent games lookup failed ({(int)response.StatusCode})");
        return ParseRecentGame(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("Roblox didn't list a recent game for this account");
    }

    public static RecentGame? ParseRecentGame(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("sorts", out var sorts) || sorts.ValueKind != JsonValueKind.Array)
            return null;
        if (!root.TryGetProperty("contentMetadata", out var metadata) || !metadata.TryGetProperty("Game", out var games))
            return null;

        foreach (var sort in sorts.EnumerateArray())
        {
            if (!IsContinueSort(sort) || !sort.TryGetProperty("recommendationList", out var list))
                continue;
            foreach (var item in list.EnumerateArray())
            {
                if (item.GetProperty("contentType").GetString() != "Game")
                    continue;
                var contentId = item.GetProperty("contentId").ToString();
                if (games.TryGetProperty(contentId, out var game)
                    && game.TryGetProperty("rootPlaceId", out var place)
                    && place.ValueKind == JsonValueKind.Number)
                    return new RecentGame(place.ToString(), game.GetProperty("name").GetString() ?? "the game");
            }
        }
        return null;
    }

    static bool IsContinueSort(JsonElement sort)
    {
        if (sort.TryGetProperty("topicId", out var id) && id.ToString() == "100000003")
            return true;
        return sort.TryGetProperty("topic", out var topic)
            && topic.ValueKind == JsonValueKind.String
            && topic.GetString()!.StartsWith("continue", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPrivateLink(string link) => PrivateLinkFormat().IsMatch(link);

    public static PrivateServerTarget? ParseDirectPrivateLink(string link)
    {
        var place = GamesPath().Match(link);
        var code = LinkCodeParam().Match(link);
        return place.Success && code.Success ? new PrivateServerTarget(place.Groups[1].Value, code.Groups[1].Value) : null;
    }

    public static async Task<PrivateServerTarget> ResolvePrivateServer(string cookie, string link)
    {
        var direct = ParseDirectPrivateLink(link);
        if (direct != null)
            return direct;
        var share = ShareCode().Match(link);
        if (!share.Success)
            throw new InvalidOperationException("That isn't a private server link");

        var body = JsonSerializer.Serialize(new { linkId = share.Groups[1].Value, linkType = "Server" });
        var response = await PostWithCsrf("https://apis.roblox.com/sharelinks/v1/resolve-link", cookie, body);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(SessionExpired);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Private server link lookup failed ({(int)response.StatusCode})");

        var text = await response.Content.ReadAsStringAsync();
        var place = PlaceIdField().Match(text);
        var code = LinkCodeField().Match(text);
        if (!place.Success || !code.Success)
            throw new InvalidOperationException("That private server link is invalid or expired");
        return new PrivateServerTarget(place.Groups[1].Value, code.Groups[1].Value);
    }

    public static async Task FetchPortraits(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
            return;
        Directory.CreateDirectory(Paths.Portraits);
        var url = "https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds=" + string.Join(",", ids) + "&size=150x150&format=Png&isCircular=false";
        using var response = await Send(HttpMethod.Get, url);
        if (response.StatusCode != HttpStatusCode.OK)
            return;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            if (item.GetProperty("state").GetString() != "Completed")
                continue;
            var imageUrl = item.GetProperty("imageUrl").GetString();
            if (imageUrl == null || !IsPortraitHost(imageUrl))
                continue;
            var target = Path.Combine(Paths.Portraits, item.GetProperty("targetId").GetInt64() + ".png");
            var temp = target + ".tmp";
            await File.WriteAllBytesAsync(temp, await Http.GetByteArrayAsync(imageUrl));
            File.Move(temp, target, true);
        }
    }

    public static bool IsPortraitHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase);

    public static string LaunchUri(AccountRecord account, string ticket, string placeQuery)
    {
        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tail = $"+launchtime:{time}+browsertrackerid:{account.Tracker}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
        if (placeQuery == "")
            return "roblox-player:1+launchmode:app+gameinfo:" + ticket + tail;
        return "roblox-player:1+launchmode:play+gameinfo:" + ticket
             + "+placelauncherurl:https%3A%2F%2Fassetgame.roblox.com%2Fgame%2FPlaceLauncher.ashx%3F"
             + placeQuery + tail;
    }

    public static string FollowQuery(string userId) => "request%3DRequestFollowUser%26userId%3D" + userId;

    public static string JoinQuery(AccountRecord account, string placeId) =>
        $"request%3DRequestGame%26browserTrackerId%3D{account.Tracker}%26placeId%3D{placeId}%26isPlayTogetherGame%3Dfalse";

    public static string PrivateQuery(AccountRecord account, PrivateServerTarget server) =>
        $"request%3DRequestPrivateGame%26browserTrackerId%3D{account.Tracker}%26placeId%3D{server.PlaceId}%26linkCode%3D{server.Code}";

    [GeneratedRegex("\"id\"\\s*:\\s*(\\d+)")] private static partial Regex IdField();
    [GeneratedRegex("\"name\"\\s*:\\s*\"([^\"]+)\"")] private static partial Regex NameField();
    [GeneratedRegex("\\A[A-Za-z0-9_]{3,20}\\z")] private static partial Regex UsernameFormat();
    [GeneratedRegex(@"roblox\.com/(games/\d+.*[?&]privateServerLinkCode=\w|share\?.*\bcode=\w)", RegexOptions.IgnoreCase)] private static partial Regex PrivateLinkFormat();
    [GeneratedRegex(@"/games/(\d+)")] private static partial Regex GamesPath();
    [GeneratedRegex(@"[?&]privateServerLinkCode=(\w+)")] private static partial Regex LinkCodeParam();
    [GeneratedRegex(@"roblox\.com/share\?.*?\bcode=(\w+)", RegexOptions.IgnoreCase)] private static partial Regex ShareCode();
    [GeneratedRegex("\"placeId\"\\s*:\\s*(\\d+)")] private static partial Regex PlaceIdField();
    [GeneratedRegex("\"linkCode\"\\s*:\\s*\"(\\w+)\"")] private static partial Regex LinkCodeField();
}
