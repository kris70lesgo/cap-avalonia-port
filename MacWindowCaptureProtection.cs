using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace CapAvalonia;

internal static class MacWindowCaptureProtection
{
    private const string ObjectiveCRuntime = "/usr/lib/libobjc.A.dylib";

    public static void ExcludeFromCapture(Window window)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero || handle.HandleDescriptor != "NSWindow")
            return;

        try
        {
            var selector = sel_registerName("setSharingType:");
            SendSharingType(handle.Handle, selector, 0); // NSWindowSharingNone
        }
        catch
        {
            // Capture exclusion is best-effort; it must never block recording controls.
        }
    }

    [DllImport(ObjectiveCRuntime, EntryPoint = "sel_registerName", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SendSharingType(IntPtr receiver, IntPtr selector, nint sharingType);
}
