using Microsoft.Extensions.Logging;
using opcUaConnectionTest.OPC;

class Program
{
    public static async Task Main(string[] args)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });

        ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
        logger.LogDebug("Starting OPC UA connection test...");

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            logger.LogInformation("Shutdown requested (Ctrl+C). Closing OPC UA sessions...");
            e.Cancel = true;
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            cts.Cancel();
        };

        await using var opcUaTester = new OpcUaTester(loggerFactory);

        try
        {
            await opcUaTester.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogInformation("OPC UA connection test cancelled.");
        }

        // DisposeAsync is called automatically by 'await using',
        // which closes all OPC UA sessions on the server.
        logger.LogInformation("Shutdown complete.");
    }
}
