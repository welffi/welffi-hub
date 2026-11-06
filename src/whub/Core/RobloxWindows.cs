using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Whub.Core;

public static partial class RobloxWindows
{
    const int MinWidth = 300;
    const int MinHeight = 200;
    const int SwRestore = 9;
    const uint KeyUp = 2;
    const int WhKeyboardLl = 13;
    const int WmKeyDown = 0x100;
    const int WmKeyUp = 0x101;
    const int WmSysKeyDown = 0x104;
    const int WmSysKeyUp = 0x105;
    const int InjectedFlag = 0x10;

    static readonly int[] CarryKeys = [0x57, 0x41, 0x53, 0x44, 0x20, 0xA0];
    static readonly bool[] PhysicallyDown = new bool[256];
    static readonly LowLevelKeyboardProc KeyboardCallback = OnKeyboard;
    static Thread? _hookThread;

    public static List<nint> FindAll()
    {
        var pids = new HashSet<uint>();
        foreach (var process in Process.GetProcessesByName("RobloxPlayerBeta"))
        {
            pids.Add((uint)process.Id);
            process.Dispose();
        }

        var byProcess = new List<nint>();
        var byTitle = new List<nint>();
        EnumWindows((hwnd, _) =>
        {
            if (IsWindowVisible(hwnd) && IsGameSized(hwnd))
            {
                GetWindowThreadProcessId(hwnd, out var pid);
                if (pids.Contains(pid))
                    byProcess.Add(hwnd);
                else if (IsStoreRoblox(hwnd))
                    byTitle.Add(hwnd);
            }
            return true;
        }, 0);

        var found = byProcess.Count > 0 ? byProcess : byTitle;
        found.Sort();
        return found;
    }

    static bool IsGameSized(nint hwnd)
    {
        if (IsIconic(hwnd))
            return true;
        return GetWindowRect(hwnd, out var r) && r.Right - r.Left >= MinWidth && r.Bottom - r.Top >= MinHeight;
    }

    static bool IsStoreRoblox(nint hwnd)
    {
        var text = new StringBuilder(256);
        GetClassName(hwnd, text, text.Capacity);
        if (text.ToString() != "WINDOWSCLIENT")
            return false;
        text.Clear();
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString().StartsWith("Roblox", StringComparison.OrdinalIgnoreCase);
    }

    public static int NextIndex(int activeIndex, int count) => activeIndex < 0 ? 0 : (activeIndex + 1) % count;

    public static async Task ActivateNext(int hotkeyVirtualKey)
    {
        var windows = FindAll();
        if (windows.Count == 0)
            return;
        var foreground = GetForegroundWindow();
        var target = windows[NextIndex(windows.IndexOf(foreground), windows.Count)];
        if (target == foreground)
            return;

        var held = CarryKeys.Where(k => k != hotkeyVirtualKey && PhysicallyDown[k]).ToList();
        foreach (var key in held)
            SendKey(key, true);

        if (IsIconic(target))
            ShowWindow(target, SwRestore);
        BringToFront(target, foreground);
        for (var waited = 0; waited < 1000 && GetForegroundWindow() != target; waited += 50)
            await Task.Delay(50);
        await Task.Delay(50);

        foreach (var key in held.Where(k => PhysicallyDown[k]))
            SendKey(key, false);
    }

    static void BringToFront(nint target, nint foreground)
    {
        var mine = GetCurrentThreadId();
        var theirs = GetWindowThreadProcessId(foreground, out _);
        var attached = theirs != 0 && theirs != mine && AttachThreadInput(mine, theirs, true);
        SetForegroundWindow(target);
        if (attached)
            AttachThreadInput(mine, theirs, false);
    }

    static void SendKey(int virtualKey, bool up) =>
        keybd_event((byte)virtualKey, (byte)MapVirtualKey((uint)virtualKey, 0), up ? KeyUp : 0, 0);

    public static void TrackPhysicalKeys()
    {
        if (_hookThread != null)
            return;
        _hookThread = new Thread(RunKeyboardHook) { IsBackground = true, Name = "whub keyboard" };
        _hookThread.Start();
    }

    static void RunKeyboardHook()
    {
        SetWindowsHookEx(WhKeyboardLl, KeyboardCallback, GetModuleHandle(null), 0);
        var message = new byte[64];
        while (GetMessage(message, 0, 0, 0) > 0)
        {
        }
    }

    static nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var key = Marshal.ReadInt32(data, 0);
            var flags = Marshal.ReadInt32(data, 8);
            if ((flags & InjectedFlag) == 0 && key is >= 0 and < 256)
            {
                if (message is WmKeyDown or WmSysKeyDown)
                    PhysicallyDown[key] = true;
                else if (message is WmKeyUp or WmSysKeyUp)
                    PhysicallyDown[key] = false;
            }
        }
        return CallNextHookEx(0, code, message, data);
    }

    delegate bool EnumWindowsProc(nint hwnd, nint param);
    delegate nint LowLevelKeyboardProc(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32")] static extern bool EnumWindows(EnumWindowsProc callback, nint param);
    [DllImport("user32")] static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32")] static extern bool IsIconic(nint hwnd);
    [DllImport("user32")] static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetClassName(nint hwnd, StringBuilder text, int size);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint hwnd, StringBuilder text, int size);
    [DllImport("user32")] static extern nint GetForegroundWindow();
    [DllImport("user32")] static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32")] static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32")] static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
    [DllImport("user32")] static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32")] static extern nint SetWindowsHookEx(int id, LowLevelKeyboardProc callback, nint module, uint thread);
    [DllImport("user32")] static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32")] static extern int GetMessage(byte[] message, nint hwnd, uint min, uint max);
    [DllImport("user32")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("kernel32")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern nint GetModuleHandle(string? name);
}
