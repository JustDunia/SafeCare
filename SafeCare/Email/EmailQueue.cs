using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SafeCare.Email
{
    /// <summary>
    /// Hand-off point between request handling and e-mail delivery.
    /// </summary>
    /// <remarks>
    /// Registered as a singleton: the channel has to be shared by every Blazor circuit that
    /// enqueues and by the single background service that drains it.
    /// </remarks>
    public interface IEmailQueue
    {
        /// <summary>
        /// Queues a message for delivery and returns immediately. Never blocks and never
        /// throws, so submitting a report is not slowed down or failed by the mail server.
        /// </summary>
        void Enqueue(EmailMessage message);

        /// <summary>
        /// Streams queued messages until cancellation. Consumed only by
        /// <see cref="EmailBackgroundService"/>.
        /// </summary>
        IAsyncEnumerable<EmailMessage> DequeueAllAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Unbounded in-memory <see cref="Channel{T}"/> implementation of <see cref="IEmailQueue"/>.
    /// </summary>
    /// <remarks>
    /// Because the queue lives in process memory, messages still waiting when the application
    /// stops are lost. That is an accepted trade-off: the notification is a convenience, and
    /// the report itself is already safely committed to the database.
    /// </remarks>
    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<EmailMessage> _channel =
            Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions
            {
                SingleReader = true,   // only EmailBackgroundService reads
                SingleWriter = false   // multiple Blazor circuits can enqueue concurrently
            });

        public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

        public async IAsyncEnumerable<EmailMessage> DequeueAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return message;
            }
        }
    }
}
