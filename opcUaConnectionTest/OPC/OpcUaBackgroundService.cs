using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace opcUaConnectionTest.OPC
{
    public sealed class OpcUaBackgroundService : IHostedService, IDisposable
    {
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan ReconcileDelay = TimeSpan.FromSeconds(10);

        private readonly IOpcUaConnectionManager _connectionManager;
        private readonly ILogger<OpcUaBackgroundService> _logger;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maxDelay;
        private readonly TimeSpan _reconcileDelay;

        private CancellationTokenSource? _internalCancellationTokenSource;
        private Task? _executingTask;

        public OpcUaBackgroundService(IOpcUaConnectionManager connectionManager, ILogger<OpcUaBackgroundService> logger)
            : this(connectionManager, logger, InitialDelay, MaxDelay, ReconcileDelay)
        {
        }

        internal OpcUaBackgroundService(
            IOpcUaConnectionManager connectionManager,
            ILogger<OpcUaBackgroundService> logger,
            TimeSpan initialDelay,
            TimeSpan maxDelay,
            TimeSpan reconcileDelay)
        {
            this._connectionManager = connectionManager;
            this._logger = logger;
            _initialDelay = initialDelay;
            _maxDelay = maxDelay;
            _reconcileDelay = reconcileDelay;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_executingTask is { IsCompleted: false })
                return Task.CompletedTask;

            _internalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = Task.Run(() => RunAsync(_internalCancellationTokenSource.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var delay = _initialDelay;
            var hasLoggedReady = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _connectionManager.InitializeAsync(cancellationToken);

                    if (!hasLoggedReady)
                    {
                        _logger.LogInformation("OPC UA initialization loop started.");
                        hasLoggedReady = true;
                    }

                    delay = _initialDelay;
                    await Task.Delay(_reconcileDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OPC UA initialization failed. Retrying in {Seconds}s.", delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, _maxDelay.TotalSeconds));
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_internalCancellationTokenSource == null) return;

            _internalCancellationTokenSource.Cancel();

            if (_executingTask != null)
            {
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }

            _internalCancellationTokenSource.Dispose();
            _internalCancellationTokenSource = null;
            _executingTask = null;
        }

        public void Dispose()
        {
            _internalCancellationTokenSource?.Cancel();
            _internalCancellationTokenSource?.Dispose();
            _internalCancellationTokenSource = null;
            _executingTask = null;
        }
    }
}
