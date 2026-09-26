using System.Threading.Channels;

namespace Platform.Infrastructure.Queue;

public record GenerationJobMessage(Guid JobId, Guid ProductId, Guid UserId);

public interface IJobQueue
{
    Task EnqueueAsync(GenerationJobMessage message, CancellationToken ct = default);
    IAsyncEnumerable<GenerationJobMessage> DequeueAllAsync(CancellationToken ct = default);
}

/// <summary>
/// In-memory queue for local development/demo purposes only. Replace with a
/// RedisJobQueue or RabbitMqJobQueue implementation of IJobQueue for
/// production — the API and worker code never need to know the difference.
/// </summary>
public class InMemoryJobQueue : IJobQueue
{
    private readonly Channel<GenerationJobMessage> _channel =
        Channel.CreateUnbounded<GenerationJobMessage>();

    public async Task EnqueueAsync(GenerationJobMessage message, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(message, ct);

    public IAsyncEnumerable<GenerationJobMessage> DequeueAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
}
