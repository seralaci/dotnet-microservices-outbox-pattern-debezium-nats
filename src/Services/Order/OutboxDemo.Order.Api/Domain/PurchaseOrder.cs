namespace OutboxDemo.Order.Api.Domain;

/// <summary>
/// Aggregate root representing a customer order persisted in the <c>orders</c> table.
/// </summary>
/// <remarks>
/// Construction is funnelled through the <see cref="Create"/> factory so that invariants
/// are validated up front and the parameterless constructor remains available for EF Core
/// materialization only.
/// </remarks>
internal sealed class PurchaseOrder
{
    /// <summary>Stable identifier assigned at creation time.</summary>
    public Guid Id { get; private init; } = Guid.NewGuid();

    /// <summary>First name of the customer placing the order.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Last name of the customer placing the order.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>Email address used to deliver order confirmations.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Moment the order was created (UTC).</summary>
    public DateTimeOffset OrderDate { get; private init; } = DateTimeOffset.UtcNow;

    // Reserved for EF Core. Do not call from application code.
    private PurchaseOrder()
    {
    }

    /// <summary>
    /// Validates required fields and returns a new <see cref="PurchaseOrder"/> instance.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when any input is null, empty, or whitespace.</exception>
    public static PurchaseOrder Create(string firstName, string lastName, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new PurchaseOrder
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email
        };
    }
}
