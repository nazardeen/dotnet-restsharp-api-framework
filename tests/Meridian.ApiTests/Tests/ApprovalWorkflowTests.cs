using System.Net;
using Meridian.ApiTests.Clients;
using Meridian.ApiTests.Infrastructure;
using Meridian.ApiTests.Models;
using Xunit;

namespace Meridian.ApiTests.Tests;

[Collection(ApiCollection.Name)]
public sealed class ApprovalWorkflowTests
{
    private readonly ApiFixture _fixture;

    public ApprovalWorkflowTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task An_independent_approver_can_release_a_held_order()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit());

        var response = await client.ApproveAsync(order.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.Approved, response.Data?.Status);
        Assert.Equal("S. Approver", response.Data?.ApprovedBy);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task An_approver_can_reject_a_held_order()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit("AUD/USD"));

        var response = await client.RejectAsync(order.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.Rejected, response.Data?.Status);
        Assert.Equal("S. Approver", response.Data?.ApprovedBy);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task The_capturing_user_cannot_approve_their_own_order()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        // Captured by the approver's own credentials, so the four-eyes rule -
        // not the role check - is what must refuse it.
        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit(), ApiKeys.Approver);

        var response = await client.ApproveAsync(order.Id, ApiKeys.Approver);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("four_eyes_violation", ApiClientBase.ErrorOf(response)?.Code);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task An_already_approved_order_cannot_be_approved_again()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit());
        await client.ApproveAsync(order.Id);

        var second = await client.ApproveAsync(order.Id);

        // A retried or double-submitted approval must conflict, not silently succeed.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("invalid_transition", ApiClientBase.ErrorOf(second)?.Code);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task A_rejected_order_cannot_subsequently_be_approved()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit());
        await client.RejectAsync(order.Id);

        var response = await client.ApproveAsync(order.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var current = await client.GetAsync(order.Id);
        Assert.Equal(OrderStatus.Rejected, current.Data?.Status);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task An_order_that_never_required_approval_cannot_be_approved()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var order = await client.CaptureOrThrowAsync(TestData.BelowLimit());

        var response = await client.ApproveAsync(order.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("invalid_transition", ApiClientBase.ErrorOf(response)?.Code);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task Approving_an_unknown_order_returns_404()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.ApproveAsync("ORD-000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("order_not_found", ApiClientBase.ErrorOf(response)?.Code);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task Held_orders_are_discoverable_through_the_status_filter()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit());

        var response = await client.ListAsync(OrderStatus.PendingApproval);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Data!.Items, o => o.Id == order.Id);
        Assert.All(response.Data.Items, o => Assert.Equal(OrderStatus.PendingApproval, o.Status));
    }
}
