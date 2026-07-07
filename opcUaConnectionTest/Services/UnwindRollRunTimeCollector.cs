using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using unwindRollRuntime.Unwind;
using unwindRollRuntime.ZMQ;

namespace unwindRollRuntime.Services
{
    public class UnwindRollRunTimeCollector: IAsyncDisposable
    {
        private readonly ILogger<UnwindRollRunTimeCollector> _logger;
        private readonly SharedDataObject _sharedDataObject;
        private readonly ZmqSubscriber _zmqSubscriber;
        private readonly LineUnwinds _lineUnwinds;

        #region Constructors
        public UnwindRollRunTimeCollector(
            ILogger<UnwindRollRunTimeCollector> logger,
            SharedDataObject sharedDataObject,
            ZmqSubscriber zmqSubscriber,
            LineUnwinds lineUnwinds)
        {
            _logger = logger;
            _sharedDataObject = sharedDataObject;
            _zmqSubscriber = zmqSubscriber;
            _lineUnwinds = lineUnwinds;
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
        #endregion Constructors


        #region Methods
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    // Launch the three main tasks
                    Task zmqSubscriberTask = Task.Run(() => _zmqSubscriber.RunAsync(cancellationToken));

                    // Task glueLoadingPlannerTask = _espressoMissionPlanningService.Value.GlueLoadingMissionPlannerAsync(cancellationToken);

                    Task unwindRollRunTimeAnalyzer = UnwindRollRunTimeAnalyzer(cancellationToken);

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
                            Console.WriteLine("ZMQ subscriber stopped.");
                        }

                        else if (finishedTask == unwindRollRunTimeAnalyzer)
                        {
                            Console.WriteLine("Unwind roll run time analyzer task stopped.");
                        }

                        // Propagate any exceptions
                        await finishedTask;
                    }
                }, cancellationToken);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Service");
            }

            // immediately return so startup can continue
            return;
        }

        public async Task UnwindRollRunTimeAnalyzer(CancellationToken cancellationToken = default)
        {
            Unwind.Unwind AQL_Unwind = _lineUnwinds.Unwinds.FirstOrDefault(unwind => unwind.Name == "AQL") ?? throw new Exception(" AQL Unwind has not been configured");
            Unwind.Unwind CC_Unwind = _lineUnwinds.Unwinds.FirstOrDefault(unwind => unwind.Name == "CC") ?? throw new Exception(" NWCC Unwind has not been configured");
            Unwind.Unwind DL_Unwind = _lineUnwinds.Unwinds.FirstOrDefault(unwind => unwind.Name == "DL") ?? throw new Exception(" NWDL Unwind has not been configured");
            Unwind.Unwind TS_Unwind = _lineUnwinds.Unwinds.FirstOrDefault(unwind => unwind.Name == "TS") ?? throw new Exception(" TS Unwind has not been configured");
            Unwind.Unwind Cuff_Unwind = _lineUnwinds.Unwinds.FirstOrDefault(unwind => unwind.Name == "Cuff") ?? throw new Exception(" Cuff Unwind has not been configured");
            Unwind.Unwind IBU_Unwind = _lineUnwinds.Unwinds.FirstOrDefault(unwind => unwind.Name == "IBU") ?? throw new Exception(" IBU Unwind has not been configured");
            Unwind.Unwind OBU_Unwind = _lineUnwinds.Unwinds.FirstOrDefault(unwind => unwind.Name == "OBU") ?? throw new Exception(" OBU Unwind has not been configured");

            int elapsedTimeInSeconds = 2; // unit: seconds

            // ignore the first ZMQ messages
            int numberOfScans = 0;
            bool firstScans = true;
            double lineState = 1;
            const double lineProducing = 6;
            const double lineStartUpReject = 5;
            const double lineManualReject = 18;
            const double lineThreadingUp = 11;
            const double lineToRun = 12;
            const double lineToThread = 10;
            double productionTimeInSeconds = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(elapsedTimeInSeconds * 1000);

                productionTimeInSeconds += elapsedTimeInSeconds;
                if (productionTimeInSeconds % 900 == 0)
                {
                    _logger.LogInformation($"Current production time is {productionTimeInSeconds} seconds.");
                }

                // implement the logic to recognize the first scans, and not triggered ARL tasks during these first scans  
                if (numberOfScans < 20)
                {
                    numberOfScans++;
                    firstScans = true;
                }
                else if (firstScans == true)
                    firstScans = false;
                else { }

                // check line state zmq data
                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_Line_State))
                {
                    if (lineState != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_Line_State])
                    {
                        lineState = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_Line_State];

                        if ((lineState == lineProducing) || (lineState == lineStartUpReject)
                            || (lineState == lineManualReject) || (lineState == lineThreadingUp)
                            || (lineState == lineToRun) || (lineState == lineToThread))
                        {
                            _sharedDataObject.LineRunning = true;
                        }
                        else
                        {
                            _sharedDataObject.LineRunning = false;
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_AQL_Roll_A_Active))
                {
                    if (AQL_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_AQL_Roll_A_Active])
                    {
                        AQL_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_AQL_Roll_A_Active];
                        if (firstScans == false)
                        {
                            _logger.LogInformation($"{AQL_Unwind.Name} has spliced.");
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_CC_Roll_A_Active))
                {
                    if (CC_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_CC_Roll_A_Active])
                    {
                        CC_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_CC_Roll_A_Active];
                        if (firstScans == false)
                        {
                            _logger.LogInformation($"{CC_Unwind.Name} has spliced.");
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_DS_Roll_A_Active))
                {
                    if (DL_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_DS_Roll_A_Active])
                    {
                        DL_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_DS_Roll_A_Active];
                        if (firstScans == false)
                        {
                            _logger.LogInformation($"{DL_Unwind.Name} has spliced.");
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_TS_Roll_A_Active))
                {
                    if (TS_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_TS_Roll_A_Active])
                    {
                        TS_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_TS_Roll_A_Active];
                        if (firstScans == false)
                        {
                            _logger.LogInformation($"{TS_Unwind.Name} has spliced.");
                        }
                    }
                }
                    
                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_Cuff_Roll_A_Active))
                {
                    if (Cuff_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_Cuff_Roll_A_Active])
                    {
                        Cuff_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_Cuff_Roll_A_Active];
                        if (firstScans == false)
                        {
                            _logger.LogInformation($"{Cuff_Unwind.Name} has spliced.");
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_IBU_Roll_A_Active))
                {
                    if (IBU_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_IBU_Roll_A_Active])
                    {
                        IBU_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_IBU_Roll_A_Active];
                        if (firstScans == false)
                        {
                            _logger.LogInformation($"{IBU_Unwind.Name} has spliced.");
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_OBU_Roll_A_Active))
                {
                    if (OBU_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_OBU_Roll_A_Active])
                    {
                        OBU_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_OBU_Roll_A_Active];
                        if (firstScans == false)
                        {
                            _logger.LogInformation($"{OBU_Unwind.Name} has spliced.");
                        }
                    }
                }
            }
        }
        #endregion Methods

    }
}
