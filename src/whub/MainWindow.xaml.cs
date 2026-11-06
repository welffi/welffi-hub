using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using Whub.Core;
using Whub.Pages;

namespace Whub;

public partial class MainWindow : Window
{
    const int DwmWindowCornerPreference = 33;
    const int DwmCornerRound = 2;

    readonly Dictionary<HubTool, UserControl> _pages = [];
    readonly Dictionary<HubTool, RadioButton> _navItems = [];
    readonly HomePage _home;

    public MainWindow()
    {
        InitializeComponent();
        _home = new HomePage(Open);
        AddNavItem("Home", "", ShowHome).IsChecked = true;
        foreach (var tool in HubTools.All.Where(t => t.Create != null))
            _navItems[tool] = AddNavItem(tool.Name, tool.Icon, () => ShowTool(tool));
        StateChanged += (_, _) => MatchWindowState();
        ShowHome();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        var corner = DwmCornerRound;
        DwmSetWindowAttribute(source.Handle, DwmWindowCornerPreference, ref corner, sizeof(int));
        GlobalHotkey.Attach(source);
        GlobalHotkey.Bind(HubData.Settings.SwitchKey);
        _ = Portraits.Refresh();
    }

    RadioButton AddNavItem(string name, string icon, Action show)
    {
        var item = new RadioButton { Style = (Style)FindResource("NavItem"), GroupName = "nav" };
        var glyph = new TextBlock { Text = icon, Style = (Style)FindResource("Icon"), Margin = new Thickness(0, 0, 12, 0) };
        var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(glyph);
        row.Children.Add(label);
        item.Content = row;
        item.Checked += (_, _) => show();
        AutomationProperties.SetName(item, name);
        Nav.Children.Add(item);
        return item;
    }

    void ShowHome() => PageHost.Content = _home;

    void ShowTool(HubTool tool)
    {
        if (!_pages.TryGetValue(tool, out var page))
            _pages[tool] = page = tool.Create!();
        PageHost.Content = page;
    }

    void Open(HubTool tool) => _navItems[tool].IsChecked = true;

    void MatchWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        var frame = SystemParameters.WindowResizeBorderThickness;
        Root.Margin = maximized ? new Thickness(frame.Left + 2, frame.Top + 2, frame.Right + 2, frame.Bottom + 2) : new Thickness(0);
        MaximizeButton.Content = maximized ? "" : "";
    }

    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("dwmapi")] static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
