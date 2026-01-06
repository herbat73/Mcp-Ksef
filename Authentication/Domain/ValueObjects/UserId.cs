namespace Authentication.Domain.ValueObjects;

/// <summary>
/// User ID value object following Microsoft DDD patterns.
/// Represents immutable user identification.
/// </summary>
public class UserId : IEquatable<UserId>
{
    public string Value { get; }

    private UserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("User ID cannot be empty", nameof(value));

        Value = value;
    }

    public static UserId Create(string value)
    {
        return new UserId(value);
    }

    public bool Equals(UserId? other)
    {
        return other != null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as UserId);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator string(UserId userId) => userId.Value;
}