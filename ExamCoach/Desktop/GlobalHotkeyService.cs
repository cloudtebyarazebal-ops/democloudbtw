using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ExamCoachDesktop;

public enum GlobalHotkeyAction
{
    NextFragment,
    RevealToVs,
    PrevStep,
    NextStep,
    RevealTen
}

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNorepeat = 0x4000;

    private readonly Dictionary<int, GlobalHotkeyAction> _registered = new();
    private int _nextId = 0xA000;
    private HwndSource? _source;
    private bool _disposed;

    public event Action<GlobalHotkeyAction>? HotkeyPressed;

    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);

        TryRegister(ModifierKeys.None, Key.F8, GlobalHotkeyAction.NextFragment);
        TryRegister(ModifierKeys.Control, Key.F8, GlobalHotkeyAction.RevealTen);
        TryRegister(ModifierKeys.Control, Key.Right, GlobalHotkeyAction.RevealToVs);
        TryRegister(ModifierKeys.None, Key.F7, GlobalHotkeyAction.PrevStep);
        TryRegister(ModifierKeys.None, Key.F9, GlobalHotkeyAction.NextStep);
    }

    private void TryRegister(ModifierKeys modifiers, Key key, GlobalHotkeyAction action)
    {
        if (_source == null) return;

        var id = _nextId++;
        var mod = ModNorepeat | ToNativeModifiers(modifiers);
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (!RegisterHotKey(_source.Handle, id, mod, vk))
            return;

        _registered[id] = action;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        if (_registered.TryGetValue(wParam.ToInt32(), out var action))
        {
            HotkeyPressed?.Invoke(action);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint mod = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mod |= ModAlt;
        if (modifiers.HasFlag(ModifierKeys.Control)) mod |= ModControl;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mod |= ModShift;
        return mod;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_source != null)
        {
            foreach (var id in _registered.Keys)
                UnregisterHotKey(_source.Handle, id);
            _source.RemoveHook(WndProc);
        }

        _registered.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
