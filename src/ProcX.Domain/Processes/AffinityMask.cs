using System.Numerics;

namespace ProcX.Domain.Processes;

/// <summary>
/// A set of logical processors a process may run on, within a single processor group (max 64).
/// </summary>
/// <param name="Value">The raw bit mask; bit <c>n</c> set means processor <c>n</c> is allowed.</param>
public readonly record struct AffinityMask(ulong Value)
{
    /// <summary>
    /// The maximum number of processors representable by a mask.
    /// </summary>
    public const int MaxProcessors = 64;

    /// <summary>
    /// Gets the number of processors in the mask.
    /// </summary>
    public int Count => BitOperations.PopCount(Value);

    /// <summary>
    /// Gets whether no processor is selected; such a mask is not a valid affinity.
    /// </summary>
    public bool IsEmpty => Value == 0;

    /// <summary>
    /// Creates a mask selecting the first <paramref name="processorCount"/> processors.
    /// </summary>
    /// <param name="processorCount">The number of processors (1-64).</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="processorCount"/> is out of range.</exception>
    public static AffinityMask All(int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(processorCount, MaxProcessors);

        return new AffinityMask(processorCount == MaxProcessors ? ulong.MaxValue : (1UL << processorCount) - 1);
    }

    /// <summary>
    /// Creates a mask from processor indices.
    /// </summary>
    /// <param name="processors">Zero-based processor indices (0-63).</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public static AffinityMask FromProcessors(IEnumerable<int> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);

        var value = 0UL;
        foreach (var processor in processors)
        {
            ValidateIndex(processor);
            value |= 1UL << processor;
        }

        return new AffinityMask(value);
    }

    /// <summary>
    /// Determines whether the processor is included in the mask.
    /// </summary>
    /// <param name="processor">The zero-based processor index (0-63).</param>
    /// <returns><see langword="true"/> if included.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="processor"/> is out of range.</exception>
    public bool Contains(int processor)
    {
        ValidateIndex(processor);
        return (Value & (1UL << processor)) != 0;
    }

    /// <summary>
    /// Enumerates the processor indices included in the mask, in ascending order.
    /// </summary>
    /// <returns>The processor indices.</returns>
    public IEnumerable<int> GetProcessors()
    {
        var remaining = Value;
        while (remaining != 0)
        {
            yield return BitOperations.TrailingZeroCount(remaining);
            remaining &= remaining - 1;
        }
    }

    /// <summary>
    /// Validates that a processor index is within the representable range.
    /// </summary>
    /// <param name="processor">The processor index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="processor"/> is out of range.</exception>
    private static void ValidateIndex(int processor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(processor);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(processor, MaxProcessors);
    }
}
