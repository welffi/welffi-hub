using System.Windows;
using System.Windows.Controls;
using Whub.Core;
using Whub.Pages;

namespace Whub;

public sealed record HubTool(string Name, string Blurb, string Icon, Func<UserControl>? Create, Func<string> Summary, Func<UIElement?> Preview, Action? Launch = null);

public static class HubTools
{
    public static readonly IReadOnlyList<HubTool> All =
    [
        new("Account Manager",
            "Log in to your Roblox accounts and launch them together.",
            "",
            () => new AccountsPage(),
            () => HubData.Accounts.Count == 1 ? "1 account" : $"{HubData.Accounts.Count} accounts",
            PortraitStack.ForAccounts),
        new("welffi animate",
            "Animate Roblox rigs and copy them straight into Studio.",
            "",
            null,
            Programs.AnimateStatus,
            () => null,
            AnimateLauncher.Open),
        new("Macros",
            "Record and play back macros. Moving over from AutoHotkey later.",
            "",
            null,
            () => "Coming later",
            () => null),
    ];
}
