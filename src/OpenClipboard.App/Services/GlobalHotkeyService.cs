using System;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace OpenClipboard.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private EventLoopGlobalHook? _hook;
    private Task? _runTask;
    private long _lastActivationMilliseconds;

    public event Action<string>? ErrorOccurred;

    public void Start(Action activate)
    {
        if (_hook is not null)
        {
            return;
        }

        _hook = new EventLoopGlobalHook();
        _hook.KeyPressed += (_, args) =>
        {
            if (args.Data.KeyCode == KeyCode.VcF2)
            {
                var now = Environment.TickCount64;
                var last = Interlocked.Read(ref _lastActivationMilliseconds);
                if (last != 0 && now - last < 500)
                {
                    return;
                }

                Interlocked.Exchange(ref _lastActivationMilliseconds, now);
                activate();
            }
        };

        try
        {
            _runTask = _hook.RunAsync(GlobalHookType.Keyboard);
            _ = ObserveAsync(_runTask);
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(exception.Message);
            _hook.Dispose();
            _hook = null;
        }
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
}
