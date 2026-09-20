using System.Net.Sockets;
using Meridian.MockApi;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Meridian.ApiTests.Infrastructure;

/// <summary>
/// Starts the service under test in-process on an ephemeral port, so a clean
/// clone runs with a single `dotnet test` and parallel agents never collide on
/// a fixed port.
///
/// Set API_BASE_URL to run the identical suite against a deployed environment:
///     API_BASE_URL=https://api.staging.example.com dotnet test
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private WebApplication? _app;

    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>True when running against the bundled mock rather than a deployed environment.</summary>
    public bool UsingBundledService => _app is not null;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("API_BASE_URL");

        if (!string.IsNullOrWhiteSpace(external))
        {
            BaseUrl = external.TrimEnd('/');
            return;
        }

        BaseUrl = $"http://127.0.0.1:{FindFreePort()}";
        _app = MockApiHost.Build(BaseUrl);
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
