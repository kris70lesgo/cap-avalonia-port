using System;
using System.Runtime.InteropServices;

namespace CapAvalonia;

internal static class MacScreenCaptureAccess
{
    private const string CoreGraphicsPath = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    public static bool HasPermission
    {
        get
        {
            if (!OperatingSystem.IsMacOS())
                return false;

            try { return CGPreflightScreenCaptureAccess(); }
            catch { return false; }
        }
    }

    public static void EnsureAvailable()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var permitted = HasPermission;
        if (!permitted)
            permitted = CGRequestScreenCaptureAccess();

        if (!permitted)
            throw new UnauthorizedAccessException(
                "Screen capture access is off for Cap Avalonia. Enable it in System Settings → Privacy & Security → Screen & System Audio Recording, then restart the app.");
    }

    [DllImport(CoreGraphicsPath, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGPreflightScreenCaptureAccess();

    [DllImport(CoreGraphicsPath, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRequestScreenCaptureAccess();
}
