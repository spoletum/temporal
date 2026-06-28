using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace PayR.Temporal.Web.Workflows;

/// <summary>Connects to Temporal once and reuses the client for the app lifetime.</summary>
public sealed class TemporalClientProvider : IAsyncDisposable
{
    private readonly TemporalSettings _settings;
    private readonly ILogger<TemporalClientProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ITemporalClient? _client;
    private bool _disposed;

    public TemporalClientProvider(IOptions<TemporalSettings> settings, ILogger<TemporalClientProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ITemporalClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        if (_client is not null) return _client;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null) return _client;

            _logger.LogInformation("Connecting to Temporal at {Address} (namespace {Namespace})",
                _settings.Address, _settings.Namespace);

            var options = new TemporalClientConnectOptions(_settings.Address)
            {
                Namespace = _settings.Namespace,
            };
            _client = await TemporalClient.ConnectAsync(options).ConfigureAwait(false);
            return _client;
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
        if (_client is IAsyncDisposable d) await d.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}

/// <summary>Connection settings for Temporal. Bound from <c>Temporal</c> in appsettings.</summary>
public sealed class TemporalSettings
{
    public string Address { get; set; } = "localhost:7233";
    public string Namespace { get; set; } = "default";
    /// <summary>Public Temporal Web UI URL (opened in a new window from the nav).</summary>
    public string UiUrl { get; set; } = "http://localhost:8233";
}
