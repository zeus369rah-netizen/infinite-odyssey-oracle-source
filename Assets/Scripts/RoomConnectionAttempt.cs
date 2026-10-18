using System;
using System.Threading;
using System.Threading.Tasks;

namespace OdysseyOracle
{
    // Cold per-attempt ownership lease. All methods are called by the main-thread
    // room adapter; no worker may mutate this lease or its Unity owner.
    internal sealed class RoomConnectionAttempt : IDisposable
    {
        readonly CancellationTokenSource cancellation;
        readonly TaskCompletionSource<bool> settled = new TaskCompletionSource<bool>();
        bool disposed;
        string timeoutPhase;

        public int Ticket { get; }
        public double Deadline { get; }
        public string Phase { get; set; } = "preparing game services";
        public bool TimedOut { get; private set; }
        public CancellationToken Token { get; }
        public Task Settled => settled.Task;
        public string TimeoutReason => "Timed out while " + (TimedOut ? timeoutPhase : Phase) + ".";

        public RoomConnectionAttempt(int ticket, double now, double timeoutSeconds, CancellationToken applicationExit)
        {
            Ticket = ticket;
            Deadline = now + timeoutSeconds;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(applicationExit);
            Token = cancellation.Token;
        }

        public bool Expire(double now)
        {
            if (disposed || TimedOut || Token.IsCancellationRequested || now < Deadline) return false;
            TimedOut = true;
            timeoutPhase = Phase;
            cancellation.Cancel();
            return true;
        }

        public void Cancel()
        {
            if (!disposed && !Token.IsCancellationRequested) cancellation.Cancel();
        }

        public void ThrowIfCancellationRequested()
        {
            if (TimedOut) throw new TimeoutException(TimeoutReason);
            Token.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            cancellation.Dispose();
            settled.TrySetResult(true);
        }
    }
}

// # ARCHITECTURE SNAPSHOT: Cold main-thread connection lease owns one linked CTS and one completion task until SDK/cleanup settles. Ticket identifies the existing RoomConnection operation; no coordinates, spatial arrays, native memory, Unity Objects, credentials, or saved profiles are stored. Disposing signals drained ownership and permits the existing owner to retry.
