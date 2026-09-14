using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniFloat {
internal static class Native {
    internal const int GWL_EXSTYLE = -20, WS_EX_TOPMOST = 8, WS_EX_TRANSPARENT = 0x20;
    internal const uint SWP_NOACTIVATE = 0x10, SWP_NOZORDER = 4;
    [StructLayout(LayoutKind.Sequential)] internal struct RECT {
        public int Left, Top, Right, Bottom;
        public RECT(int x, int y, int w, int h) { Left=x; Top=y; Right=x+w; Bottom=y+h; }
        public int Width { get { return Right-Left; } }
        public int Height { get { return Bottom-Top; } }
    }
    [StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct MINMAXINFO {
        public POINT Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct THUMBNAIL {
        public uint Flags;
        public RECT Destination, Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct PROCESSENTRY32 {
        public uint Size, Usage, ProcessId;
        public UIntPtr DefaultHeap;
        public uint ModuleId, Threads, ParentId;
        public int BasePriority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string Exe;
    }
    internal delegate bool EnumCallback(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumCallback callback, IntPtr data);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint color,byte alpha,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetLayeredWindowAttributes(IntPtr hwnd,out uint color,out byte alpha,out uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref THUMBNAIL properties);
    [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail, out SIZE size);
    [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern bool Process32First(IntPtr handle, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern bool Process32Next(IntPtr handle, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

    internal static bool IsSupportedBrowser(string executable) {
        return String.Equals(executable,"chrome.exe",StringComparison.OrdinalIgnoreCase)
            || String.Equals(executable,"msedge.exe",StringComparison.OrdinalIgnoreCase);
    }
    internal static int BrowserAncestor() {
        var parents = new Dictionary<uint, PROCESSENTRY32>();
        IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return 0;
        try {
            var entry = new PROCESSENTRY32(); entry.Size = (uint)Marshal.SizeOf(entry);
            if (Process32First(snapshot, ref entry)) do { parents[entry.ProcessId]=entry; } while (Process32Next(snapshot, ref entry));
        } finally { CloseHandle(snapshot); }
        uint current = (uint)Process.GetCurrentProcess().Id;
        for (int i=0; i<12; i++) {
            PROCESSENTRY32 entry;
            if (!parents.TryGetValue(current, out entry)) break;
            if (IsSupportedBrowser(entry.Exe)) return (int)current;
            if (current == entry.ParentId) break;
            current = entry.ParentId;
        }
        return 0;
    }
    internal static List<IntPtr> BrowserWindows(int pid, bool topOnly) {
        var found = new List<IntPtr>();
        EnumWindows(delegate(IntPtr hwnd, IntPtr data) {
            uint owner; GetWindowThreadProcessId(hwnd, out owner);
            if (owner != pid || !IsWindowVisible(hwnd)) return true;
            var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity);
            if (!name.ToString().StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal)) return true;
            if (topOnly && (GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOPMOST)==0) return true;
            found.Add(hwnd); return true;
        }, IntPtr.Zero);
        return found;
    }
}
}
