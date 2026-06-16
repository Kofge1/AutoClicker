using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoClickerCs;

internal static class NativeMethods
{
    internal const int WhKeyboardLl = 13;
    internal const int WhMouseLl = 14;

    internal const int WmKeyDown = 0x0100;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmSysKeyUp = 0x0105;
    internal const int WmLButtonDown = 0x0201;
    internal const int WmLButtonUp = 0x0202;
    internal const int WmRButtonDown = 0x0204;
    internal const int WmRButtonUp = 0x0205;
    internal const int WmMButtonDown = 0x0207;
    internal const int WmMButtonUp = 0x0208;
    internal const int WmXButtonDown = 0x020B;
    internal const int WmXButtonUp = 0x020C;

    internal const int LlkhfInjected = 0x10;
    internal const int LlmhfInjected = 0x00000001;

    internal const uint MouseeventfLeftDown = 0x0002;
    internal const uint MouseeventfLeftUp = 0x0004;
    internal const uint MouseeventfRightDown = 0x0008;
    internal const uint MouseeventfRightUp = 0x0010;
    internal const uint MouseeventfMiddleDown = 0x0020;
    internal const uint MouseeventfMiddleUp = 0x0040;
    internal const uint MouseeventfXDown = 0x0080;
    internal const uint MouseeventfXUp = 0x0100;
    internal const uint XButton1MouseData = 0x0001;
    internal const uint XButton2MouseData = 0x0002;

    internal const int VkLButton = 0x01;
    internal const int VkRButton = 0x02;
    internal const int VkCancel = 0x03;
    internal const int VkMButton = 0x04;
    internal const int VkXButton1 = 0x05;
    internal const int VkXButton2 = 0x06;
    internal const int VkBack = 0x08;
    internal const int VkTab = 0x09;
    internal const int VkReturn = 0x0D;
    internal const int VkShift = 0x10;
    internal const int VkControl = 0x11;
    internal const int VkMenu = 0x12;
    internal const int VkPause = 0x13;
    internal const int VkCapsLock = 0x14;
    internal const int VkEscape = 0x1B;
    internal const int VkSpace = 0x20;
    internal const int VkPageUp = 0x21;
    internal const int VkPageDown = 0x22;
    internal const int VkEnd = 0x23;
    internal const int VkHome = 0x24;
    internal const int VkLeft = 0x25;
    internal const int VkUp = 0x26;
    internal const int VkRight = 0x27;
    internal const int VkDown = 0x28;
    internal const int VkPrintScreen = 0x2C;
    internal const int VkInsert = 0x2D;
    internal const int VkDelete = 0x2E;
    internal const int Vk0 = 0x30;
    internal const int Vk9 = 0x39;
    internal const int VkA = 0x41;
    internal const int VkZ = 0x5A;
    internal const int VkApps = 0x5D;
    internal const int VkNumpad0 = 0x60;
    internal const int VkNumpad9 = 0x69;
    internal const int VkF1 = 0x70;
    internal const int VkF24 = 0x87;
    internal const int VkNumLock = 0x90;
    internal const int VkScroll = 0x91;
    internal const int GwlExstyle = -20;
    internal const int WmNclbuttonUp = 0x00A2;
    internal const int WmSyscommand = 0x0112;
    internal const int ScMinimize = 0xF020;
    internal const int HtMinButton = 8;
    internal const uint WsExAppwindow = 0x00040000;
    internal const uint WsExToolwindow = 0x00000080;
    internal const uint SwpNosize = 0x0001;
    internal const uint SwpNomove = 0x0002;
    internal const uint SwpNozorder = 0x0004;
    internal const uint SwpFramechanged = 0x0020;
    internal const uint SwpNoactivate = 0x0010;
    internal static readonly nuint AutoClickerExtraInfo = unchecked((nuint)0xAC10C11C);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Kbdllhookstruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Msllhookstruct
    {
        public Point Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public nuint DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowInfo
    {
        public int Size;
        public RECT Window;
        public RECT Client;
        public uint Style;
        public uint ExStyle;
        public uint WindowStatus;
        public uint CxWindowBorders;
        public uint CyWindowBorders;
        public ushort AtomWindowType;
        public ushort CreatorVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder returnedString, int size, string filePath);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool WritePrivateProfileString(string section, string? key, string? value, string filePath);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("kernel32.dll")]
    internal static extern bool QueryPerformanceFrequency(out long lpFrequency);

    [DllImport("kernel32.dll")]
    internal static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    internal static extern uint TimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    internal static extern uint TimeEndPeriod(uint period);

    internal static string GetWindowTitle(IntPtr hwnd)
    {
        var builder = new StringBuilder(512);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    internal static string GetWindowClass(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        _ = GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    internal static string GetWindowProcessName(IntPtr hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static bool IsPressed(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }
}
