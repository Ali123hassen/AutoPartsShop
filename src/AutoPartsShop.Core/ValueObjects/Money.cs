namespace AutoPartsShop.Core.ValueObjects;

/// <summary>
/// Represents a monetary value with currency information.
/// Immutable value object that supports arithmetic operations and comparisons.
/// </summary>
public record Money
{
    /// <summary>
    /// Gets the monetary amount.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Gets the ISO 4217 currency code. Defaults to "SAR" (Saudi Riyal).
    /// </summary>
    public string Currency { get; init; } = "SAR";

    /// <summary>
    /// Initializes a new instance of the <see cref="Money"/> record with default values.
    /// </summary>
    public Money() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Money"/> record.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    /// <param name="currency">The ISO 4217 currency code. Defaults to "SAR".</param>
    public Money(decimal amount, string currency = "SAR")
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Represents a zero monetary value in SAR.
    /// </summary>
    public static Money ZeroSar => new(0m, "SAR");

    /// <summary>
    /// Adds two monetary values of the same currency.
    /// </summary>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>
    /// Subtracts one monetary value from another of the same currency.
    /// </summary>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>
    /// Multiplies a monetary value by a scalar.
    /// </summary>
    public static Money operator *(Money money, decimal multiplier) =>
        new(money.Amount * multiplier, money.Currency);

    /// <summary>
    /// Multiplies a monetary value by a scalar.
    /// </summary>
    public static Money operator *(decimal multiplier, Money money) =>
        money * multiplier;

    /// <summary>
    /// Divides a monetary value by a scalar.
    /// </summary>
    public static Money operator /(Money money, decimal divisor) =>
        new(money.Amount / divisor, money.Currency);

    /// <summary>
    /// Negates a monetary value.
    /// </summary>
    public static Money operator -(Money money) =>
        new(-money.Amount, money.Currency);

    // record type already provides == and != operators via Equals GetHashCode

    /// <summary>
    /// Determines whether the left monetary value is greater than the right.
    /// </summary>
    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    /// <summary>
    /// Determines whether the left monetary value is less than the right.
    /// </summary>
    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    /// <summary>
    /// Determines whether the left monetary value is greater than or equal to the right.
    /// </summary>
    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    /// <summary>
    /// Determines whether the left monetary value is less than or equal to the right.
    /// </summary>
    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    /// <summary>
    /// Returns a string representation of the monetary value.
    /// </summary>
    public override string ToString() => $"{Amount:N2} {Currency}";

    /// <summary>
    /// Ensures that two monetary values share the same currency.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when currencies do not match.</exception>
    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException(
                $"Cannot perform operation on money with different currencies: {left.Currency} vs {right.Currency}.");
    }
}
