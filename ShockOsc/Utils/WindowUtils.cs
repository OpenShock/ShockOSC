using System.Drawing;
using System.Runtime.InteropServices;

namespace OpenShock.ShockOsc.Utils;

public static class WindowUtils
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowLongPtrA(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowLongPtrA(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    
    [DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool AdjustWindowRectEx(ref Rect lpRect, IntPtr dwStyle, bool bMenu, uint dwExStyle);
    
    [DllImport("dwmapi.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMarInset);
    
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, int X, int Y, int cx, int cy, uint uFlags);
}

public enum WindowLongFlags : int
{
    GWL_EXSTYLE = -20,
    GWLP_HINSTANCE = -6,
    GWLP_HWNDPARENT = -8,
    GWL_ID = -12,
    GWL_STYLE = -16,
    GWL_USERDATA = -21,
    GWL_WNDPROC = -4,
    DWLP_USER = 0x8,
    DWLP_MSGRESULT = 0x0,
    DWLP_DLGPROC = 0x4
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Rect {
    public int left;
    public int  top;
    public int right;
    public int bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Margins {
    public int cxLeftWidth;
    public int cxRightWidth;
    public int cyTopHeight;
    public int cyBottomHeight;
}