using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace CapAvalonia;

internal static class MacTitlebarButtonOffset
{
    private const string ObjectiveCRuntime = "/usr/lib/libobjc.A.dylib";
    private const double HorizontalOffset = 15;

    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero || handle.HandleDescriptor != "NSWindow")
            return;

        try
        {
            var standardWindowButton = sel_registerName("standardWindowButton:");
            var frameSelector = sel_registerName("frame");
            var setFrameOrigin = sel_registerName("setFrameOrigin:");

            for (nint buttonKind = 0; buttonKind < 3; buttonKind++)
            {
                var button = SendWindowButton(handle.Handle, standardWindowButton, buttonKind);
                if (button == IntPtr.Zero)
                    continue;

                var frame = SendRect(button, frameSelector);
                SendPoint(button, setFrameOrigin,
                    new NativePoint(frame.X + HorizontalOffset + ((double)buttonKind * 3), frame.Y));
            }
        }
        catch (Exception)
        {
            // Native titlebar positioning is cosmetic; keep the window usable if AppKit changes.
        }
    }

    [DllImport(ObjectiveCRuntime, EntryPoint = "sel_registerName", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SendWindowButton(IntPtr receiver, IntPtr selector, nint argument);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern NativeRect SendRect(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCRuntime, EntryPoint = "objc_msgSend", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SendPoint(IntPtr receiver, IntPtr selector, NativePoint argument);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(double x, double y)
    {
        public readonly double X = x;
        public readonly double Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect(double x, double y, double width, double height)
    {
        public readonly double X = x;
        public readonly double Y = y;
        public readonly double Width = width;
        public readonly double Height = height;
    }
}
