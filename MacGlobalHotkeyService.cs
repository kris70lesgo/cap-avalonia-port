using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace CapAvalonia;

internal sealed class MacGlobalHotkeyService : IDisposable
{
    private const string CarbonPath = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const uint KeyboardEventClass = 0x6b657962; // 'keyb'
    private const uint HotKeyPressedEvent = 5;
    private const uint DirectObjectParameter = 0x2d2d2d2d; // '----'
    private const uint HotKeyIdType = 0x686b6964; // 'hkid'
    private const uint NoHotKeyOptions = 0;
    private const uint CommandModifier = 1 << 8;
    private const uint ShiftModifier = 1 << 9;
    private const uint OptionModifier = 1 << 11;
    private const uint ControlModifier = 1 << 12;

    private static readonly (string Name, string PreferenceKey)[] Bindings =
    {
        ("screenshot.display", "screenshot.display"),
        ("screenshot.window", "screenshot.window"),
        ("screenshot.area", "screenshot.area"),
        ("recording.picker", "recording.picker"),
        ("recording.stop", "recording.stop"),
        ("recording.restart", "recording.restart"),
        ("recording.pause", "recording.pause"),
        ("recording.cycle", "recording.cycle"),
        ("recording.display", "recording.display"),
        ("recording.window", "recording.window"),
        ("recording.area", "recording.area")
    };

    private static readonly EventHandlerProc CarbonEventHandler = HandleCarbonEvent;
    private static MacGlobalHotkeyService? _current;

    private readonly object _gate = new();
    private readonly Action<string> _onShortcut;
    private readonly List<IntPtr> _registeredHotkeys = new();
    private readonly Dictionary<uint, string> _actionByHotkeyId = new();
    private IntPtr _eventHandler;
    private uint _nextId;
    private bool _disposed;

    public MacGlobalHotkeyService(Action<string> onShortcut)
    {
        _onShortcut = onShortcut;
        if (!OperatingSystem.IsMacOS())
            return;

        _current = this;
        var eventTypes = new[] { new EventTypeSpec(KeyboardEventClass, HotKeyPressedEvent) };
        var status = InstallEventHandler(GetApplicationEventTarget(), CarbonEventHandler,
            (uint)eventTypes.Length, eventTypes, IntPtr.Zero, out _eventHandler);
        if (status != 0)
            throw new InvalidOperationException($"Could not install macOS global shortcut handling (Carbon error {status}).");

        Refresh();
    }

    public static void SuspendCurrent() => _current?.ClearRegistrations();

    public static string? RefreshCurrent() => _current?.Refresh();

    public string? Refresh()
    {
        if (!OperatingSystem.IsMacOS() || _disposed)
            return null;

        ClearRegistrations();
        var failures = new List<string>();
        var seenCombinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, preferenceKey) in Bindings)
        {
            var binding = AppPreferencesStore.Get("shortcut." + preferenceKey);
            if (string.IsNullOrWhiteSpace(binding) || binding.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryParse(binding, out var keyCode, out var modifiers))
            {
                failures.Add($"{binding} is not supported as a system shortcut.");
                continue;
            }

            var combination = $"{keyCode}:{modifiers}";
            if (!seenCombinations.Add(combination))
            {
                failures.Add($"{binding} is assigned to more than one action.");
                continue;
            }

            var id = ++_nextId;
            var hotkeyId = new EventHotKeyId(FourCC("CAPA"), id);
            var status = RegisterEventHotKey(keyCode, modifiers, hotkeyId, GetApplicationEventTarget(),
                NoHotKeyOptions, out var hotkeyRef);
            if (status != 0)
            {
                failures.Add($"{binding} could not be registered (Carbon error {status}).");
                continue;
            }

            lock (_gate)
            {
                _registeredHotkeys.Add(hotkeyRef);
                _actionByHotkeyId[id] = name;
            }
        }

        if (failures.Count > 0)
            return string.Join(" ", failures);
        var count = _registeredHotkeys.Count;
        return count == 1 ? "1 system-wide shortcut is registered." : $"{count} system-wide shortcuts are registered.";
    }

    private void ClearRegistrations()
    {
        lock (_gate)
        {
            foreach (var hotkey in _registeredHotkeys)
                _ = UnregisterEventHotKey(hotkey);
            _registeredHotkeys.Clear();
            _actionByHotkeyId.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ClearRegistrations();
        if (_eventHandler != IntPtr.Zero)
        {
            _ = RemoveEventHandler(_eventHandler);
            _eventHandler = IntPtr.Zero;
        }
        if (ReferenceEquals(_current, this))
            _current = null;
    }

    private static int HandleCarbonEvent(IntPtr nextHandler, IntPtr currentEvent, IntPtr userData)
    {
        var requestedSize = (uint)Marshal.SizeOf<EventHotKeyId>();
        var status = GetEventParameter(currentEvent, DirectObjectParameter, HotKeyIdType,
            out _, requestedSize, out _, out var eventHotKeyId);
        if (status == 0 && _current is { } current)
        {
            string? action;
            lock (current._gate)
                current._actionByHotkeyId.TryGetValue(eventHotKeyId.Id, out action);
            if (action is not null)
            {
                try { current._onShortcut(action); }
                catch { }
            }
        }
        return 0;
    }

    private static bool TryParse(string binding, out uint keyCode, out uint modifiers)
    {
        keyCode = 0;
        modifiers = 0;
        var parts = binding.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !TryGetMacKeyCode(parts[^1], out keyCode))
            return false;

        foreach (var part in parts.Take(parts.Length - 1))
        {
            switch (part)
            {
                case "Primary":
                case "Meta": modifiers |= CommandModifier; break;
                case "Control": modifiers |= ControlModifier; break;
                case "Shift": modifiers |= ShiftModifier; break;
                case "Alt": modifiers |= OptionModifier; break;
                default: return false;
            }
        }
        return modifiers != 0;
    }

    private static bool TryGetMacKeyCode(string name, out uint keyCode)
    {
        keyCode = name switch
        {
            "A" => 0, "S" => 1, "D" => 2, "F" => 3, "H" => 4, "G" => 5, "Z" => 6,
            "X" => 7, "C" => 8, "V" => 9, "B" => 11, "Q" => 12, "W" => 13, "E" => 14,
            "R" => 15, "Y" => 16, "T" => 17, "D1" => 18, "D2" => 19, "D3" => 20,
            "D4" => 21, "D6" => 22, "D5" => 23, "D9" => 25, "D7" => 26, "D8" => 28,
            "D0" => 29, "O" => 31, "U" => 32, "I" => 34, "P" => 35, "L" => 37, "J" => 38,
            "K" => 40, "N" => 45, "M" => 46, "Space" => 49, "Tab" => 48, "Return" => 36,
            "Enter" => 76, "Escape" => 53, "Back" => 51, "Delete" => 117, "Left" => 123,
            "Right" => 124, "Down" => 125, "Up" => 126, "F1" => 122, "F2" => 120,
            "F3" => 99, "F4" => 118, "F5" => 96, "F6" => 97, "F7" => 98, "F8" => 100,
            "F9" => 101, "F10" => 109, "F11" => 103, "F12" => 111, "OemPlus" => 24,
            "OemMinus" => 27, "OemComma" => 43, "OemPeriod" => 47, "OemQuestion" => 44,
            _ => uint.MaxValue
        };
        return keyCode != uint.MaxValue;
    }

    private static uint FourCC(string value) =>
        ((uint)value[0] << 24) | ((uint)value[1] << 16) | ((uint)value[2] << 8) | value[3];

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct EventTypeSpec(uint eventClass, uint eventKind)
    {
        public readonly uint EventClass = eventClass;
        public readonly uint EventKind = eventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct EventHotKeyId(uint signature, uint id)
    {
        public readonly uint Signature = signature;
        public readonly uint Id = id;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EventHandlerProc(IntPtr nextHandler, IntPtr currentEvent, IntPtr userData);

    [DllImport(CarbonPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(CarbonPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern int InstallEventHandler(IntPtr target, EventHandlerProc handler, uint numTypes,
        [In] EventTypeSpec[] eventTypes, IntPtr userData, out IntPtr handlerRef);

    [DllImport(CarbonPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RemoveEventHandler(IntPtr handlerRef);

    [DllImport(CarbonPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyId hotkeyId,
        IntPtr target, uint options, out IntPtr hotkeyRef);

    [DllImport(CarbonPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern int UnregisterEventHotKey(IntPtr hotkeyRef);

    [DllImport(CarbonPath, CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetEventParameter(IntPtr currentEvent, uint name, uint desiredType,
        out uint actualType, uint bufferSize, out uint actualSize, out EventHotKeyId data);
}
