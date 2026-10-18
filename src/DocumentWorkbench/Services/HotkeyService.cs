using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DocumentWorkbench.Services;

/// <summary>
/// 全局快捷键（Win32 RegisterHotKey）。
/// 仅在用户明确启用后注册；应用退出时注销；注册失败会给出原因。
/// </summary>
public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008, MOD_NOREPEAT = 0x4000;
    private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    public const string SwitchToOffice = "SwitchToOffice";
    public const string ReturnToReading = "ReturnToReading";
    public const string OpenLibrary = "OpenLibrary";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private readonly Dictionary<int, string> _idToName = new();
    private int _nextId = 0xB100;

    /// <summary>全局热键触发，参数为热键名称。</summary>
    public event Action<string>? HotkeyPressed;

    /// <summary>解析 "Ctrl+Alt+O" 形式的快捷键。</summary>
    public static bool TryParseHotkey(string? text, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var p in parts[..^1])
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl":
                case "control": modifiers |= ModifierKeys.Control; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                case "win":
                case "windows": modifiers |= ModifierKeys.Windows; break;
                default: return false;
            }
        }

        if (!Enum.TryParse<Key>(parts[^1], ignoreCase: true, out key) || key == Key.None) return false;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return false;
        return true;
    }

    public static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>挂接到主窗口消息循环。</summary>
    public void Attach(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
    }

    /// <summary>注册一个全局热键。成功返回 null，失败返回用户可读原因。</summary>
    public string? Register(string name, string hotkeyText)
    {
        if (_source == null) return "窗口尚未就绪，无法注册全局快捷键";
        if (!TryParseHotkey(hotkeyText, out var mods, out var key))
            return $"无效的快捷键：{hotkeyText}";

        uint fs = MOD_NOREPEAT;
        if (mods.HasFlag(ModifierKeys.Alt)) fs |= MOD_ALT;
        if (mods.HasFlag(ModifierKeys.Control)) fs |= MOD_CONTROL;
        if (mods.HasFlag(ModifierKeys.Shift)) fs |= MOD_SHIFT;
        if (mods.HasFlag(ModifierKeys.Windows)) fs |= MOD_WIN;

        int id = _nextId++;
        if (!RegisterHotKey(_source.Handle, id, fs, (uint)KeyInterop.VirtualKeyFromKey(key)))
        {
            int err = Marshal.GetLastWin32Error();
            Logger.Warn($"全局快捷键注册失败：{hotkeyText}（Win32 错误 {err}）");
            return err == ERROR_HOTKEY_ALREADY_REGISTERED
                ? $"组合键 {hotkeyText} 已被其他程序占用，请换一个组合"
                : $"组合键 {hotkeyText} 注册失败（错误码 {err}）";
        }
        _idToName[id] = name;
        Logger.Info($"已注册全局快捷键：{name} = {hotkeyText}");
        return null;
    }

    public void UnregisterAll()
    {
        if (_source != null)
        {
            foreach (var id in _idToName.Keys)
                UnregisterHotKey(_source.Handle, id);
        }
        _idToName.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _idToName.TryGetValue(wParam.ToInt32(), out var name))
        {
            HotkeyPressed?.Invoke(name);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }
}
