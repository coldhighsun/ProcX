using System.Collections.Concurrent;
using ProcX.Infrastructure.Native;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Queries names of <c>File</c> objects on a dedicated thread with a timeout.
/// </summary>
/// <remarks>
/// <c>NtQueryObject</c> blocks indefinitely on a synchronous file object (typically a pipe) with a pending
/// I/O request, and a blocked call cannot be cancelled. The query therefore runs on a background thread; if it
/// does not answer in time the resolver gives up on all remaining files and abandons the thread, which closes
/// its handle and exits if the call ever returns.
/// </remarks>
internal sealed class FileNameResolver : IDisposable
{
    /// <summary>
    /// How long to wait for one name.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Pending queries.
    /// </summary>
    private readonly BlockingCollection<Job> _jobs = [];

    /// <summary>
    /// The worker thread.
    /// </summary>
    private readonly Thread _worker;

    /// <summary>
    /// Set once a query timed out; no further queries are attempted.
    /// </summary>
    private volatile bool _abandoned;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNameResolver"/> class and starts its thread.
    /// </summary>
    public FileNameResolver()
    {
        _worker = new Thread(Run) { IsBackground = true, Name = "ProcX file name resolver" };
        _worker.Start();
    }

    /// <summary>
    /// Gets whether a query timed out, meaning further file names are not resolved.
    /// </summary>
    public bool IsAbandoned => _abandoned;

    /// <summary>
    /// Resolves the name of a file object. Takes ownership of <paramref name="duplicatedHandle"/>.
    /// </summary>
    /// <param name="duplicatedHandle">A handle to the object in the current process; closed by the resolver.</param>
    /// <returns>The name, or <see langword="null"/> if unnamed, timed out or abandoned.</returns>
    public string? Resolve(nint duplicatedHandle)
    {
        if (_abandoned)
        {
            Kernel32.CloseHandle(duplicatedHandle);
            return null;
        }

        var job = new Job(duplicatedHandle);
        _jobs.Add(job);
        if (job.Result.Task.Wait(Timeout))
        {
            return job.Result.Task.Result;
        }

        _abandoned = true;
        return null;
    }

    /// <summary>
    /// Stops the worker once it is idle; an abandoned worker is left to exit on its own.
    /// </summary>
    public void Dispose()
    {
        _jobs.CompleteAdding();
        if (!_abandoned)
        {
            _worker.Join();
            _jobs.Dispose();
        }
    }

    /// <summary>
    /// The worker loop.
    /// </summary>
    private void Run()
    {
        foreach (var job in _jobs.GetConsumingEnumerable())
        {
            string? name = null;
            try
            {
                if (!_abandoned)
                {
                    name = HandleNames.QueryObjectName(job.Handle);
                }
            }
            finally
            {
                Kernel32.CloseHandle(job.Handle);
                job.Result.TrySetResult(name);
            }
        }
    }

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
