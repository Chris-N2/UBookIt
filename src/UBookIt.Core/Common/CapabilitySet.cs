namespace UBookIt.Core.Common;

/// <summary>
/// A set of capability keys — carried by a resource to say what it can do, and
/// by a service role to say what it needs. Constructible only through the
/// validating factory, so a set can never hold a malformed key.
/// <para>
/// A value object rather than a bare <see cref="IReadOnlySet{T}"/> for three
/// reasons: it compares structurally, so a <c>record</c> carrying one keeps
/// value-equality semantics (a set member would give the generated
/// <c>Equals</c> reference semantics, and two roles requiring the same
/// capabilities would compare unequal); it normalizes once, so ordering and
/// duplication cannot vary by construction site; and it owns the subset test, so
/// eligibility has exactly one implementation.
/// </para>
/// </summary>
public sealed class CapabilitySet : IEquatable<CapabilitySet>
{
    /// <summary>Failure field for a resource's capabilities.</summary>
    public const string ResourceField = "Capabilities";

    /// <summary>Failure field for a service role's required capabilities.</summary>
    public const string RequiredField = "RequiredCapabilities";

    private readonly string[] _keys;

    private CapabilitySet(string[] keys) => _keys = keys;

    /// <summary>The set carrying no capabilities. Means "none", never "all".</summary>
    public static CapabilitySet Empty { get; } = new([]);

    /// <summary>
    /// The keys, deduplicated and in a deterministic order. The order is an
    /// implementation detail of the normalization, not a caller's input.
    /// </summary>
    public IReadOnlyList<string> Keys => _keys;

    public int Count => _keys.Length;

    public bool IsEmpty => _keys.Length == 0;

    /// <summary>
    /// Builds a set from raw keys, rejecting any that is not a normalized key.
    /// Keys are rejected rather than normalized: the value stored is the value
    /// supplied, so an editor never finds that their input was silently rewritten.
    /// </summary>
    /// <param name="keys">The raw keys. Null or empty yields <see cref="Empty"/>.</param>
    /// <param name="field">Failure field, so a message can be associated with the control it came from.</param>
    public static DomainResult<CapabilitySet> Create(IEnumerable<string?>? keys, string field = ResourceField)
    {
        if (keys is null)
        {
            return DomainResult<CapabilitySet>.Success(Empty);
        }

        var failures = new List<DomainFailure>();
        var accepted = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            if (!NormalizedKey.IsValid(key))
            {
                failures.Add(new DomainFailure(
                    FailureCodes.CapabilityKeyInvalid,
                    $"Capability key '{key}' must be lower-case kebab-case (e.g. 'cert-x').",
                    field));
                continue;
            }

            accepted.Add(key!);
        }

        return failures.Count > 0
            ? DomainResult<CapabilitySet>.Failure(failures)
            : DomainResult<CapabilitySet>.Success(accepted.Count == 0 ? Empty : new CapabilitySet([.. accepted]));
    }

    /// <summary>
    /// Whether <paramref name="held"/> carries every capability this set
    /// requires — the eligibility subset test, with this set as the requirement.
    /// <para>
    /// Named for the direction it reads in: <c>role.RequiredCapabilities
    /// .IsSatisfiedBy(resource.Capabilities)</c>. A symmetric-looking name such
    /// as <c>Satisfies</c> can be called the wrong way round and still compile,
    /// and the wrong way round is <em>silently</em> wrong — it would admit
    /// under-qualified resources whenever the requirement is the larger set.
    /// </para>
    /// <para>
    /// An empty requirement is satisfied by anything, including an empty held
    /// set: that is what keeps a service defined without capabilities matching
    /// every resource of its type.
    /// </para>
    /// </summary>
    public bool IsSatisfiedBy(CapabilitySet held)
    {
        ArgumentNullException.ThrowIfNull(held);

        // Ordinal set containment. Both sides are already deduplicated, so a
        // linear scan over the requirement is the whole test.
        foreach (var required in _keys)
        {
            if (Array.IndexOf(held._keys, required) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether this set carries the given capability.</summary>
    public bool Contains(string key) => Array.IndexOf(_keys, key) >= 0;

    public bool Equals(CapabilitySet? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _keys.AsSpan().SequenceEqual(other._keys);
    }

    public override bool Equals(object? obj) => Equals(obj as CapabilitySet);

    public override int GetHashCode()
    {
        // Order-independent by construction: _keys is always sorted, so equal
        // sets hash equally without needing a commutative combiner.
        var hash = new HashCode();

        foreach (var key in _keys)
        {
            hash.Add(key, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => _keys.Length == 0 ? "(none)" : string.Join(", ", _keys);

    public static bool operator ==(CapabilitySet? left, CapabilitySet? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(CapabilitySet? left, CapabilitySet? right) => !(left == right);
}
