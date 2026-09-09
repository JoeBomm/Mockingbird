using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MockingBird.Middleware.Configuration;
using Swashbuckle.AspNetCore.Swagger;

namespace MockingBird.Middleware.Routing;

/// <summary>
/// Lazily resolves the app's OpenAPI document and the <see cref="MockOperationRegistry"/> built from
/// it, on first use. Swashbuckle's swagger provider needs a fully-built app and a DI scope, so this
/// can't run at <c>AddMockingBird</c> time — it runs on the first request that reaches the middleware.
/// </summary>
public sealed class MockOperationRegistrySource(
    IServiceProvider serviceProvider,
    IOptions<MockingBirdOptions> options)
{
    private readonly SemaphoreSlim _documentGate = new(1, 1);
    private readonly SemaphoreSlim _registryGate = new(1, 1);
    private OpenApiDocument? _document;
    private MockOperationRegistry? _registry;

    public async Task<MockOperationRegistry> GetAsync(CancellationToken cancellationToken)
    {
        if (_registry is not null)
        {
            return _registry;
        }

        var document = await GetDocumentAsync(cancellationToken).ConfigureAwait(false);

        await _registryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _registry ??= new MockOperationRegistry(document, options.Value);
            return _registry;
        }
        finally
        {
            _registryGate.Release();
        }
    }

    public async Task<OpenApiDocument> GetDocumentAsync(CancellationToken cancellationToken)
    {
        if (_document is not null)
        {
            return _document;
        }

        await _documentGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _document ??= await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return _document;
        }
        finally
        {
            _documentGate.Release();
        }
    }

    private async Task<OpenApiDocument> LoadDocumentAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (opts.DocumentFactory is not null)
        {
            return await opts.DocumentFactory(serviceProvider, cancellationToken).ConfigureAwait(false);
        }

        await using var scope = serviceProvider.CreateAsyncScope();

        if (scope.ServiceProvider.GetService<IAsyncSwaggerProvider>() is { } asyncProvider)
        {
            return await asyncProvider.GetSwaggerAsync(opts.DocumentName, null, null).ConfigureAwait(false);
        }

        if (scope.ServiceProvider.GetService<ISwaggerProvider>() is { } syncProvider)
        {
            return syncProvider.GetSwagger(opts.DocumentName, null, null);
        }

        throw new InvalidOperationException(
            "MockingBird could not find a Swashbuckle ISwaggerProvider/IAsyncSwaggerProvider in the " +
            "service container. Call AddSwaggerGen() before AddMockingBird(), or supply " +
            "MockingBirdOptions.DocumentFactory to source the OpenAPI document yourself.");
    }
}
