using System.Collections.Concurrent;
using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// Caches <see cref="ProcessStaticInfo"/> per process instance so each process is opened at most once.
/// </summary>
/// <param name="provider">The underlying provider.</param>
public sealed class ProcessInfoCache(IProcessInfoProvider provider)
{
    /// <summary>
    /// Cached entries; <see cref="Lazy{T}"/> ensures concurrent callers trigger a single lookup.
    /// </summary>
    private readonly ConcurrentDictionary<ProcessKey, Lazy<ProcessStaticInfo>> _entries = new();

    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets the cached information, retrieving it on first access.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <returns>The information.</returns>
    public ProcessStaticInfo Get(ProcessKey key) =>
        _entries.GetOrAdd(key, k => new Lazy<ProcessStaticInfo>(() => provider.GetStaticInfo(k))).Value;

    /// <summary>
    /// Removes entries of processes that have exited.
    /// </summary>
    /// <param name="keys">The keys of exited processes.</param>
    public void Evict(IEnumerable<ProcessKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        foreach (var key in keys)
        {
            _entries.TryRemove(key, out _);
        }
    }
}
