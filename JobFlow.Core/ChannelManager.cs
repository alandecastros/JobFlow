using System.Collections.Concurrent;
using System.Threading.Channels;
using JobFlow.Core.Abstractions;

namespace JobFlow.Core;

public class ChannelManager : IChannelManager
{
    private readonly ConcurrentDictionary<string, Channel<object>> _channels = new();

    public Channel<object> GetOrCreateChannel(string queueName)
    {
        return _channels.GetOrAdd(queueName, _ => Channel.CreateUnbounded<object>());
    }

    public ChannelWriter<object> GetWriter(string queueName)
    {
        return GetOrCreateChannel(queueName).Writer;
    }

    public ChannelReader<object> GetReader(string queueName)
    {
        return GetOrCreateChannel(queueName).Reader;
    }
}
