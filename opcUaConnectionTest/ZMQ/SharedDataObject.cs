using System.Collections.Concurrent;

namespace unwindRollRuntime.ZMQ
{
    public class SharedDataObject
    {
        public ConcurrentDictionary<string, double> ZmqData { get; set; } = new();
    }
}
