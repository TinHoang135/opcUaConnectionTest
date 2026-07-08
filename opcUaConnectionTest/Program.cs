using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using unwindRollRuntime.Unwind;
using unwindRollRuntime.ZMQ;
using unwindRollRuntime.Services;
using System.Threading;

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

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            // create lineUnwinds object
            LineUnwinds lineUnwinds = configuration.GetSection("Unwinds").Get<LineUnwinds>()
                ?? throw new InvalidOperationException("Unwinds section is missing from appsettings.json.");

            // create zmqSubcriberData object
            ZmqSubscriberData zmqSubscriberData = configuration.GetSection("ZmqSubscriberData").Get<ZmqSubscriberData>()
                ?? throw new InvalidOperationException("ZmqSubscriberData section is missing from appsettings.json."); ;

            // create zmqSubcriber
            ZmqSubscriber zmqSubscriber = new(
                logger: loggerFactory.CreateLogger<ZmqSubscriber>(),
                config: zmqSubscriberData,
                sharedData: sharedDataObject);

            await using var unwindRollRunTimeCollector = new UnwindRollRunTimeCollector(
                logger: loggerFactory.CreateLogger<UnwindRollRunTimeCollector>(),
                sharedDataObject: sharedDataObject,
                lineUnwinds: lineUnwinds);

            // Launch the tasks
            Task zmqSubscriberTask = Task.Run(() => zmqSubscriber.RunZmqTask(cts.Token));

            Task unwindRollRunTimeAnalyzer = unwindRollRunTimeCollector.UnwindRollRunTimeAnalyzerTask(cts.Token);

            var tasks = new List<Task>
                {
                    zmqSubscriberTask,
                    unwindRollRunTimeAnalyzer
                };

            // Monitor until all terminate or cancellation requested
            while (tasks.Count > 0)
            {
                Task finishedTask = await Task.WhenAny(tasks);
                tasks.Remove(finishedTask);

                if (finishedTask == zmqSubscriberTask)
                {
                    logger.LogInformation("ZMQ subscriber stopped.");
                }

                else if (finishedTask == unwindRollRunTimeAnalyzer)
                {
                    logger.LogInformation("Unwind roll run time analyzer task stopped.");
                }

                // Propagate any exceptions
                await finishedTask;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogInformation("Unwind roll run-time collection cancelled.");
        }

        // DisposeAsync is called automatically by 'await using',
        // which closes all OPC UA sessions on the server.
        logger.LogInformation("Shutdown complete.");
    }
}
