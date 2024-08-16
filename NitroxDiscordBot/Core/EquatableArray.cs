#nullable enable
using System.Collections;
using System.Collections.Immutable;

namespace NitroxDiscordBot.Core;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    /// <summary>
    ///     The underlying <typeparamref name="T" /> array.
    /// </summary>
    private readonly T[]? array;

    /// <summary>
    ///     Creates a new <see cref="EquatableArray{T}" /> instance.
    /// </summary>
    /// <param name="array">The input <see cref="ImmutableArray" /> to wrap.</param>
    public EquatableArray(T[] array)
    {
        this.array = array;
    }

    /// <sinheritdoc />
    public bool Equals(EquatableArray<T> value)
    {
        return AsSpan().SequenceEqual(value.AsSpan());
    }

    /// <sinheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> value && Equals(this, value);
    }

    /// <sinheritdoc />
    public override int GetHashCode()
    {
        if (array is not { } value)
        {
            return 0;
        }

        HashCode hashCode = default;

        foreach (T item in value)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    ///     Returns a <see cref="ReadOnlySpan{T}" /> wrapping the current items.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}" /> wrapping the current items.</returns>
    public ReadOnlySpan<T> AsSpan()
    {
        return array.AsSpan();
    }

    /// <summary>
    ///     Gets the underlying array if there is one
    /// </summary>
    public T[]? GetArray()
    {
        return array;
    }

    /// <sinheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)(array ?? [])).GetEnumerator();
    }

    /// <sinheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<T>)(array ?? [])).GetEnumerator();
    }

    public int Count => array?.Length ?? 0;

    /// <summary>
    ///     Checks whether two <see cref="EquatableArray{T}" /> values are the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableArray{T}" /> value.</param>
    /// <param name="right">The second <see cref="EquatableArray{T}" /> value.</param>
    /// <returns>Whether <paramref name="left" /> and <paramref name="right" /> are equal.</returns>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Checks whether two <see cref="EquatableArray{T}" /> values are not the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableArray{T}" /> value.</param>
    /// <param name="right">The second <see cref="EquatableArray{T}" /> value.</param>
    /// <returns>Whether <paramref name="left" /> and <paramref name="right" /> are not equal.</returns>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}