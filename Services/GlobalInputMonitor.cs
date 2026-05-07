using System.Diagnostics;
using System.Runtime.InteropServices;
using CECOMPuang.Helpers;

namespace CECOMPuang.Services;

public sealed class GlobalInputMonitor : IDisposable
{
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private readonly NativeMethods.LowLevelProc _keyboardProc;
    private readonly NativeMethods.LowLevelProc _mouseProc;
    private bool _disposed;

    public event Action? OnActivity;
    public event Action<int, int>? OnLeftDown;
    public event Action<int, int>? OnLeftClick;
    public event Action<int, int>? OnMouseMove;

    public GlobalInputMonitor()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public void Install()
    {
        var moduleHandle = NativeMethods.GetModuleHandle(
            Process.GetCurrentProcess().MainModule?.ModuleName);

        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);

        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
    }

    private static bool IsImeKey(uint vkCode) => vkCode is
        0x15 or
        0x19 or
        0xE5 or
        0x1F or
        0x1A;

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            if (!IsImeKey(kb.vkCode))
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                    OnActivity?.Invoke();
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN)
            {
                OnActivity?.Invoke();
            }

            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_LBUTTONUP
                or NativeMethods.WM_MOUSEMOVE)
            {
                var ms = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var (x, y) = (ms.pt.x, ms.pt.y);

                if (msg is NativeMethods.WM_LBUTTONDOWN)
                    OnLeftDown?.Invoke(x, y);
                else if (msg is NativeMethods.WM_MOUSEMOVE)
                    OnMouseMove?.Invoke(x, y);
                else
                    OnLeftClick?.Invoke(x, y);
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_keyboardHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
    }
}
