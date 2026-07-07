using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using unwindRollRuntime.Unwind;
using unwindRollRuntime.ZMQ;

namespace unwindRollRuntime.Services
{
    public class UnwindRollRunTimeCollector
    {
        #region Fields

        private readonly ILogger<UnwindRollRunTimeCollector> _logger;
        private readonly SharedDataObject _sharedDataObject;
        private readonly ZmqSubscriber _ZMQsubscriber;
        private readonly LineUnwinds _lineUnwinds;

        #endregion Fields

        #region Properties

        #endregion Properties

        #region Constructors

        public UnwindRollRunTimeCollector(
            ILogger<UnwindRollRunTimeCollector> logger,
            SharedDataObject sharedDataObject,
            ZmqSubscriber zmqSubscriber,
            LineUnwinds lineUnwinds)
        {
            _logger = logger;
            _sharedDataObject = sharedDataObject;
            _ZMQsubscriber = zmqSubscriber;
            _lineUnwinds = lineUnwinds;
        }

        #endregion Constructors


        #region Methods

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            
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

    }
}
