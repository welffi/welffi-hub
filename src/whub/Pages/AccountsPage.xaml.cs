using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Whub.Core;

namespace Whub.Pages;

public partial class AccountsPage : UserControl
{
    const double MinTileWidth = 150;
    const double TilePitch = 126;
    const double EdgeScrollZone = 30;
    const double EdgeScrollStep = 16;
    static readonly string DefaultMacroFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "AutoHotKey", "welffis macros");
    static readonly Key[] LoneModifiers = [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin, Key.System];

    readonly ObservableCollection<AccountRow> _rows = [];
    readonly ObservableCollection<SavedServer> _servers = [];
    AccountRow? _pressed;
    Point _pressPoint;
    bool _busy;
    bool _capturingKey;
    TaskCompletionSource<bool>? _confirm;

    public AccountsPage()
    {
        InitializeComponent();
        AccountList.ItemsSource = _rows;
        ServerList.ItemsSource = _servers;
        foreach (var server in HubData.Settings.Servers)
            _servers.Add(server);
        LinkBox.Text = HubData.Settings.LastPrivateLink;
        FastFlagsSwitch.IsChecked = HubData.Settings.FastFlags;
        LoadRows();
        ShowSwitchKey();
        HubData.PortraitsUpdated += () =>
        {
            foreach (var row in _rows)
                row.Portrait = Portraits.Load(row.Record.Id);
        };
    }

    void LoadRows()
    {
        var tickedIds = _rows.Where(r => r.Ticked).Select(r => r.Record.Id).ToHashSet();
        var firstLoad = _rows.Count == 0;
        foreach (var row in _rows)
            row.PropertyChanged -= Row_PropertyChanged;
        _rows.Clear();
        foreach (var account in HubData.Accounts)
        {
            var ticked = tickedIds.Contains(account.Id) || (firstLoad && _rows.Count == 0);
            var row = new AccountRow(account, ticked);
            row.PropertyChanged += Row_PropertyChanged;
            _rows.Add(row);
        }
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectAllButton.Visibility = _rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ShowTickSummary();
    }

    void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountRow.Ticked))
            ShowTickSummary();
    }

    void ShowTickSummary()
    {
        var ticked = _rows.Count(r => r.Ticked);
        TickSummary.Text = _rows.Count == 0 ? "" : $"{ticked} of {_rows.Count} ticked";
    }

    void SetStatus(string message, bool? ok = null)
    {
        StatusText.Text = message;
        StatusDot.Fill = (Brush)FindResource(ok switch { true => "Good", false => "Danger", _ => "Faint" });
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        Actions.IsEnabled = !busy;
        AddButton.IsEnabled = !busy;
        ImportButton.IsEnabled = !busy;
    }

    async Task RunForTicked(Func<AccountRecord, Task<LaunchResult>> action)
    {
        if (_busy)
            return;
        var rows = _rows.Where(r => r.Ticked).ToList();
        if (rows.Count == 0)
        {
            SetStatus("Tick an account first", false);
            return;
        }

        SetBusy(true);
        try
        {
            FastFlags.Apply(HubData.Settings.FastFlags);
            var opened = 0;
            var lastFailure = "";
            foreach (var row in rows)
            {
                if (row != rows[0])
                    await Task.Delay(HubData.Settings.LaunchGapMs);
                row.Note = "Launching...";
                SetStatus($"Launching {row.Name}...");
                var result = await action(row.Record);
                row.Note = result.Message;
                if (result.Ok)
                    opened++;
                else
                    lastFailure = result.Message;
            }
            var summary = $"Launched {opened} of {rows.Count} accounts";
            SetStatus(lastFailure == "" ? summary : $"{summary}. {lastFailure}", opened == rows.Count);
        }
        catch (Exception e) when (RobloxClient.IsExpected(e))
        {
            SetStatus(e.Message, false);
        }
        finally
        {
            SetBusy(false);
        }
    }

    async void Open_Click(object sender, RoutedEventArgs e)
    {
        var follow = FollowBox.Text.Trim();
        await RunForTicked(account => RobloxClient.Open(account, follow));
    }

    async void JoinRecent_Click(object sender, RoutedEventArgs e) =>
        await RunForTicked(RobloxClient.JoinMostRecent);

    async void JoinLink_Click(object sender, RoutedEventArgs e)
    {
        var link = LinkBox.Text.Trim();
        if (link == "")
        {
            SetStatus("Paste a private server link first", false);
            return;
        }
        HubData.Settings.LastPrivateLink = link;
        HubData.SaveSettings();
        await RunForTicked(account => RobloxClient.JoinPrivate(account, link));
    }

    async void JoinServer_Click(object sender, RoutedEventArgs e)
    {
        var server = (SavedServer)((FrameworkElement)sender).DataContext;
        await RunForTicked(account => RobloxClient.JoinPrivate(account, server.Link));
    }

    void SaveServer_Click(object sender, RoutedEventArgs e)
    {
        var name = ServerNameBox.Text.Trim();
        var link = LinkBox.Text.Trim();
        if (!RobloxApi.IsPrivateLink(link))
        {
            SetStatus("That isn't a private server link", false);
            return;
        }
        if (name == "")
        {
            SetStatus("Give the server a name first", false);
            return;
        }

        var server = new SavedServer(name, link);
        var existing = _servers.FirstOrDefault(s => s.Name == name);
        if (existing != null)
            _servers[_servers.IndexOf(existing)] = server;
        else
            _servers.Add(server);
        SaveServers();
        ServerNameBox.Clear();
        SetStatus("Saved " + name, true);
    }

    async void DeleteServer_Click(object sender, RoutedEventArgs e)
    {
        var server = (SavedServer)((FrameworkElement)sender).DataContext;
        if (!await Confirm($"Delete {server.Name}?", "The saved link is removed from this list."))
            return;
        _servers.Remove(server);
        SaveServers();
        SetStatus("Deleted " + server.Name);
    }

    void SaveServers()
    {
        HubData.Settings.Servers = [.. _servers];
        HubData.SaveSettings();
    }

    async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        SetBusy(true);
        SetStatus("Log in using the browser window. It closes by itself.");
        try
        {
            var result = await BrowserLogin.AddAccount();
            SetStatus(result.Message, result.Ok);
            if (result.Ok)
            {
                LoadRows();
                await Portraits.Refresh();
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;
        var row = (AccountRow)((FrameworkElement)sender).DataContext;
        if (!await Confirm($"Remove {row.Name}?", "This only removes it from the hub. You can add it back by logging in."))
            return;
        HubData.Accounts.Remove(row.Record);
        HubData.SaveAccounts();
        Portraits.Delete(row.Record.Id);
        row.Ticked = false;
        LoadRows();
        SetStatus("Removed " + row.Name);
    }

    async void Import_Click(object sender, RoutedEventArgs e)
    {
        var folder = DefaultMacroFolder;
        if (!File.Exists(Path.Combine(folder, "accounts.ini")))
        {
            var dialog = new OpenFolderDialog { Title = "Select the welffi's macros folder" };
            if (dialog.ShowDialog() != true)
                return;
            folder = dialog.FolderName;
        }

        try
        {
            var result = MacroImport.FromFolder(folder);
            _servers.Clear();
            foreach (var server in HubData.Settings.Servers)
                _servers.Add(server);
            LinkBox.Text = HubData.Settings.LastPrivateLink;
            FastFlagsSwitch.IsChecked = HubData.Settings.FastFlags;
            LoadRows();
            SetStatus($"Imported {result.Accounts} accounts and {result.Servers} servers", true);
            await Portraits.Refresh();
        }
        catch (Exception error) when (RobloxClient.IsExpected(error))
        {
            SetStatus(error.Message, false);
        }
    }

    void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var tick = _rows.Any(r => !r.Ticked);
        foreach (var row in _rows)
            row.Ticked = tick;
    }

    public static int ColumnsFor(double width) => Math.Max(1, (int)(width / MinTileWidth));

    public static int TileIndexAt(double x, double y, double width, int columns, int count)
    {
        var rows = (int)Math.Ceiling(count / (double)columns);
        var column = Math.Clamp((int)(x / (width / columns)), 0, columns - 1);
        var row = Math.Clamp((int)(y / TilePitch), 0, rows - 1);
        return Math.Min(row * columns + column, count - 1);
    }

    void ListScroll_SizeChanged(object sender, SizeChangedEventArgs e) =>
        AccountList.Tag = ColumnsFor(e.NewSize.Width - AccountList.Margin.Left - AccountList.Margin.Right);

    void Tile_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = (AccountRow)((FrameworkElement)sender).DataContext;
        _pressPoint = e.GetPosition(AccountList);
        AccountList.CaptureMouse();
        e.Handled = true;
    }

    void List_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed == null)
            return;
        var pointer = e.GetPosition(AccountList);
        if (!_pressed.Dragging)
        {
            var moved = pointer - _pressPoint;
            if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;
            _pressed.Dragging = true;
        }

        var target = TileIndexAt(pointer.X, pointer.Y, AccountList.ActualWidth, (int)AccountList.Tag, _rows.Count);
        var current = _rows.IndexOf(_pressed);
        if (target != current)
            _rows.Move(current, target);

        var inView = e.GetPosition(ListScroll).Y;
        if (inView < EdgeScrollZone)
            ListScroll.ScrollToVerticalOffset(ListScroll.VerticalOffset - EdgeScrollStep);
        else if (inView > ListScroll.ActualHeight - EdgeScrollZone)
            ListScroll.ScrollToVerticalOffset(ListScroll.VerticalOffset + EdgeScrollStep);
    }

    void List_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_pressed is { Dragging: false })
            _pressed.Ticked = !_pressed.Ticked;
        AccountList.ReleaseMouseCapture();
    }

    void List_LostCapture(object sender, MouseEventArgs e)
    {
        if (_pressed == null)
            return;
        var reordered = _pressed.Dragging;
        _pressed.Dragging = false;
        _pressed = null;
        if (!reordered)
            return;
        HubData.Accounts.Clear();
        HubData.Accounts.AddRange(_rows.Select(r => r.Record));
        HubData.SaveAccounts();
    }

    void FastFlags_Click(object sender, RoutedEventArgs e)
    {
        var on = FastFlagsSwitch.IsChecked == true;
        try
        {
            FastFlags.Apply(on);
            HubData.Settings.FastFlags = on;
            HubData.SaveSettings();
            SetStatus(on ? "FastFlags on. Restart Roblox to apply." : "FastFlags off. Defaults restored.", true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            FastFlagsSwitch.IsChecked = !on;
            SetStatus("Couldn't change FastFlags: " + error.Message, false);
        }
    }

    void ShowSwitchKey() => SwitchKeyButton.Content = HubData.Settings.SwitchKey?.Label ?? "Not set";

    void SwitchKey_Click(object sender, RoutedEventArgs e)
    {
        _capturingKey = true;
        SwitchKeyButton.Content = "Press a key";
        SetStatus("Press the new switch key. Esc cancels, Backspace clears.");
        Focus();
    }

    void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ConfirmLayer.Visibility == Visibility.Visible && e.Key == Key.Escape)
        {
            FinishConfirm(false);
            e.Handled = true;
            return;
        }
        if (!_capturingKey)
            return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (LoneModifiers.Contains(key))
            return;
        _capturingKey = false;

        if (key == Key.Escape)
        {
            ShowSwitchKey();
            SetStatus("Ready");
            return;
        }
        if (key == Key.Back)
        {
            ApplySwitchKey(null);
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None && key is Key.F2 or Key.F3)
        {
            ShowSwitchKey();
            SetStatus($"{key} is kept for starting and stopping macros", false);
            return;
        }
        ApplySwitchKey(BuildBinding(key));
    }

    static SwitchKeyBinding BuildBinding(Key key)
    {
        var modifiers = Keyboard.Modifiers;
        var parts = new List<string>();
        var flags = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) { parts.Add("Ctrl"); flags |= 2; }
        if (modifiers.HasFlag(ModifierKeys.Alt)) { parts.Add("Alt"); flags |= 1; }
        if (modifiers.HasFlag(ModifierKeys.Shift)) { parts.Add("Shift"); flags |= 4; }
        parts.Add(key.ToString());
        return new SwitchKeyBinding(flags, KeyInterop.VirtualKeyFromKey(key), string.Join("+", parts));
    }

    void ApplySwitchKey(SwitchKeyBinding? binding)
    {
        if (!GlobalHotkey.Bind(binding))
        {
            GlobalHotkey.Bind(HubData.Settings.SwitchKey);
            ShowSwitchKey();
            SetStatus($"{binding?.Label} is already used by another program", false);
            return;
        }
        HubData.Settings.SwitchKey = binding;
        HubData.SaveSettings();
        ShowSwitchKey();
        SetStatus(binding == null ? "Switch key cleared" : "Switch key set to " + binding.Label, true);
    }

    Task<bool> Confirm(string title, string body)
    {
        ConfirmTitle.Text = title;
        ConfirmBody.Text = body;
        ConfirmLayer.Visibility = Visibility.Visible;
        ConfirmCancel.Focus();
        _confirm = new TaskCompletionSource<bool>();
        return _confirm.Task;
    }

    void FinishConfirm(bool accepted)
    {
        ConfirmLayer.Visibility = Visibility.Collapsed;
        _confirm?.TrySetResult(accepted);
    }

    void ConfirmCancel_Click(object sender, RoutedEventArgs e) => FinishConfirm(false);

    void ConfirmAccept_Click(object sender, RoutedEventArgs e) => FinishConfirm(true);
}
