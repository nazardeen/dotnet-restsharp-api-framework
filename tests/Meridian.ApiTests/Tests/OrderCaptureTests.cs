using System.Net;
using Meridian.ApiTests.Clients;
using Meridian.ApiTests.Infrastructure;
using Meridian.ApiTests.Models;
using Xunit;

namespace Meridian.ApiTests.Tests;

[Collection(ApiCollection.Name)]
public sealed class OrderCaptureTests
{
    private readonly ApiFixture _fixture;

    public OrderCaptureTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Capturing_a_below_limit_order_returns_201_and_the_full_contract()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var payload = TestData.BelowLimit("EUR/USD");

        var response = await client.CreateAsync(payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = response.Data;
        Assert.NotNull(order);
        Assert.Matches(@"^ORD-\d+$", order!.Id);
        Assert.Equal("EUR/USD", order.Instrument);
        Assert.Equal("BUY", order.Side);
        Assert.Equal(125_000m, order.Notional);
        Assert.Equal(OrderStatus.Approved, order.Status);
        Assert.Equal("A. Trader", order.CapturedBy);
        Assert.Null(order.ApprovedBy);
        Assert.NotEqual(default, order.CreatedAt);

        // A 201 must point at the created resource.
        var location = response.Headers?.FirstOrDefault(h => h.Name == "Location")?.Value?.ToString();
        Assert.Equal($"/api/orders/{order.Id}", location);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task Notional_is_computed_as_quantity_times_price()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var order = await client.CaptureOrThrowAsync(TestData.AnOrder(quantity: 12_345m, price: 1.2345m));

        Assert.Equal(12_345m * 1.2345m, order.Notional);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task An_order_exactly_at_the_limit_does_not_require_approval()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var order = await client.CaptureOrThrowAsync(TestData.AtLimit());

        // The rule is "above the threshold", so the boundary itself must pass
        // straight through. Off-by-one here is a real-world approval bypass.
        Assert.Equal(TestData.ApprovalThreshold, order.Notional);
        Assert.Equal(OrderStatus.Approved, order.Status);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task An_order_above_the_limit_is_held_for_approval()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit());

        Assert.True(order.Notional > TestData.ApprovalThreshold);
        Assert.Equal(OrderStatus.PendingApproval, order.Status);
        Assert.Null(order.ApprovedBy);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task A_captured_order_is_retrievable_by_id()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var created = await client.CaptureOrThrowAsync(TestData.BelowLimit("USD/JPY"));

        var response = await client.GetAsync(created.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, response.Data?.Id);
        Assert.Equal(created.Notional, response.Data?.Notional);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task An_unknown_order_id_returns_404_with_a_machine_readable_code()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.GetAsync("ORD-000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("order_not_found", ApiClientBase.ErrorOf(response)?.Code);
    }

    public static TheoryData<string, CreateOrderDto, string> InvalidPayloads() => new()
    {
        { "zero quantity", TestData.AnOrder(quantity: 0m), "invalid_quantity" },
        { "negative quantity", TestData.AnOrder(quantity: -100m), "invalid_quantity" },
        { "zero price", TestData.AnOrder(price: 0m), "invalid_price" },
        { "negative price", TestData.AnOrder(price: -1.5m), "invalid_price" },
        { "unknown instrument", TestData.AnOrder(instrument: "XXX/YYY"), "invalid_instrument" },
        { "unknown side", TestData.AnOrder(side: "HOLD"), "invalid_side" },
    };

    [Theory]
    [Trait("Category", "Regression")]
    [MemberData(nameof(InvalidPayloads))]
    public async Task Invalid_payloads_are_rejected_with_400_and_a_specific_code(
        string scenario, CreateOrderDto payload, string expectedCode)
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.CreateAsync(payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = ApiClientBase.ErrorOf(response);
        Assert.Equal(expectedCode, error?.Code);
        // A client needs to be able to tell the user which field to fix.
        Assert.False(string.IsNullOrWhiteSpace(error?.Detail), $"[{scenario}] no detail supplied");
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task Listing_by_an_unrecognised_status_is_a_client_error_not_an_empty_list()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.ListAsync(status: "NOT_A_STATUS");

        // Returning 200 with zero rows would hide a caller's typo.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_status", ApiClientBase.ErrorOf(response)?.Code);
    }
}
