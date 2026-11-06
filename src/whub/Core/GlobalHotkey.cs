using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Whub.Core;

public static class GlobalHotkey
{
    const int HotkeyId = 0x5748;
    const int WmHotkey = 0x312;
    const int ModNoRepeat = 0x4000;

    static HwndSource? _source;
    static int _virtualKey;

    public static void Attach(HwndSource source)
    {
        _source = source;
        source.AddHook(OnMessage);
    }

    public static bool Bind(SwitchKeyBinding? binding)
    {
        if (_source == null)
            return false;
        UnregisterHotKey(_source.Handle, HotkeyId);
        _virtualKey = 0;
        if (binding == null)
            return true;
        if (!RegisterHotKey(_source.Handle, HotkeyId, (uint)(binding.Modifiers | ModNoRepeat), (uint)binding.VirtualKey))
            return false;
        _virtualKey = binding.VirtualKey;
        return true;
    }

    static nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam == HotkeyId)
        {
            handled = true;
            _ = RobloxWindows.ActivateNext(_virtualKey);
        }
        return 0;
    }

    [DllImport("user32", SetLastError = true)] static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32")] static extern bool UnregisterHotKey(nint hwnd, int id);
}
