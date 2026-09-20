using System.Net;
using Meridian.ApiTests.Clients;
using Meridian.ApiTests.Infrastructure;
using Xunit;

namespace Meridian.ApiTests.Tests;

[Collection(ApiCollection.Name)]
public sealed class HealthAndReferenceDataTests
{
    private readonly ApiFixture _fixture;

    public HealthAndReferenceDataTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Health_endpoint_reports_ok()
    {
        using var client = new OrdersApiClient(_fixture.BaseUrl);

        var response = await client.GetHealthAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", response.Data?.Status);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Fx_rates_default_to_a_usd_base()
    {
        using var client = new FxApiClient(_fixture.BaseUrl);

        var response = await client.GetRatesAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Data);
        Assert.Equal("USD", response.Data!.Base);
        Assert.NotEmpty(response.Data.Rates);
        // The base currency is not quoted against itself.
        Assert.DoesNotContain("USD", response.Data.Rates.Keys);
    }

    [Theory]
    [Trait("Category", "Regression")]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("JPY")]
    [InlineData("AUD")]
    public async Task Fx_rates_are_positive_for_every_supported_base(string baseCurrency)
    {
        using var client = new FxApiClient(_fixture.BaseUrl);

        var response = await client.GetRatesAsync(baseCurrency);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(baseCurrency, response.Data!.Base);
        Assert.All(response.Data.Rates, rate => Assert.True(
            rate.Value > 0,
            $"Rate for {rate.Key} was {rate.Value}; a quote must be strictly positive."));
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task Fx_rates_reject_an_unsupported_base_currency()
    {
        using var client = new FxApiClient(_fixture.BaseUrl);

        var response = await client.GetRatesAsync("XYZ");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("unknown_currency", ApiClientBase.ErrorOf(response)?.Code);
    }

    [Fact]
    [Trait("Category", "Regression")]
    public async Task Fx_base_currency_is_case_insensitive()
    {
        using var client = new FxApiClient(_fixture.BaseUrl);

        var response = await client.GetRatesAsync("gbp");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("GBP", response.Data!.Base);
    }
}
