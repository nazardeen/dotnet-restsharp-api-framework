namespace Meridian.ApiTests.Models;

/// <summary>
/// The contracts the suite asserts against.
///
/// These are deliberately *not* the server's own record types. If the service
/// renames a field, these tests must fail - which cannot happen when both sides
/// share one class.
/// </summary>
public sealed record OrderDto(
    string Id,
    string Instrument,
    string Side,
    decimal Quantity,
    decimal Price,
    decimal Notional,
    string Status,
    string CapturedBy,
    string? ApprovedBy,
    DateTimeOffset CreatedAt);

public sealed record OrderListDto(int Count, IReadOnlyList<OrderDto> Items);

public sealed record FxRatesDto(string Base, string Date, IReadOnlyDictionary<string, decimal> Rates);

public sealed record ApiErrorDto(string Code, string Detail);

public sealed record HealthDto(string Status);

public sealed record CreateOrderDto(string? Instrument, string? Side, decimal? Quantity, decimal? Price);

public static class OrderStatus
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}

public static class ApiKeys
{
    public const string Trader = "trader-key-001";
    public const string Approver = "approver-key-002";
    public const string Viewer = "readonly-key-003";
    public const string Invalid = "not-a-real-key";
}
