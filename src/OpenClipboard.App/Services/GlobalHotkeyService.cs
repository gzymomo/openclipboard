using System;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace OpenClipboard.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private EventLoopGlobalHook? _hook;
    private long _lastActivationMilliseconds;

    public event Action<string>? ErrorOccurred;

    public void Start(string activationHotkey, string quickCopyHotkey, Action activate, Action quickCopy)
    {
        var activation = HotkeyBinding.Parse(activationHotkey);
        HotkeyBinding? quickCopyBinding = string.IsNullOrWhiteSpace(quickCopyHotkey)
            ? null
            : HotkeyBinding.Parse(quickCopyHotkey);
        if (quickCopyBinding is not null && activation == quickCopyBinding.Value)
        {
            throw new ArgumentException("The activation and quick-copy shortcuts must be different.");
        }

        Dispose();

        _hook = new EventLoopGlobalHook();
        _hook.KeyPressed += (_, args) =>
        {
            if (activation.Matches(args.Data.KeyCode, args.RawEvent.Mask))
            {
                InvokeDebounced(activate);
            }
            else if (quickCopyBinding?.Matches(args.Data.KeyCode, args.RawEvent.Mask) == true)
            {
                InvokeDebounced(quickCopy);
            }
        };

        try
        {
            _ = ObserveAsync(_hook.RunAsync(GlobalHookType.Keyboard));
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(exception.Message);
            _hook.Dispose();
            _hook = null;
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
        action();
    }

    private async Task ObserveAsync(Task runTask)
    {
        try
        {
            await runTask;
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(exception.Message);
        }
    }

    public void Dispose()
    {
        _hook?.Dispose();
        _hook = null;
    }

    private readonly record struct HotkeyBinding(KeyCode Key, EventMask Modifiers)
    {
        public bool Matches(KeyCode key, EventMask modifiers)
        {
            const EventMask modifierMask = EventMask.Shift | EventMask.Ctrl | EventMask.Alt | EventMask.Meta;
            return key == Key && (modifiers & modifierMask) == Modifiers;
        }

        public static HotkeyBinding Parse(string shortcut)
        {
            var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                throw new ArgumentException("A keyboard shortcut is required.", nameof(shortcut));
            }

            var modifiers = EventMask.None;
            for (var index = 0; index < parts.Length - 1; index++)
            {
                modifiers |= parts[index].ToUpperInvariant() switch
                {
                    "CTRL" or "CONTROL" => EventMask.Ctrl,
                    "SHIFT" => EventMask.Shift,
                    "ALT" or "OPTION" => EventMask.Alt,
                    "META" or "WIN" or "CMD" or "COMMAND" => EventMask.Meta,
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
