using System.Net;
using Meridian.ApiTests.Clients;
using Meridian.ApiTests.Infrastructure;
using Meridian.ApiTests.Models;
using Xunit;

namespace Meridian.ApiTests.Tests;

[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests
{
    private readonly ApiFixture _fixture;

    public AuthorizationTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Requests_without_an_api_key_are_rejected()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.ListAsync(apiKey: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("missing_or_invalid_api_key", ApiClientBase.ErrorOf(response)?.Code);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task An_unrecognised_api_key_is_rejected_as_unauthorised_not_forbidden()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.ListAsync(apiKey: ApiKeys.Invalid);

        // 401, not 403: the caller has not been identified at all, so the
        // correct signal is "authenticate", not "you may not do this".
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task A_read_only_role_cannot_capture_orders()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.CreateAsync(TestData.BelowLimit(), ApiKeys.Viewer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("insufficient_role", ApiClientBase.ErrorOf(response)?.Code);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task A_read_only_role_can_still_read_orders()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.ListAsync(apiKey: ApiKeys.Viewer);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task A_trader_role_cannot_action_approvals()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);
        var order = await client.CaptureOrThrowAsync(TestData.AboveLimit());

        var response = await client.ApproveAsync(order.Id, ApiKeys.Trader);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("insufficient_role", ApiClientBase.ErrorOf(response)?.Code);
    }
}
