using System;
using System.Runtime.InteropServices;

namespace CapAvalonia;

internal static class MacFileTrash
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private static readonly IntPtr DefaultManagerSelector = Selector("defaultManager");
    private static readonly IntPtr StringFromUtf8Selector = Selector("stringWithUTF8String:");
    private static readonly IntPtr FileUrlSelector = Selector("fileURLWithPath:");
    private static readonly IntPtr TrashItemSelector = Selector("trashItemAtURL:resultingItemURL:error:");
    private static readonly IntPtr LocalizedDescriptionSelector = Selector("localizedDescription");
    private static readonly IntPtr Utf8StringSelector = Selector("UTF8String");

    public static void Move(string path)
    {
        var autoreleasePool = AutoreleasePoolPush();
        try
        {
            var managerClass = Class("NSFileManager");
            var manager = SendMessage(managerClass, DefaultManagerSelector);
            var urlClass = Class("NSURL");
            var stringClass = Class("NSString");
            var utf8Path = Marshal.StringToCoTaskMemUTF8(path);
            try
            {
                var nativePath = SendMessageWithArgument(stringClass, StringFromUtf8Selector, utf8Path);
                var fileUrl = SendMessageWithArgument(urlClass, FileUrlSelector, nativePath);
                if (manager == IntPtr.Zero || fileUrl == IntPtr.Zero)
                    throw new InvalidOperationException("macOS could not prepare this file for the Trash.");

                var moved = SendTrashItem(manager, TrashItemSelector, fileUrl, IntPtr.Zero, out var error);
                if (moved == 0)
                {
                    var description = error == IntPtr.Zero
                        ? IntPtr.Zero
                        : SendMessage(error, LocalizedDescriptionSelector);
                    var messagePointer = description == IntPtr.Zero
                        ? IntPtr.Zero
                        : SendMessage(description, Utf8StringSelector);
                    var message = messagePointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(messagePointer);
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                        ? "macOS could not move this file to the Trash."
                        : message);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8Path);
            }
        }
        finally
        {
            AutoreleasePoolPop(autoreleasePool);
        }
    }

    private static IntPtr Class(string name)
    {
        var result = GetClass(name);
        return result != IntPtr.Zero ? result : throw new InvalidOperationException($"macOS class {name} is unavailable.");
    }

    private static IntPtr Selector(string name)
    {
        var result = RegisterSelector(name);
        return result != IntPtr.Zero ? result : throw new InvalidOperationException($"macOS selector {name} is unavailable.");
    }

    [DllImport(ObjectiveC, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    [DllImport(ObjectiveC, EntryPoint = "sel_registerName")]
    private static extern IntPtr RegisterSelector(string name);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessage(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessageWithArgument(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern byte SendTrashItem(IntPtr receiver, IntPtr selector, IntPtr url, IntPtr resultingUrl, out IntPtr error);

    [DllImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPush")]
    private static extern IntPtr AutoreleasePoolPush();

    [DllImport(ObjectiveC, EntryPoint = "objc_autoreleasePoolPop")]
    private static extern void AutoreleasePoolPop(IntPtr pool);
}
