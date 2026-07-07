using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using unwindRollRuntime.Unwind;
using unwindRollRuntime.ZMQ;
using unwindRollRuntime.Services;

class Program
{
    public static async Task Main(string[] args)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });

        ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
        logger.LogDebug("Starting unwind roll run-time collection...");

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

        try
        {
            // create data object
            SharedDataObject sharedDataObject = new();

            // create lineUnwinds object
            LineUnwinds lineUnwinds = LoadLineUnwinds();

            // create zmqSubcriberData object
            ZmqSubscriberData zmqSubscriberData = LoadZmqSubscriberData();

            // create zmqSubcriber
            ZmqSubscriber zmqSubscriber = new(
                logger: loggerFactory.CreateLogger<ZmqSubscriber>(),
                config: zmqSubscriberData,
                sharedData: sharedDataObject);

            await using var unwindRollRunTimeCollector = new UnwindRollRunTimeCollector(
                logger: loggerFactory.CreateLogger<UnwindRollRunTimeCollector>(),
                sharedDataObject: sharedDataObject,
                zmqSubscriber: zmqSubscriber,
                lineUnwinds: lineUnwinds);

            await unwindRollRunTimeCollector.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogInformation("Unwind roll run-time collection cancelled.");
        }

        // DisposeAsync is called automatically by 'await using',
        // which closes all OPC UA sessions on the server.
        logger.LogInformation("Shutdown complete.");
    }

    private static LineUnwinds LoadLineUnwinds()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        return configuration.GetSection("Unwinds").Get<LineUnwinds>()
            ?? throw new InvalidOperationException("Unwinds section is missing from appsettings.json.");
    }

    private static ZmqSubscriberData LoadZmqSubscriberData()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        return configuration.GetSection("ZmqSubscriberData").Get<ZmqSubscriberData>()
            ?? throw new InvalidOperationException("ZmqSubscriberData section is missing from appsettings.json.");
    }
}
