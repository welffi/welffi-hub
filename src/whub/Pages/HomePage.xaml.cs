using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Whub.Core;

namespace Whub.Pages;

public partial class HomePage : UserControl
{
    readonly Action<HubTool> _open;
    readonly DispatcherTimer _windowTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public HomePage(Action<HubTool> open)
    {
        _open = open;
        InitializeComponent();
        _windowTimer.Tick += (_, _) => WindowCount.Text = RobloxWindows.FindAll().Count.ToString();
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => _windowTimer.Stop();
        HubData.PortraitsUpdated += () => { if (IsLoaded) Refresh(); };
        AnimateUpdates.Changed += () => { if (IsLoaded) Refresh(); };
    }

    void Refresh()
    {
        var now = DateTime.Now;
        Greeting.Text = now.Hour switch
        {
            < 5 => "Up late",
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening",
        };
        DateLine.Text = now.ToString("dddd, MMMM d");
        AccountCount.Text = HubData.Accounts.Count.ToString();
        ServerCount.Text = HubData.Settings.Servers.Count.ToString();
        WindowCount.Text = RobloxWindows.FindAll().Count.ToString();
        _windowTimer.Start();

        ToolGrid.Children.Clear();
        foreach (var tool in HubTools.All)
            ToolGrid.Children.Add(BuildTile(tool));
    }

    FrameworkElement BuildTile(HubTool tool)
    {
        var available = tool.Create != null || tool.Launch != null;
        var iconBack = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(11),
            Background = (Brush)FindResource(available ? "AccentSoft" : "Raised"),
            Child = new TextBlock
            {
                Text = tool.Icon,
                Style = (Style)FindResource("Icon"),
                FontSize = 18,
                Foreground = (Brush)FindResource(available ? "Accent" : "Muted"),
            },
        };

        var preview = tool.Preview();
        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0), MinHeight = 34, LastChildFill = false };
        var summary = new TextBlock { Text = tool.Summary(), Style = (Style)FindResource("Hint"), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(summary, Dock.Left);
        footer.Children.Add(summary);
        if (preview != null)
        {
            DockPanel.SetDock(preview, Dock.Right);
            footer.Children.Add(preview);
        }

        var body = new StackPanel();
        body.Children.Add(iconBack);
        iconBack.HorizontalAlignment = HorizontalAlignment.Left;
        body.Children.Add(new TextBlock { Text = tool.Name, Style = (Style)FindResource("CardTitle"), Margin = new Thickness(0, 14, 0, 4) });
        body.Children.Add(new TextBlock { Text = tool.Blurb, Style = (Style)FindResource("Hint"), TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        body.Children.Add(footer);

        if (!available)
            return new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(6), Padding = new Thickness(20), Opacity = 0.6, Child = body };

        var button = new Button
        {
            Style = (Style)FindResource("ToolTile"),
            Margin = new Thickness(6),
            Content = body,
        };
        AutomationProperties.SetName(button, "Open " + tool.Name);
        button.Click += (_, _) =>
        {
            if (tool.Launch != null)
                tool.Launch();
            else
                _open(tool);
        };
        return button;
    }
}
