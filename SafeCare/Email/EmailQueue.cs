using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SafeCare.Email
{
    public interface IEmailQueue
    {
        void Enqueue(EmailMessage message);
        IAsyncEnumerable<EmailMessage> DequeueAllAsync(CancellationToken cancellationToken);
    }

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
