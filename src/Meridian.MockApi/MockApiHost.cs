using System.Collections.Concurrent;

namespace Meridian.MockApi;

/// <summary>
/// The service under test: an order capture, approval and settlement API with
/// an FX reference-data endpoint.
///
/// It is built as a host factory rather than only an entry point so the test
/// suite can start it in-process on an ephemeral port. That keeps a clean clone
/// runnable with a single <c>dotnet test</c> - no container to start first, no
/// external environment, no shared state between runs.
/// </summary>
public static class MockApiHost
{
    public const decimal ApprovalThreshold = 1_000_000m;

    public static readonly IReadOnlyDictionary<string, ApiUser> ApiKeys = new Dictionary<string, ApiUser>
    {
        ["trader-key-001"] = new("A. Trader", "trader"),
        ["approver-key-002"] = new("S. Approver", "approver"),
        ["readonly-key-003"] = new("R. Viewer", "viewer"),
    };

    private static readonly string[] Instruments = { "GBP/USD", "EUR/USD", "USD/JPY", "AUD/USD" };

    private static readonly IReadOnlyDictionary<string, decimal> UsdRates = new Dictionary<string, decimal>
    {
        ["EUR"] = 0.9215m,
        ["GBP"] = 0.7880m,
        ["JPY"] = 151.4200m,
        ["AUD"] = 1.5140m,
        ["USD"] = 1.0000m,
    };

    public static WebApplication Build(string url)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(url);

        var app = builder.Build();

        var orders = new ConcurrentDictionary<string, Order>();
        var sequence = 1000;

        // ---------------------------------------------------------- health

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        // ------------------------------------------------ reference data

        app.MapGet("/api/fx/rates", (string? @base) =>
        {
            var baseCurrency = (@base ?? "USD").ToUpperInvariant();

            if (!UsdRates.TryGetValue(baseCurrency, out var baseRate))
            {
                return Problem(StatusCodes.Status404NotFound, "unknown_currency",
                    $"'{baseCurrency}' is not a supported base currency.");
            }

            var rates = UsdRates
                .Where(r => r.Key != baseCurrency)
                .ToDictionary(r => r.Key, r => decimal.Round(r.Value / baseRate, 6));

            return Results.Ok(new FxRatesResponse(baseCurrency, DateOnly.FromDateTime(DateTime.UtcNow), rates));
        });

        // ---------------------------------------------------------- orders

        app.MapGet("/api/orders", (HttpContext ctx, string? status) =>
        {
            if (Authenticate(ctx) is not { } user) return Unauthorized();

            var items = orders.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!OrderStatuses.All.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    return Problem(StatusCodes.Status400BadRequest, "invalid_status",
                        $"'{status}' is not a recognised order status.");
                }
                items = items.Where(o => string.Equals(o.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            var list = items.OrderByDescending(o => o.Id).ToList();
            return Results.Ok(new OrderListResponse(list.Count, list));
        });

        app.MapGet("/api/orders/{id}", (HttpContext ctx, string id) =>
        {
            if (Authenticate(ctx) is not { } user) return Unauthorized();

            return orders.TryGetValue(id, out var order)
                ? Results.Ok(order)
                : Problem(StatusCodes.Status404NotFound, "order_not_found", $"No order with id '{id}'.");
        });

        app.MapPost("/api/orders", (HttpContext ctx, CreateOrderRequest request) =>
        {
            if (Authenticate(ctx) is not { } user) return Unauthorized();

            if (user.Role == "viewer")
            {
                return Problem(StatusCodes.Status403Forbidden, "insufficient_role",
                    "Role 'viewer' may not capture orders.");
            }

            if (Validate(request) is { } failure) return failure;

            var notional = request.Quantity!.Value * request.Price!.Value;
            var order = new Order(
                Id: $"ORD-{Interlocked.Increment(ref sequence)}",
                Instrument: request.Instrument!,
                Side: request.Side!.ToUpperInvariant(),
                Quantity: request.Quantity.Value,
                Price: request.Price.Value,
                Notional: notional,
                Status: notional > ApprovalThreshold ? OrderStatuses.PendingApproval : OrderStatuses.Approved,
                CapturedBy: user.DisplayName,
                ApprovedBy: null,
                CreatedAt: DateTimeOffset.UtcNow);

            orders[order.Id] = order;
            return Results.Created($"/api/orders/{order.Id}", order);
        });

        app.MapPost("/api/orders/{id}/{action}", (HttpContext ctx, string id, string action) =>
        {
            if (Authenticate(ctx) is not { } user) return Unauthorized();

            if (action is not ("approve" or "reject"))
            {
                return Problem(StatusCodes.Status404NotFound, "unknown_action", $"Unknown action '{action}'.");
            }

            if (!orders.TryGetValue(id, out var order))
            {
                return Problem(StatusCodes.Status404NotFound, "order_not_found", $"No order with id '{id}'.");
            }

            if (user.Role != "approver")
            {
                return Problem(StatusCodes.Status403Forbidden, "insufficient_role",
                    $"Role '{user.Role}' may not action approvals.");
            }

            if (order.CapturedBy == user.DisplayName)
            {
                return Problem(StatusCodes.Status403Forbidden, "four_eyes_violation",
                    "An order may not be approved by the user who captured it.");
            }

            if (order.Status != OrderStatuses.PendingApproval)
            {
                return Problem(StatusCodes.Status409Conflict, "invalid_transition",
                    $"Order is {order.Status} and cannot be actioned.");
            }

            var updated = order with
            {
                Status = action == "approve" ? OrderStatuses.Approved : OrderStatuses.Rejected,
                ApprovedBy = user.DisplayName,
            };

            orders[id] = updated;
            return Results.Ok(updated);
        });

        return app;

        // ------------------------------------------------------- helpers

        static ApiUser? Authenticate(HttpContext ctx)
        {
            var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
            return key is not null && ApiKeys.TryGetValue(key, out var user) ? user : null;
        }

        static IResult Unauthorized() =>
            Problem(StatusCodes.Status401Unauthorized, "missing_or_invalid_api_key",
                "Supply a valid X-Api-Key header.");

        static IResult Problem(int status, string code, string detail) =>
            Results.Json(new ApiError(code, detail), statusCode: status);

        static IResult? Validate(CreateOrderRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.Instrument) || !Instruments.Contains(r.Instrument))
                return Problem(StatusCodes.Status400BadRequest, "invalid_instrument",
                    "Instrument must be one of: " + string.Join(", ", Instruments));

            if (r.Side is null || (!r.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase) &&
                                   !r.Side.Equals("SELL", StringComparison.OrdinalIgnoreCase)))
                return Problem(StatusCodes.Status400BadRequest, "invalid_side", "Side must be BUY or SELL.");

            if (r.Quantity is null or <= 0)
                return Problem(StatusCodes.Status400BadRequest, "invalid_quantity",
                    "Quantity must be a positive number.");

            if (r.Price is null or <= 0)
                return Problem(StatusCodes.Status400BadRequest, "invalid_price",
                    "Price must be a positive number.");

            return null;
        }
    }
}

public record ApiUser(string DisplayName, string Role);

public record CreateOrderRequest(string? Instrument, string? Side, decimal? Quantity, decimal? Price);

public record Order(
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

public record OrderListResponse(int Count, IReadOnlyList<Order> Items);

public record FxRatesResponse(string Base, DateOnly Date, IReadOnlyDictionary<string, decimal> Rates);

public record ApiError(string Code, string Detail);

public static class OrderStatuses
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Settled = "SETTLED";

    public static readonly string[] All = { PendingApproval, Approved, Rejected, Settled };
}
