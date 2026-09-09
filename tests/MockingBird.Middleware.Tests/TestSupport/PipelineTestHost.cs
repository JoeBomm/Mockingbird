using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MockingBird.Middleware.Configuration;
using MockingBird.Middleware.Providers;

namespace MockingBird.Middleware.Tests.TestSupport;

/// <summary>
/// A minimal in-memory host wiring up MockingBird over <see cref="TestDocuments.BuildSampleDocument"/>,
/// with a <see cref="FakeMockProvider"/> in place of OpenRouter, so the middleware pipeline (routing,
/// caching, validation, retry, pass-through) can be exercised end-to-end without a real ASP.NET Core
/// app or network calls.
/// </summary>
public sealed class PipelineTestHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly RealEndpointTracker _tracker;

    public HttpClient Client { get; }
    public FakeMockProvider Provider { get; }
    public bool RealEndpointWasCalled => _tracker.WasCalled;

    private PipelineTestHost(IHost host, HttpClient client, FakeMockProvider provider, RealEndpointTracker tracker)
    {
        _host = host;
        Client = client;
        Provider = provider;
        _tracker = tracker;
    }

    public static async Task<PipelineTestHost> StartAsync(
        Action<MockingBirdOptions>? configure = null, FakeMockProvider? provider = null)
    {
        provider ??= new FakeMockProvider("""{"id":"11111111-1111-1111-1111-111111111111","name":"Widget"}""");
        var tracker = new RealEndpointTracker();

        var hostBuilder = new HostBuilder().ConfigureWebHost(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddMockingBird(options =>
                {
                    options.DocumentFactory = (_, _) => Task.FromResult(TestDocuments.BuildSampleDocument());
                    configure?.Invoke(options);
                });
                services.AddSingleton<IMockProvider>(provider);
            });
            webBuilder.Configure(app =>
            {
                app.UseRouting();
                app.UseMockingBird();
                app.Run(context =>
                {
                    tracker.WasCalled = true;
                    context.Response.StatusCode = 200;
                    return context.Response.WriteAsync("real-endpoint");
                });
            });
        });

        var host = await hostBuilder.StartAsync();
        return new PipelineTestHost(host, host.GetTestClient(), provider, tracker);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private sealed class RealEndpointTracker
    {
        public bool WasCalled { get; set; }
    }
}
