using System.Threading.Channels;
using AgentFlow.Api.Models;

namespace AgentFlow.Api.Services;

// Skickar händelser från orkestreraren till alla webbläsare som följer en körning.
public class RunEvents
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, List<Channel<RunEvent>>> _subscribers = [];

    public Subscription Subscribe(Guid runId)
    {
        var channel = Channel.CreateUnbounded<RunEvent>();
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(runId, out var list))
                _subscribers[runId] = list = [];
            list.Add(channel);
        }
        return new Subscription(channel.Reader, () => Unsubscribe(runId, channel));
    }

    public void Publish(Guid runId, RunEvent e)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(runId, out var list)) return;
            foreach (var channel in list) channel.Writer.TryWrite(e);
        }
    }

    private void Unsubscribe(Guid runId, Channel<RunEvent> channel)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(runId, out var list)) return;
            list.Remove(channel);
            if (list.Count == 0) _subscribers.Remove(runId);
        }
        channel.Writer.TryComplete();
    }

    public sealed class Subscription(ChannelReader<RunEvent> reader, Action dispose) : IDisposable
    {
        public ChannelReader<RunEvent> Reader => reader;
        public void Dispose() => dispose();
    }
}
