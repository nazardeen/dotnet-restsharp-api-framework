using System.Text.Json;
using Meridian.ApiTests.Models;
using RestSharp;
using RestSharp.Serializers.Json;

namespace Meridian.ApiTests.Clients;

/// <summary>
/// Thin wrapper over RestSharp. Keeps serialiser configuration, the auth header
/// and error-body parsing in one place so the tests read as statements about
/// behaviour rather than plumbing.
///
/// Note the deliberate absence of status-code assertions here: a client that
/// throws on a non-2xx cannot be used to test that the API returns 403.
/// </summary>
public abstract class ApiClientBase : IDisposable
{
    private static readonly JsonSerializerOptions ErrorJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    protected readonly RestClient Client;

    protected ApiClientBase(string baseUrl)
    {
        var options = new RestClientOptions(baseUrl)
        {
            ThrowOnAnyError = false,
            ThrowOnDeserializationError = false,
            Timeout = TimeSpan.FromSeconds(15),
        };

        Client = new RestClient(
            options,
            configureSerialization: s => s.UseSystemTextJson(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }));
    }

    protected static RestRequest Request(string resource, Method method, string? apiKey = null)
    {
        var request = new RestRequest(resource, method);
        if (apiKey is not null) request.AddHeader("X-Api-Key", apiKey);
        return request;
    }

    /// <summary>Parses a problem body. Returns null when the response carried none.</summary>
    public static ApiErrorDto? ErrorOf(RestResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Content)) return null;

        try
        {
            return JsonSerializer.Deserialize<ApiErrorDto>(response.Content, ErrorJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}
