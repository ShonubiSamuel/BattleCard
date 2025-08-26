using System;

public static class InputLockService
{
    private static int _depth;
    public static bool IsLocked => _depth > 0;

    public static event Action<bool> OnChanged;

    public static IDisposable Acquire()
    {
        _depth++;
        if (_depth == 1) { OnChanged?.Invoke(true); UIInputBlocker.SetBlocked(true); }
        return new Token();
    }

    public static void PushLock()
    {
        _depth++;
        if (_depth == 1) { OnChanged?.Invoke(true); UIInputBlocker.SetBlocked(true); }
    }

    public static void PopLock()
    {
        if (_depth <= 0) return;
        _depth--;
        if (_depth == 0) { OnChanged?.Invoke(false); UIInputBlocker.SetBlocked(false); }
    }

    private sealed class Token : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            PopLock();
        }
    }
}