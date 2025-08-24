// InputLockService.cs
using System;

public static class InputLockService
{
    private static int _lockCount = 0;
    public static bool IsLocked => _lockCount > 0;

    public static IDisposable Acquire()
    {
        _lockCount++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_lockCount > 0) _lockCount--;
        }
    }
}