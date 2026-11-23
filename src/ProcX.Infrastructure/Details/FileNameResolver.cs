using System.Collections.Concurrent;
using ProcX.Infrastructure.Native;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Queries names of <c>File</c> objects on dedicated threads with a timeout.
/// </summary>
/// <remarks>
/// <c>NtQueryObject</c> blocks indefinitely on a synchronous file object (typically a pipe) with a pending
/// I/O request, and a blocked call cannot be cancelled. Each query therefore runs on a background thread; if it
/// does not answer in time that thread is abandoned (it closes its handle and exits if the call ever returns)
/// and a fresh thread serves the remaining files. After <see cref="MaxAbandonedWorkers"/> timeouts the resolver
/// gives up on the remaining files so that a process full of blocked pipes cannot leak unbounded threads.
/// </remarks>
internal sealed class FileNameResolver : IDisposable
{
    /// <summary>
    /// How long to wait for one name.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// How many blocked threads are tolerated before the resolver stops resolving file names.
    /// </summary>
    private const int MaxAbandonedWorkers = 8;

    /// <summary>
    /// The worker serving queries.
    /// </summary>
    private Worker _current;

    /// <summary>
    /// How many workers were abandoned after a timeout.
    /// </summary>
    private int _abandonedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNameResolver"/> class and starts its first thread.
    /// </summary>
    public FileNameResolver() => _current = StartWorker();

    /// <summary>
    /// Gets whether too many queries timed out, meaning further file names are not resolved.
    /// </summary>
    public bool IsAbandoned => _abandonedCount >= MaxAbandonedWorkers;

    /// <summary>
    /// Resolves the name of a file object. Takes ownership of <paramref name="duplicatedHandle"/>.
    /// </summary>
    /// <param name="duplicatedHandle">A handle to the object in the current process; closed by the resolver.</param>
    /// <returns>The name, or <see langword="null"/> if unnamed, timed out or abandoned.</returns>
    public string? Resolve(nint duplicatedHandle)
    {
        if (IsAbandoned)
        {
            Kernel32.CloseHandle(duplicatedHandle);
            return null;
        }

        var job = new Job(duplicatedHandle);
        _current.Jobs.Add(job);
        if (job.Result.Task.Wait(Timeout))
        {
            return job.Result.Task.Result;
        }

        // The worker is stuck inside the kernel: leave it behind (it exits once the call returns) and continue
        // with a new one. Only one query is outstanding at a time, so nothing else is queued on it.
        _abandonedCount++;
        _current.Jobs.CompleteAdding();
        if (!IsAbandoned)
        {
            _current = StartWorker();
        }

        return null;
    }

    /// <summary>
    /// Stops the current worker once it is idle; abandoned workers are left to exit on their own.
    /// </summary>
    public void Dispose()
    {
        if (IsAbandoned)
        {
            return;
        }

        _current.Jobs.CompleteAdding();
        _current.Thread.Join();
        _current.Jobs.Dispose();
    }

    /// <summary>
    /// Starts a worker thread.
    /// </summary>
    /// <returns>The worker.</returns>
    private static Worker StartWorker()
    {
        var jobs = new BlockingCollection<Job>();
        var thread = new Thread(() => Run(jobs)) { IsBackground = true, Name = "ProcX file name resolver" };
        thread.Start();
        return new Worker(jobs, thread);
    }

    /// <summary>
    /// The worker loop.
    /// </summary>
    /// <param name="jobs">The queue served by the worker.</param>
    private static void Run(BlockingCollection<Job> jobs)
    {
        foreach (var job in jobs.GetConsumingEnumerable())
        {
            string? name = null;
            try
            {
                name = HandleNames.QueryObjectName(job.Handle);
            }
            finally
            {
                Kernel32.CloseHandle(job.Handle);
                job.Result.TrySetResult(name);
            }
        }
    }

    /// <summary>
    /// A worker thread and its queue.
    /// </summary>
    /// <param name="Jobs">The pending queries.</param>
    /// <param name="Thread">The thread serving them.</param>
    private sealed record Worker(BlockingCollection<Job> Jobs, Thread Thread);

    /// <summary>
    /// A pending name query.
    /// </summary>
    /// <param name="Handle">The duplicated handle.</param>
    private sealed record Job(nint Handle)
    {
        /// <summary>
        /// Gets the completion source receiving the name.
        /// </summary>
        public TaskCompletionSource<string?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
