using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeCare.E2ETests.Infrastructure;

public sealed record MailPitMessage(
    [property: JsonPropertyName("ID")] string Id,
    [property: JsonPropertyName("Subject")] string Subject,
    [property: JsonPropertyName("Bcc")] List<MailPitAddress>? Bcc,
    [property: JsonPropertyName("To")] List<MailPitAddress>? To);

public sealed record MailPitAddress([property: JsonPropertyName("Address")] string Address);

/// <summary>
/// Reads what the application actually handed to the mail server.
/// </summary>
public sealed class MailPitClient(string baseUrl) : IDisposable
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri(baseUrl) };

    public async Task DeleteAllAsync()
    {
        using var response = await _client.DeleteAsync("/api/v1/messages");
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Polls until a message whose subject contains <paramref name="subjectFragment"/> arrives.
    /// Delivery is asynchronous - the background service drains a queue - so waiting is required.
    /// </summary>
    public async Task<MailPitMessage> WaitForMessageAsync(string subjectFragment, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var payload = await _client.GetStringAsync("/api/v1/messages");
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.TryGetProperty("messages", out var messages))
            {
                foreach (var element in messages.EnumerateArray())
                {
                    var message = element.Deserialize<MailPitMessage>();
                    if (message is not null && message.Subject.Contains(subjectFragment))
                    {
                        return message;
                    }
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"No message with subject containing '{subjectFragment}' arrived");
    }

    /// <summary>
    /// Fetches the rendered HTML body of a message; the listing endpoint leaves it out.
    /// </summary>
    public async Task<string> GetHtmlBodyAsync(string messageId)
    {
        var payload = await _client.GetStringAsync($"/api/v1/message/{messageId}");
        using var document = JsonDocument.Parse(payload);

        return document.RootElement.GetProperty("HTML").GetString() ?? "";
    }

    public void Dispose() => _client.Dispose();
}
