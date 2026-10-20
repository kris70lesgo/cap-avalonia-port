using System;
using System.Runtime.InteropServices;

namespace CapAvalonia;

internal static class MacScreenCaptureAccess
{
    private const string CoreGraphicsPath = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    public static string? PermissionCheckError { get; private set; }

    public static bool HasPermission
    {
        get
        {
            if (!OperatingSystem.IsMacOS())
                return false;

            try
            {
                var permitted = CGPreflightScreenCaptureAccess();
                PermissionCheckError = null;
                return permitted;
            }
            catch (Exception error)
            {
                PermissionCheckError = $"{error.GetType().Name}: {error.Message}";
                return false;
            }
        }
    }

    public static void EnsureAvailable()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var permitted = HasPermission;
        if (!permitted)
        {
            try
            {
                permitted = CGRequestScreenCaptureAccess();
                if (permitted)
                    PermissionCheckError = null;
            }
            catch (Exception error)
            {
                PermissionCheckError = $"{error.GetType().Name}: {error.Message}";
            }
        }

        if (!permitted)
            throw new UnauthorizedAccessException(
                "macOS is blocking screen capture for this Cap Avalonia build. In System Settings → Privacy & Security → Screen & System Audio Recording, turn on this build's entry. If it is already on, turn it off and on, then fully quit and reopen the app. Ad hoc development signing can make permission grants stop applying after a rebuild." +
                (string.IsNullOrWhiteSpace(PermissionCheckError) ? string.Empty : $" Permission check detail: {PermissionCheckError}"));
    }

    [DllImport(CoreGraphicsPath, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGPreflightScreenCaptureAccess();

    [DllImport(CoreGraphicsPath, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRequestScreenCaptureAccess();
}
