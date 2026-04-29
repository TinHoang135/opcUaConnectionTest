using Microsoft.Extensions.Logging;
using opcUaConnectionTest.OPC;

class Program
{
    private static OpcUaTester? _opcUaTester;

    public static async Task Main(string[] args)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });

        ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
        logger.LogDebug("Starting OPC UA connection test...");

        _opcUaTester = new OpcUaTester(loggerFactory);
        await _opcUaTester.RunAsync();
    }
}
