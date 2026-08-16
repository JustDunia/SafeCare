using System.Runtime.CompilerServices;
using SafeCare.Email;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Records what would have been sent. <see cref="ThrowOnEnqueue"/> simulates a broken mail
/// pipeline, which must never fail the report being submitted.
/// </summary>
public sealed class FakeEmailQueue : IEmailQueue
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent => _sent;

    public bool ThrowOnEnqueue { get; set; }

    public void Enqueue(EmailMessage message)
    {
        if (ThrowOnEnqueue)
        {
            throw new InvalidOperationException("Simulated mail queue failure");
        }

        _sent.Add(message);
    }

    public async IAsyncEnumerable<EmailMessage> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
