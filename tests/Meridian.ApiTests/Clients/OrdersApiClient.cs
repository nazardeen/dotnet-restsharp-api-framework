using Meridian.ApiTests.Models;
using RestSharp;

namespace Meridian.ApiTests.Clients;

public sealed class OrdersApiClient : ApiClientBase
{
    public OrdersApiClient(string baseUrl) : base(baseUrl) { }

    public Task<RestResponse<HealthDto>> GetHealthAsync() =>
        Client.ExecuteAsync<HealthDto>(Request("/api/health", Method.Get));

    public Task<RestResponse<OrderDto>> CreateAsync(CreateOrderDto order, string? apiKey = ApiKeys.Trader)
    {
        var request = Request("/api/orders", Method.Post, apiKey).AddJsonBody(order);
        return Client.ExecuteAsync<OrderDto>(request);
    }

    public Task<RestResponse<OrderDto>> GetAsync(string id, string? apiKey = ApiKeys.Trader) =>
        Client.ExecuteAsync<OrderDto>(Request($"/api/orders/{id}", Method.Get, apiKey));

    public Task<RestResponse<OrderListDto>> ListAsync(string? status = null, string? apiKey = ApiKeys.Trader)
    {
        var request = Request("/api/orders", Method.Get, apiKey);
        if (status is not null) request.AddQueryParameter("status", status);
        return Client.ExecuteAsync<OrderListDto>(request);
    }

    public Task<RestResponse<OrderDto>> ApproveAsync(string id, string? apiKey = ApiKeys.Approver) =>
        Client.ExecuteAsync<OrderDto>(Request($"/api/orders/{id}/approve", Method.Post, apiKey));

    public Task<RestResponse<OrderDto>> RejectAsync(string id, string? apiKey = ApiKeys.Approver) =>
        Client.ExecuteAsync<OrderDto>(Request($"/api/orders/{id}/reject", Method.Post, apiKey));

    /// <summary>Captures an order and fails loudly if setup did not succeed.</summary>
    public async Task<OrderDto> CaptureOrThrowAsync(CreateOrderDto order, string apiKey = ApiKeys.Trader)
    {
        var response = await CreateAsync(order, apiKey);

        if (response.Data is null)
        {
            throw new InvalidOperationException(
                $"Setup failed: could not capture order. Status {(int)response.StatusCode}, body: {response.Content}");
        }

        return response.Data;
    }
}
