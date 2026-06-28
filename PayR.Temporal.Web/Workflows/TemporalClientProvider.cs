using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace PayR.Temporal.Web.Workflows;

/// <summary>
/// Connects to Temporal once per namespace and reuses each client for the
/// app lifetime. The Web UI starts workflows in multiple namespaces
/// (e.g. <c>say-hello</c>, <c>payout</c>), so a single cached client is
/// not enough — the namespace is bound to the client at connect time.
/// </summary>
public sealed class TemporalClientProvider : IAsyncDisposable
{
    private readonly TemporalSettings _settings;
    private readonly ILogger<TemporalClientProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, ITemporalClient> _clients = new();
    private bool _disposed;

    public TemporalClientProvider(IOptions<TemporalSettings> settings, ILogger<TemporalClientProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>Returns a cached client for the given namespace, connecting on first use.</summary>
    public async Task<ITemporalClient> GetClientAsync(
        string @namespace,
        CancellationToken cancellationToken = default)
    {
        if (_clients.TryGetValue(@namespace, out var existing)) return existing;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_clients.TryGetValue(@namespace, out existing)) return existing;

            _logger.LogInformation("Connecting to Temporal at {Address} (namespace {Namespace})",
                _settings.Address, @namespace);

            var options = new TemporalClientConnectOptions(_settings.Address)
            {
                Namespace = @namespace,
            };
            var client = await TemporalClient.ConnectAsync(options).ConfigureAwait(false);
            _clients[@namespace] = client;
            return client;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var client in _clients.Values)
        {
            if (client is IAsyncDisposable d) await d.DisposeAsync().ConfigureAwait(false);
        }
        _clients.Clear();
        _gate.Dispose();
    }
}

/// <summary>Connection settings for Temporal. Bound from <c>Temporal</c> in appsettings.</summary>
public sealed class TemporalSettings
{
    public string Address { get; set; } = "localhost:7233";
    /// <summary>
    /// Default namespace used when a caller doesn't specify one. Individual
    /// workflow definitions declare their own namespace via
    /// <see cref="IWorkflowDefinition.Namespace"/>.
    /// </summary>
    public string Namespace { get; set; } = "default";
    /// <summary>Public Temporal Web UI URL (opened in a new window from the nav).</summary>
    public string UiUrl { get; set; } = "http://localhost:8233";
}
