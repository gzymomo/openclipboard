using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace OpenClipboard.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private EventLoopGlobalHook? _hook;
    private Task? _runTask;
    private CancellationTokenSource? _permissionRetry;
    private bool _accessibilityPromptShown;
    private int _generation;
    private long _lastActivationMilliseconds;

    public event Action<string>? ErrorOccurred;

    public void Start(string activationHotkey, string quickCopyHotkey, Action activate, Action quickCopy)
    {
        HotkeyBinding activation;
        try
        {
            activation = HotkeyBinding.Parse(activationHotkey);
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(exception.Message);
            return;
        }

        HotkeyBinding? quickCopyBinding = null;
        if (!string.IsNullOrWhiteSpace(quickCopyHotkey))
        {
            try
            {
                quickCopyBinding = HotkeyBinding.Parse(quickCopyHotkey);
            }
            catch (Exception exception)
            {
                ErrorOccurred?.Invoke(exception.Message);
            }
        }
        if (quickCopyBinding is not null && activation == quickCopyBinding.Value)
        {
            ErrorOccurred?.Invoke("The activation and quick-copy shortcuts must be different.");
            quickCopyBinding = null;
        }

        StopCurrentHook();
        StartHook(activation, quickCopyBinding, activate, quickCopy);
    }

    private void StartHook(HotkeyBinding activation, HotkeyBinding? quickCopyBinding, Action activate, Action quickCopy)
    {
        // On macOS a global hook uses CGEventTap, which is silently blocked until
        // the process is trusted as an accessibility client. Without this check
        // the hook looks started but never receives any key event.
        if (OperatingSystem.IsMacOS() && !MacAccessibility.IsTrusted(prompt: true))
        {
            ErrorOccurred?.Invoke("Global hotkeys need Accessibility permission. Allow OpenClipboard in the system dialog, or enable it under System Settings → Privacy & Security → Accessibility. Hotkeys start automatically once granted.");
            WatchForAccessibilityPermission(activation, quickCopyBinding, activate, quickCopy);
            return;
        }

        try
        {
            var hook = new EventLoopGlobalHook();
            _hook = hook;
            var generation = ++_generation;
            hook.KeyPressed += (_, args) =>
            {
                try
                {
                    if (activation.Matches(args.Data.KeyCode, args.RawEvent.Mask))
                    {
                        InvokeDebounced(activate);
                    }
                    else if (quickCopyBinding?.Matches(args.Data.KeyCode, args.RawEvent.Mask) == true)
                    {
                        InvokeDebounced(quickCopy);
                    }
                }
                catch (Exception exception)
                {
                    ErrorOccurred?.Invoke(exception.Message);
                }
            };

            var runTask = hook.RunAsync();
            _runTask = runTask;
            _ = ObserveAsync(hook, generation, runTask);
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(exception.Message);
            _hook?.Dispose();
            _hook = null;
            _runTask = null;
        }
    }

    private void WatchForAccessibilityPermission(HotkeyBinding activation, HotkeyBinding? quickCopyBinding, Action activate, Action quickCopy)
    {
        var cancellation = new CancellationTokenSource();
        _permissionRetry = cancellation;

        if (!_accessibilityPromptShown)
        {
            _accessibilityPromptShown = true;
            try
            {
                Process.Start(new ProcessStartInfo(
                    "open", "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
                { UseShellExecute = false });
            }
            catch
            {
                // Opening Settings is a convenience; the message above still tells the user where to go.
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    await Task.Delay(2000, cancellation.Token);
                    if (!MacAccessibility.IsTrusted(prompt: false))
                    {
                        continue;
                    }

                    if (!cancellation.IsCancellationRequested)
                    {
                        StartHook(activation, quickCopyBinding, activate, quickCopy);
                    }
                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ErrorOccurred?.Invoke(exception.Message);
            }
        }, cancellation.Token);
    }

    private void StopCurrentHook()
    {
        _permissionRetry?.Cancel();
        _permissionRetry = null;
        var hook = _hook;
        _hook = null;
        _runTask = null;
        if (hook is not null)
        {
            try
            {
                hook.Dispose();
            }
            catch
            {
                // Best effort; the hook is being replaced or the service is shutting down.
            }
        }
    }

    private void InvokeDebounced(Action action)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastActivationMilliseconds);
        if (last != 0 && now - last < 350)
        {
            return;
        }

        Interlocked.Exchange(ref _lastActivationMilliseconds, now);
        try
        {
            action();
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(exception.Message);
        }
    }

    private async Task ObserveAsync(EventLoopGlobalHook hook, int generation, Task runTask)
    {
        try
        {
            await runTask;
            // Only report unexpected stops for the active hook. Disposing/restarting a hook stops it intentionally.
            if (generation == _generation && ReferenceEquals(_hook, hook))
            {
                ErrorOccurred?.Invoke("Global hotkey listener stopped.");
            }
        }
        catch (Exception exception)
        {
            if (generation == _generation && ReferenceEquals(_hook, hook))
            {
                ErrorOccurred?.Invoke(exception.Message);
            }
        }
    }

    public void Dispose()
    {
        _generation++;
        StopCurrentHook();
    }

    private static class MacAccessibility
    {
        private const string CoreFoundation =
            "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string ApplicationServices =
            "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
        private const int KCfStringEncodingUtf8 = 0x08000100;

        [DllImport(ApplicationServices)]
        private static extern bool AXIsProcessTrusted();

        [DllImport(ApplicationServices)]
        private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string cString, int encoding);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDictionaryCreate(
            IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint count,
            IntPtr keyCallbacks, IntPtr valueCallbacks);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr cf);

        public static bool IsTrusted(bool prompt)
        {
            if (!prompt)
            {
                return AXIsProcessTrusted();
            }

            // AXIsProcessTrustedWithOptions prompts macOS to register this exact
            // app (and shows the system dialog) instead of relying on a stale TCC
            // entry that no longer matches an ad-hoc-signed build.
            try
            {
                var coreFoundation = NativeLibrary.Load(CoreFoundation);
                var keyCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryKeyCallBacks");
                var valueCallbacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryValueCallBacks");
                var booleanTrue = Marshal.ReadIntPtr(NativeLibrary.GetExport(coreFoundation, "kCFBooleanTrue"));

                var key = CFStringCreateWithCString(IntPtr.Zero, "AXTrustedCheckOptionPrompt", KCfStringEncodingUtf8);
                if (key == IntPtr.Zero)
                {
                    return AXIsProcessTrusted();
                }

                try
                {
                    var options = CFDictionaryCreate(
                        IntPtr.Zero, new[] { key }, new[] { booleanTrue }, 1, keyCallbacks, valueCallbacks);
                    if (options == IntPtr.Zero)
                    {
                        return AXIsProcessTrusted();
                    }

                    try
                    {
                        return AXIsProcessTrustedWithOptions(options);
                    }
                    finally
                    {
                        CFRelease(options);
                    }
                }
                finally
                {
                    CFRelease(key);
                }
            }
            catch
            {
                // Older OS or a missing symbol: fall back to the non-prompting check.
                return AXIsProcessTrusted();
            }
        }
    }

    [Flags]
    private enum ModifierKeys
    {
        None = 0,
        Shift = 1,
        Ctrl = 2,
        Alt = 4,
        Meta = 8
    }

    private readonly record struct HotkeyBinding(KeyCode Key, ModifierKeys Modifiers)
    {

        public bool Matches(KeyCode key, EventMask modifiers)
        {
            return key == Key && Normalize(modifiers) == Modifiers;
        }

        private static ModifierKeys Normalize(EventMask modifiers)
        {
            var normalized = ModifierKeys.None;
            var text = modifiers.ToString();

            if (HasAny(modifiers, text, "Shift", EventMask.Shift)) normalized |= ModifierKeys.Shift;
            if (HasAny(modifiers, text, "Ctrl", EventMask.Ctrl, "Control")) normalized |= ModifierKeys.Ctrl;
            if (HasAny(modifiers, text, "Alt", EventMask.Alt)) normalized |= ModifierKeys.Alt;
            if (HasAny(modifiers, text, "Meta", EventMask.Meta, "Win", "Cmd", "Command", "Super")) normalized |= ModifierKeys.Meta;

            return normalized;
        }

        private static bool HasAny(EventMask modifiers, string text, string primaryName, EventMask primaryFlag, params string[] aliases)
        {
            try
            {
                if ((modifiers & primaryFlag) == primaryFlag && primaryFlag != EventMask.None)
                {
                    return true;
                }
            }
            catch
            {
                // Fall back to name matching if enum flags behave unexpectedly.
            }

            if (text.Contains(primaryName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var alias in aliases)
            {
                if (text.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static HotkeyBinding Parse(string shortcut)
        {
            var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                throw new ArgumentException("A keyboard shortcut is required.", nameof(shortcut));
            }

            var modifiers = ModifierKeys.None;
            for (var index = 0; index < parts.Length - 1; index++)
            {
                modifiers |= parts[index].ToUpperInvariant() switch
                {
                    "CTRL" or "CONTROL" => ModifierKeys.Ctrl,
                    "SHIFT" => ModifierKeys.Shift,
                    "ALT" or "OPTION" => ModifierKeys.Alt,
                    "META" or "WIN" or "CMD" or "COMMAND" => ModifierKeys.Meta,
                    _ => throw new ArgumentException($"Unsupported shortcut modifier '{parts[index]}'.", nameof(shortcut))
                };
            }

            if (!Enum.TryParse<KeyCode>("Vc" + parts[^1], ignoreCase: true, out var key)
                || key is KeyCode.VcUndefined or KeyCode.VcLeftShift or KeyCode.VcRightShift
                    or KeyCode.VcLeftControl or KeyCode.VcRightControl or KeyCode.VcLeftAlt
                    or KeyCode.VcRightAlt or KeyCode.VcLeftMeta or KeyCode.VcRightMeta)
            {
                throw new ArgumentException($"Unsupported shortcut key '{parts[^1]}'.", nameof(shortcut));
            }

            return new HotkeyBinding(key, modifiers);
        }
    }
}
