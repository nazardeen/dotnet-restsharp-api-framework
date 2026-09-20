using Meridian.ApiTests.Models;
using RestSharp;

namespace Meridian.ApiTests.Clients;

public sealed class FxApiClient : ApiClientBase
{
    public FxApiClient(string baseUrl) : base(baseUrl) { }

    public Task<RestResponse<FxRatesDto>> GetRatesAsync(string? baseCurrency = null)
    {
        var request = Request("/api/fx/rates", Method.Get);
        if (baseCurrency is not null) request.AddQueryParameter("base", baseCurrency);
        return Client.ExecuteAsync<FxRatesDto>(request);
    }
}
