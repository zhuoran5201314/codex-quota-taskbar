using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CodexQuotaDashboard;

internal static class NativeGlass
{
    private const int WcaAccentPolicy = 19;

    public static void Apply(Window window, double opacity, int radius)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var accent = new AccentPolicy
        {
            AccentState = AccentState.EnableAcrylicBlurBehind,
            AccentFlags = 2,
            GradientColor = ((byte)(Math.Clamp(opacity, 0.35, 0.98) * 255) << 24) | 0x181A20
        };
        var size = Marshal.SizeOf<AccentPolicy>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, pointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                SizeOfData = size,
                Data = pointer
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(pointer); }

        var rect = new Rect();
        GetWindowRect(hwnd, ref rect);
        var region = CreateRoundRectRgn(0, 0, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1, radius, radius);
        SetWindowRgn(hwnd, region, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private enum AccentState
    {
        Disabled,
        EnableGradient,
        EnableTransparentGradient,
        EnableBlurBehind,
        EnableAcrylicBlurBehind
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, ref Rect rect);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
}
