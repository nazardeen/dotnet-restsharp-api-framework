using Meridian.ApiTests.Models;

namespace Meridian.ApiTests.Infrastructure;

/// <summary>
/// Builders rather than shared constants: each test states only the dimension
/// it cares about, so adjusting a default cannot quietly change the meaning of
/// an unrelated assertion.
/// </summary>
public static class TestData
{
    public const decimal ApprovalThreshold = 1_000_000m;

    public static CreateOrderDto AnOrder(
        string instrument = "GBP/USD",
        string side = "BUY",
        decimal quantity = 100_000m,
        decimal price = 1.25m) => new(instrument, side, quantity, price);

    /// <summary>Notional 125,000 - below the limit, approved on capture.</summary>
    public static CreateOrderDto BelowLimit(string instrument = "GBP/USD") =>
        AnOrder(instrument, quantity: 100_000m, price: 1.25m);

    /// <summary>Notional 6,250,000 - above the limit, held for approval.</summary>
    public static CreateOrderDto AboveLimit(string instrument = "GBP/USD") =>
        AnOrder(instrument, quantity: 5_000_000m, price: 1.25m);

    /// <summary>Notional exactly at the limit - the boundary case that must NOT require approval.</summary>
    public static CreateOrderDto AtLimit() =>
        AnOrder(quantity: 1_000_000m, price: 1.00m);
}
