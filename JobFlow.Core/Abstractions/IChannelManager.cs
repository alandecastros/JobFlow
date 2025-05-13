using System.Threading.Channels;

namespace JobFlow.Core.Abstractions;

public interface IChannelManager
{
    ChannelWriter<object> GetWriter(string queueName);
    ChannelReader<object> GetReader(string queueName);
    Channel<object> GetOrCreateChannel(string queueName);
}
