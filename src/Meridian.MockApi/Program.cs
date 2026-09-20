using Meridian.MockApi;

// Standalone entry point, for running the service under test on its own:
//   dotnet run --project src/Meridian.MockApi
// The test suite does not use this - it calls MockApiHost.Build directly so it
// can bind an ephemeral port and avoid collisions on a shared build agent.
var url = Environment.GetEnvironmentVariable("MOCK_URL") ?? "http://localhost:5055";
var app = MockApiHost.Build(url);

Console.WriteLine($"Meridian Order API listening on {url}");
await app.RunAsync();
