using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using unwindRollRuntime.Unwind;
using unwindRollRuntime.ZMQ;

namespace unwindRollRuntime.Services
{
    public class UnwindRollRunTimeCollector
    {
        private readonly ILogger<UnwindRollRunTimeCollector> _logger;
        private readonly SharedDataObject _sharedDataObject;
        private readonly LineUnwinds _lineUnwinds;

        #region Constructors
        public UnwindRollRunTimeCollector(
            ILogger<UnwindRollRunTimeCollector> logger,
            SharedDataObject sharedDataObject,
            LineUnwinds lineUnwinds)
        {
            _logger = logger;
            _sharedDataObject = sharedDataObject;
            _lineUnwinds = lineUnwinds;
        }

        #endregion Constructors

        #region Methods
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
            TimeSpan rollRunTime;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(elapsedTimeInSeconds * 1000, cancellationToken);

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
                            || (lineState == lineToRun))
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
                    if (!_sharedDataObject.LineRunning)
                    {
                        AQL_Unwind.LineDownTimer += elapsedTimeInSeconds;
                    }
                    if (AQL_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_AQL_Roll_A_Active])
                    {
                        AQL_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_AQL_Roll_A_Active];
                        if (firstScans == false)
                        {
                            if (AQL_Unwind.lastSpliceTime is not null)
                            {
                                rollRunTime = DateTimeOffset.UtcNow - AQL_Unwind.lastSpliceTime.Value;

                                _logger.LogInformation(
                                    "{Unwind} has spliced. Roll run time was {Runtime}. During this time, line was down for {Downtime} seconds.",
                                    AQL_Unwind.Name,
                                    rollRunTime,
                                    AQL_Unwind.LineDownTimer);
                            }
                            else
                            {
                                _logger.LogInformation(
                                   "{Unwind} has the first splice", AQL_Unwind.Name);
                            }
                            AQL_Unwind.lastSpliceTime = DateTimeOffset.UtcNow;
                            AQL_Unwind.LineDownTimer = 0;
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_CC_Roll_A_Active))
                {
                    if (!_sharedDataObject.LineRunning)
                    {
                        CC_Unwind.LineDownTimer += elapsedTimeInSeconds;
                    }
                    if (CC_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_CC_Roll_A_Active])
                    {
                        CC_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_CC_Roll_A_Active];
                        if (firstScans == false)
                        {
                            if (CC_Unwind.lastSpliceTime is not null)
                            {
                                rollRunTime = DateTimeOffset.UtcNow - CC_Unwind.lastSpliceTime.Value;

                                _logger.LogInformation(
                                    "{Unwind} has spliced. Roll run time was {Runtime}. During this time, line was down for {Downtime} seconds.",
                                    CC_Unwind.Name,
                                    rollRunTime,
                                    CC_Unwind.LineDownTimer);
                            }
                            else
                            {
                                _logger.LogInformation(
                                   "{Unwind} has the first splice", CC_Unwind.Name);
                            }
                            CC_Unwind.lastSpliceTime = DateTimeOffset.UtcNow;
                            CC_Unwind.LineDownTimer = 0;
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_DS_Roll_A_Active))
                {
                    if (!_sharedDataObject.LineRunning)
                    {
                        DL_Unwind.LineDownTimer += elapsedTimeInSeconds;
                    }
                    if (DL_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_DS_Roll_A_Active])
                    {
                        DL_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_DS_Roll_A_Active];
                        if (firstScans == false)
                        {
                            if (DL_Unwind.lastSpliceTime is not null)
                            {
                                rollRunTime = DateTimeOffset.UtcNow - DL_Unwind.lastSpliceTime.Value;

                                _logger.LogInformation(
                                    "{Unwind} has spliced. Roll run time was {Runtime}. During this time, line was down for {Downtime} seconds.",
                                    DL_Unwind.Name,
                                    rollRunTime,
                                    DL_Unwind.LineDownTimer);
                            }
                            else
                            {
                                _logger.LogInformation(
                                   "{Unwind} has the first splice", DL_Unwind.Name);
                            }
                            DL_Unwind.lastSpliceTime = DateTimeOffset.UtcNow;
                            DL_Unwind.LineDownTimer = 0;
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_TS_Roll_A_Active))
                {
                    if (!_sharedDataObject.LineRunning)
                    {
                        TS_Unwind.LineDownTimer += elapsedTimeInSeconds;
                    }
                    if (TS_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_TS_Roll_A_Active])
                    {
                        TS_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_TS_Roll_A_Active];
                        if (firstScans == false)
                        {
                            if (TS_Unwind.lastSpliceTime is not null)
                            {
                                rollRunTime = DateTimeOffset.UtcNow - TS_Unwind.lastSpliceTime.Value;

                                _logger.LogInformation(
                                    "{Unwind} has spliced. Roll run time was {Runtime}. During this time, line was down for {Downtime} seconds.",
                                    TS_Unwind.Name,
                                    rollRunTime,
                                    TS_Unwind.LineDownTimer);
                            }
                            else
                            {
                                _logger.LogInformation(
                                   "{Unwind} has the first splice", TS_Unwind.Name);
                            }
                            TS_Unwind.lastSpliceTime = DateTimeOffset.UtcNow;
                            TS_Unwind.LineDownTimer = 0;
                        }
                    }
                }
                    
                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_Cuff_Roll_A_Active))
                {
                    if (!_sharedDataObject.LineRunning)
                    {
                        Cuff_Unwind.LineDownTimer += elapsedTimeInSeconds;
                    }
                    if (Cuff_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_Cuff_Roll_A_Active])
                    {
                        Cuff_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_Cuff_Roll_A_Active];
                        if (firstScans == false)
                        {
                            if (Cuff_Unwind.lastSpliceTime is not null)
                            {
                                rollRunTime = DateTimeOffset.UtcNow - Cuff_Unwind.lastSpliceTime.Value;

                                _logger.LogInformation(
                                    "{Unwind} has spliced. Roll run time was {Runtime}. During this time, line was down for {Downtime} seconds.",
                                    Cuff_Unwind.Name,
                                    rollRunTime,
                                    Cuff_Unwind.LineDownTimer);
                            }
                            else
                            {
                                _logger.LogInformation(
                                   "{Unwind} has the first splice", Cuff_Unwind.Name);
                            }
                            Cuff_Unwind.lastSpliceTime = DateTimeOffset.UtcNow;
                            Cuff_Unwind.LineDownTimer = 0;
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_IBU_Roll_A_Active))
                {
                    if (!_sharedDataObject.LineRunning)
                    {
                        IBU_Unwind.LineDownTimer += elapsedTimeInSeconds;
                    }
                    if (IBU_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_IBU_Roll_A_Active])
                    {
                        IBU_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_IBU_Roll_A_Active];
                        if (firstScans == false)
                        {
                            if (IBU_Unwind.lastSpliceTime is not null)
                            {
                                rollRunTime = DateTimeOffset.UtcNow - IBU_Unwind.lastSpliceTime.Value;

                                _logger.LogInformation(
                                    "{Unwind} has spliced. Roll run time was {Runtime}. During this time, line was down for {Downtime} seconds.",
                                    IBU_Unwind.Name,
                                    rollRunTime,
                                    IBU_Unwind.LineDownTimer);
                            }
                            else
                            {
                                _logger.LogInformation("{Unwind} has the first splice", IBU_Unwind.Name);
                            }
                            IBU_Unwind.lastSpliceTime = DateTimeOffset.UtcNow;
                            IBU_Unwind.LineDownTimer = 0;
                        }
                    }
                }

                if (_sharedDataObject.ZmqData.ContainsKey(ServiceConstants.zmq_Topic_OBU_Roll_A_Active))
                {
                    if (!_sharedDataObject.LineRunning)
                    {
                        OBU_Unwind.LineDownTimer += elapsedTimeInSeconds;
                    }
                    if (OBU_Unwind.RollAIsActive != _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_OBU_Roll_A_Active])
                    {
                        OBU_Unwind.RollAIsActive = _sharedDataObject.ZmqData[ServiceConstants.zmq_Topic_OBU_Roll_A_Active];
                        if (firstScans == false)
                        {
                            if (OBU_Unwind.lastSpliceTime is not null)
                            {
                                rollRunTime = DateTimeOffset.UtcNow - OBU_Unwind.lastSpliceTime.Value;

                                _logger.LogInformation(
                                    "{Unwind} has spliced. Roll run time was {Runtime}. During this time, line was down for {Downtime} seconds.",
                                    OBU_Unwind.Name,
                                    rollRunTime,
                                    OBU_Unwind.LineDownTimer);
                            }
                            else
                            {
                                _logger.LogInformation(
                                   "{Unwind} has the first splice", OBU_Unwind.Name);
                            }
                            OBU_Unwind.lastSpliceTime = DateTimeOffset.UtcNow;
                            OBU_Unwind.LineDownTimer = 0;
                        }
                    }
                }
            }
        }
        #endregion Methods

    }
}
