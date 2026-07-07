using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using NetMQ.Sockets;
using NetMQ;

namespace unwindRollRuntime.ZMQ
{
    public class ZmqTopic
    {
        public required string ReferenceName { get; set; }
    }

    public class ZmqSubscriberData
    {
        public required string EndPoint { get; set; }
        public List<ZmqTopic> ZmqTopics { get; set; } = new();
    }

    #region ZMQ
    public class ZmqSubscriber
    {
        private readonly ILogger<ZmqSubscriber> _logger;
        private readonly ZmqSubscriberData _config;
        private readonly SharedDataObject _dataObject;

        public ZmqSubscriber(
            ILogger<ZmqSubscriber> logger,
            IOptions<ZmqSubscriberData> config,
            SharedDataObject sharedData)
        {
            _logger = logger;
            _config = config.Value;
            _dataObject = sharedData;
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ZMQ Subscriber starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    RunSubscriber(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ZMQ subscriber crashed. Retrying in 10 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("ZMQ Subscriber stopped.");
        }

        private void RunSubscriber(CancellationToken token)
        {
            using var subscriber = new SubscriberSocket();

            subscriber.Options.ReceiveHighWatermark = 1000000;

            foreach (var topic in _config.ZmqTopics)
            {
                subscriber.Subscribe(topic.ReferenceName);
            }

            subscriber.Connect(_config.EndPoint);

            _logger.LogInformation("Connected to ZMQ at {EndPoint}", _config.EndPoint);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!subscriber.TryReceiveFrameString(TimeSpan.FromMilliseconds(10000), out var topic))
                        continue;

                    if (!subscriber.TryReceiveFrameString(TimeSpan.FromMilliseconds(10000), out var message))
                        continue;

                    if (TryParseZmqMessage(message, out var referenceName, out var value))
                    {
                        _dataObject.ZmqData[referenceName] = value;
                    }
                    else
                    {
                        _logger.LogDebug("Invalid message format: {Message}", message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while receiving ZMQ message");
                }
            }
        }

        private bool TryParseZmqMessage(string message, out string referenceName, out double value)
        {
            referenceName = string.Empty;
            value = default;

            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message);

                if (data == null) return false;

                if (!TryGetString(data, out referenceName, "ReferenceName", "Reference Name"))
                    return false;

                if (!TryGetDouble(data, out value, "Value", "value"))
                    return false;

                return true;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON received");
                return false;
            }
        }

        private static bool TryGetString(Dictionary<string, JsonElement> data, out string value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (data.TryGetValue(key, out var elem) && elem.ValueKind == JsonValueKind.String)
                {
                    value = elem.GetString()!;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static bool TryGetDouble(Dictionary<string, JsonElement> data, out double value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!data.TryGetValue(key, out var elem)) continue;

                if (elem.ValueKind == JsonValueKind.Number)
                {
                    value = elem.GetDouble();
                    return true;
                }

                if (elem.ValueKind == JsonValueKind.String &&
                    double.TryParse(elem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
    #endregion ZMQ
}
