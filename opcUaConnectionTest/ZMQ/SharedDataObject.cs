using System.Collections.Concurrent;

namespace unwindRollRuntime.ZMQ
{
    public class SharedDataObject
    {
        public bool LineRunning = true;
        public ConcurrentDictionary<string, double> ZmqData { get; set; } = new();
    }
}
