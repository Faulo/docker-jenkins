using System;
using System.Threading;

namespace Agent;

static class AgentSupervisor {
    const string MUTEX_NAME = @"Global\docker-jenkins-agent-supervisor";

    public static IDisposable Acquire() => Acquire(MUTEX_NAME);

    public static bool IsRunning() => IsRunning(MUTEX_NAME);

    internal static IDisposable Acquire(string name) {
        var mutex = new Mutex(false, name);
        try {
            if (!TryAcquire(mutex)) {
                throw new InvalidOperationException("another managed Jenkins agent process is already running");
            }
            return new Lease(mutex);
        } catch {
            mutex.Dispose();
            throw;
        }
    }

    internal static bool IsRunning(string name) {
        Mutex mutex;
        try {
            mutex = Mutex.OpenExisting(name);
        } catch (WaitHandleCannotBeOpenedException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }

        using (mutex) {
            if (!TryAcquire(mutex)) {
                return true;
            }
            mutex.ReleaseMutex();
            return false;
        }
    }

    static bool TryAcquire(Mutex mutex) {
        try {
            return mutex.WaitOne(0);
        } catch (AbandonedMutexException) {
            return true;
        }
    }

    sealed class Lease(Mutex mutex) : IDisposable {
        bool disposed;

        public void Dispose() {
            if (disposed) {
                return;
            }
            disposed = true;
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
